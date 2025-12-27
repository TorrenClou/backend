using Hangfire;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Text.Json;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Interfaces.Hangfire;
using TorreClou.Core.Shared;
using TorreClou.Core.Specifications;
using TorreClou.Infrastructure.Data;
using TorreClou.Infrastructure.Services;
using TorreClou.Infrastructure.Settings;
using TorreClou.Infrastructure.Workers;
using SyncEntity = TorreClou.Core.Entities.Jobs.Sync;

namespace TorreClou.Sync.Worker.Services
{
    public class S3SyncJob(
        IUnitOfWork unitOfWork,
        ILogger<S3SyncJob> logger,
        IOptions<BackblazeSettings> backblazeSettings,
        IS3ResumableUploadService s3UploadService,
        IJobStatusService jobStatusService)
        : SyncJobBase<S3SyncJob>(unitOfWork, logger, jobStatusService), IS3SyncJob
    {
        private readonly BackblazeSettings _backblazeSettings = backblazeSettings.Value;
        private const long PartSize = 10 * 1024 * 1024; // 10MB
        private const int ProgressUpdateIntervalSeconds = 10;

        // Heartbeat independent from progress
        private const int HeartbeatIntervalSeconds = 15;

        protected override string LogPrefix => "[S3:SYNC]";

      
        // =========================================================
        [Queue("sync")]
        [AutomaticRetry(
            Attempts = 3,
            DelaysInSeconds = new[] { 60, 300, 900 },
            OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public new Task ExecuteAsync(int syncId, CancellationToken cancellationToken = default)
            => base.ExecuteAsync(syncId, cancellationToken);

        protected override async Task ExecuteCoreAsync(SyncEntity sync, UserJob job, CancellationToken cancellationToken)
        {
            var originalDownloadPath = sync.LocalFilePath ?? job.DownloadPath;

            // Start heartbeat loop early (prevents "stuck" when UploadPart takes long)
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeatTask = RunHeartbeatLoopAsync(sync.Id, heartbeatCts.Token);

            try
            {
                if (sync.Status != SyncStatus.PENDING &&
                    sync.Status != SyncStatus.SYNC_RETRY &&
                    sync.Status != SyncStatus.SYNCING)
                {
                    Logger.LogWarning("{LogPrefix} Sync invalid state | SyncId: {SyncId} | Status: {Status}",
                        LogPrefix, sync.Id, sync.Status);
                    return;
                }

                if (string.IsNullOrEmpty(originalDownloadPath) || !Directory.Exists(originalDownloadPath))
                {
                    await MarkSyncFailedAsync(sync, $"Download directory missing: {originalDownloadPath}", hasRetries: false);
                    return;
                }

                Logger.LogInformation("{LogPrefix} Starting | SyncId: {SyncId} | Bucket: {Bucket}",
                    LogPrefix, sync.Id, _backblazeSettings.BucketName);

                sync.StartedAt = DateTime.UtcNow;
                sync.LastHeartbeat = DateTime.UtcNow;

                await JobStatusService.TransitionSyncStatusAsync(
                    sync,
                    SyncStatus.SYNCING,
                    StatusChangeSource.Worker,
                    metadata: new { bucket = _backblazeSettings.BucketName, startedAt = sync.StartedAt });

                var filesToUpload = GetFilesToUpload(originalDownloadPath);
                if (filesToUpload.Length == 0)
                {
                    await MarkSyncFailedAsync(sync, "No files found to upload", hasRetries: false);
                    return;
                }

                var totalBytes = filesToUpload.Sum(f => f.Length);
                if (sync.FilesTotal == 0 || sync.TotalBytes == 0)
                {
                    sync.FilesTotal = filesToUpload.Length;
                    sync.TotalBytes = totalBytes;
                    await UnitOfWork.Complete();
                }

                var overallBytesUploaded = sync.BytesSynced;
                var filesSynced = sync.FilesSynced;
                var lastProgressUpdate = DateTime.UtcNow;

                for (int i = filesSynced; i < filesToUpload.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var file = filesToUpload[i];
                    var relativePath = Path.GetRelativePath(originalDownloadPath, file.FullName);

                    var s3Key = sync.S3KeyPrefix != null
                        ? $"{sync.S3KeyPrefix}/{relativePath.Replace('\\', '/')}"
                        : $"torrents/{job.Id}/{relativePath.Replace('\\', '/')}";

                    Logger.LogInformation("{LogPrefix} File {Index}/{Total} | {FileName} | {SizeMB:F2} MB",
                        LogPrefix, i + 1, filesToUpload.Length, file.Name, file.Length / (1024.0 * 1024.0));

                    var uploadResult = await UploadFileWithResumeAsync(sync, job, file, s3Key, cancellationToken);
                    if (uploadResult.IsFailure)
                    {
                        await MarkSyncFailedAsync(sync, $"Upload failed for {file.Name}: {uploadResult.Error.Message}", hasRetries: true);
                        return;
                    }

                    overallBytesUploaded += file.Length;
                    filesSynced++;

                    // Throttle DB updates (progress)
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressUpdate).TotalSeconds >= ProgressUpdateIntervalSeconds)
                    {
                        sync.BytesSynced = overallBytesUploaded;
                        sync.FilesSynced = filesSynced;
                        sync.LastHeartbeat = now; // extra safety (heartbeat loop also runs)
                        await UnitOfWork.Complete();

                        var percent = totalBytes == 0 ? 0 : (overallBytesUploaded / (double)totalBytes) * 100;
                        Logger.LogInformation("{LogPrefix} Progress: {Percent:F1}% | {Uploaded}/{Total} MB",
                            LogPrefix, percent, overallBytesUploaded >> 20, totalBytes >> 20);

                        lastProgressUpdate = now;
                    }
                }

                sync.CompletedAt = DateTime.UtcNow;
                sync.BytesSynced = overallBytesUploaded;
                sync.FilesSynced = filesSynced;

                await JobStatusService.TransitionSyncStatusAsync(
                    sync,
                    SyncStatus.COMPLETED,
                    StatusChangeSource.Worker,
                    metadata: new { totalBytes = overallBytesUploaded, filesSynced, completedAt = sync.CompletedAt });

                Logger.LogInformation("{LogPrefix} Sync Complete | SyncId: {SyncId}", LogPrefix, sync.Id);

                // Stop heartbeat now (completed)
                heartbeatCts.Cancel();
                try { await heartbeatTask; } catch { /* ignore */ }

                // Safe cleanup (final step)
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                await CleanupBlockStorageAsync(originalDownloadPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{LogPrefix} Fatal error during sync | SyncId: {SyncId} | ExceptionType: {ExceptionType}",
                    LogPrefix, sync.Id, ex.GetType().Name);

                // stop heartbeat before rethrow
                heartbeatCts.Cancel();
                try { await heartbeatTask; } catch { /* ignore */ }

                throw; // let SyncJobBase handle transition + hangfire retry
            }
            finally
            {
                // Ensure heartbeat stops
                heartbeatCts.Cancel();
            }
        }

        private async Task RunHeartbeatLoopAsync(int syncId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), ct);

                    // Reload minimal entity to avoid EF tracking conflicts
                    var hbSpec = new BaseSpecification<SyncEntity>(s => s.Id == syncId);
                    var current = await UnitOfWork.Repository<SyncEntity>().GetEntityWithSpec(hbSpec);

                    if (current == null) return;

                    // Only heartbeat while actively syncing
                    if (current.Status != SyncStatus.SYNCING) return;

                    current.LastHeartbeat = DateTime.UtcNow;
                    await UnitOfWork.Complete();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "{LogPrefix} Heartbeat loop failed | SyncId: {SyncId}", LogPrefix, syncId);
                    // keep loop running
                }
            }
        }

        private async Task<Result> UploadFileWithResumeAsync(SyncEntity sync, UserJob job, FileInfo file, string s3Key, CancellationToken cancellationToken)
        {
            try
            {
                // 1) Exists?
                var existsResult = await s3UploadService.CheckObjectExistsAsync(_backblazeSettings.BucketName, s3Key, cancellationToken);
                if (existsResult.IsSuccess && existsResult.Value) return Result.Success();

                // 2) Load/Create progress record
                var existingProgress = await UnitOfWork.Repository<S3SyncProgress>()
                    .GetEntityWithSpec(new BaseSpecification<S3SyncProgress>(p => p.SyncId == sync.Id && p.S3Key == s3Key));

                S3SyncProgress? progress = existingProgress;
                string? uploadId = null;
                List<PartETag>? existingParts = null;

                // 3) Resume logic
                if (progress != null && progress.Status == SyncStatus.SYNCING && !string.IsNullOrEmpty(progress.UploadId))
                {
                    uploadId = progress.UploadId;
                    existingParts = ParsePartETags(progress.PartETags);

                    var listPartsResult = await s3UploadService.ListPartsAsync(_backblazeSettings.BucketName, s3Key, uploadId, cancellationToken);
                    if (listPartsResult.IsFailure)
                    {
                        Logger.LogWarning("{LogPrefix} Remote upload session not found, restarting | Key: {Key}", LogPrefix, s3Key);
                        uploadId = null;
                    }
                    else
                    {
                        existingParts = MergePartETags(existingParts, listPartsResult.Value);
                    }
                }

                // 4) Init if needed
                if (string.IsNullOrEmpty(uploadId))
                {
                    var init = await s3UploadService.InitiateUploadAsync(_backblazeSettings.BucketName, s3Key, file.Length, cancellationToken: cancellationToken);
                    if (init.IsFailure) return Result.Failure(init.Error.Code, init.Error.Message);

                    uploadId = init.Value;
                    var totalParts = (int)Math.Ceiling((double)file.Length / PartSize);

                    progress = new S3SyncProgress
                    {
                        JobId = job.Id,
                        SyncId = sync.Id,
                        LocalFilePath = file.FullName,
                        S3Key = s3Key,
                        UploadId = uploadId,
                        PartSize = PartSize,
                        TotalParts = totalParts,
                        Status = SyncStatus.SYNCING,
                        StartedAt = DateTime.UtcNow,
                        TotalBytes = file.Length
                    };

                    UnitOfWork.Repository<S3SyncProgress>().Add(progress);
                    await UnitOfWork.Complete();

                    existingParts = [];
                }
                else
                {
                    progress!.UpdatedAt = DateTime.UtcNow;
                    await UnitOfWork.Complete();
                    existingParts ??= [];
                }

                // 5) Upload loop (memory optimized)
                await using var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

                var startPartNumber = existingParts.Count > 0
                    ? existingParts.Max(p => p.PartNumber) + 1
                    : 1;

                var buffer = ArrayPool<byte>.Shared.Rent((int)PartSize);

                try
                {
                    for (int partNumber = startPartNumber; partNumber <= progress.TotalParts; partNumber++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var partStart = (partNumber - 1) * PartSize;
                        var currentPartSize = (int)Math.Min(PartSize, file.Length - partStart);

                        fileStream.Seek(partStart, SeekOrigin.Begin);
                        var bytesRead = await fileStream.ReadAsync(buffer, 0, currentPartSize, cancellationToken);

                        if (bytesRead != currentPartSize)
                            return Result.Failure("READ_ERROR", $"Read mismatch part {partNumber}");

                        using var partStream = new MemoryStream(buffer, 0, bytesRead);

                        var uploadPart = await s3UploadService.UploadPartAsync(
                            _backblazeSettings.BucketName, s3Key, uploadId, partNumber, partStream, cancellationToken);

                        if (uploadPart.IsFailure) return Result.Failure(uploadPart.Error.Code, uploadPart.Error.Message);

                        existingParts.Add(uploadPart.Value);

                        progress.PartsCompleted = existingParts.Count;
                        progress.BytesUploaded = Math.Min(progress.PartsCompleted * PartSize, file.Length);
                        progress.PartETags = SerializePartETags(existingParts);
                        progress.UpdatedAt = DateTime.UtcNow;

                        await UnitOfWork.Complete();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                // 6) Complete
                var comp = await s3UploadService.CompleteUploadAsync(_backblazeSettings.BucketName, s3Key, uploadId, existingParts, cancellationToken);
                if (comp.IsFailure) return Result.Failure(comp.Error.Code, comp.Error.Message);

                progress.Status = SyncStatus.COMPLETED;
                progress.CompletedAt = DateTime.UtcNow;

                UnitOfWork.Repository<S3SyncProgress>().Delete(progress);
                await UnitOfWork.Complete();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure("UPLOAD_ERROR", ex.Message);
            }
        }

        // Helpers
        private List<PartETag> MergePartETags(List<PartETag>? stored, List<PartETag> s3Parts)
        {
            var merged = new Dictionary<int, PartETag>();
            foreach (var p in s3Parts) merged[p.PartNumber] = p;

            if (stored != null)
            {
                foreach (var p in stored)
                {
                    if (!merged.ContainsKey(p.PartNumber)) merged[p.PartNumber] = p;
                }
            }

            return merged.Values.OrderBy(p => p.PartNumber).ToList();
        }

        private FileInfo[] GetFilesToUpload(string downloadPath)
        {
            try
            {
                var dir = new DirectoryInfo(downloadPath);
                if (!dir.Exists) return [];

                return dir.GetFiles("*", SearchOption.AllDirectories)
                    .Where(f =>
                        !f.Name.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.EndsWith(".dht", StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.EndsWith(".fresume", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            catch
            {
                return [];
            }
        }

        private Task CleanupBlockStorageAsync(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                Logger.LogInformation("{LogPrefix} Deleted local files | Path: {Path}", LogPrefix, path);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{LogPrefix} Cleanup failed (non-critical) | Path: {Path}", LogPrefix, path);
            }

            return Task.CompletedTask;
        }

        // Serialization helpers
        private List<PartETag> ParsePartETags(string json)
        {
            try { return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<PartETag>>(json) ?? []; }
            catch { return []; }
        }

        private string SerializePartETags(List<PartETag> parts) => JsonSerializer.Serialize(parts);
    }
}
