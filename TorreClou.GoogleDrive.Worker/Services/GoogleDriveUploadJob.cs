using Microsoft.Extensions.Logging;
using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Interfaces.Hangfire;
using TorreClou.Core.Specifications;
using TorreClou.Infrastructure.Workers;


namespace TorreClou.GoogleDrive.Worker.Services
{

    public partial class GoogleDriveUploadJob(
        IUnitOfWork unitOfWork,
        ILogger<GoogleDriveUploadJob> logger,
        IGoogleDriveJobService googleDriveService,
        IUploadProgressContext progressContext,
        ITransferSpeedMetrics speedMetrics,
        IRedisLockService redisLockService,
        IJobStatusService jobStatusService,
        IUploadRoutingService uploadRoutingService,
        IStorageProfileHealthService storageHealthService,
        IDownloadCleanupService downloadCleanupService) : UserJobBase<GoogleDriveUploadJob>(unitOfWork, logger, jobStatusService), IGoogleDriveUploadJob
    {

        protected override string LogPrefix => "[GOOGLE_DRIVE:UPLOAD]";

        protected override void ConfigureSpecification(BaseSpecification<UserJob> spec)
        {
            spec.AddInclude(j => j.StorageProfile);
            spec.AddInclude(j => j.User);
        }


        public new async Task ExecuteAsync(int jobId, CancellationToken cancellationToken = default)
        {
            await base.ExecuteAsync(jobId, cancellationToken);
        }

        protected override async Task ExecuteCoreAsync(UserJob job, CancellationToken cancellationToken)
        {
            using var distributedLock = await AcquireJobLockAsync(job, cancellationToken);
            if (distributedLock == null) return;

            await HandleStatusTransitionAsync(job);

            if (!await ValidateEnvironmentAsync(job)) return;

            var allFiles = GetFilesToUpload(job.DownloadPath!);
            if (allFiles.Length == 0)
            {
                await MarkJobFailedAsync(job, "No valid files found in download path.");
                return;
            }

            var totalBytes = allFiles.Sum(f => f.Length);
            var uploadStartTime = DateTime.UtcNow;

            // Pick the destination before authenticating: a revoked or full Drive is moved
            // to another healthy account of the same user rather than failing the job.
            if (!await ResolveDestinationAsync(job, totalBytes, cancellationToken)) return;

            var accessToken = await AuthenticateAsync(job, totalBytes, cancellationToken);
            if (accessToken == null) return;

            ConfigureProgressContext(job, totalBytes);

            var rootFolderId = await EnsureRootFolderExistsAsync(job, accessToken, cancellationToken);

            var filesToProcess = await FilterAlreadyUploadedFilesAsync(allFiles, job.DownloadPath!);
            var folderIdMap = await CreateFolderHierarchyAsync([.. filesToProcess], job.DownloadPath!, rootFolderId, accessToken, cancellationToken);
            var result = await UploadFilesAsync(job, [.. filesToProcess], folderIdMap, accessToken, cancellationToken);

            await FinalizeJobAsync(job, result, totalBytes, allFiles.Length, uploadStartTime);
        }

        // --- Step Handlers ---

        /// <summary>
        /// Ensures the job points at a storage profile that can accept the upload,
        /// rerouting to a healthy one when it cannot. Returns false when the job has been
        /// failed because nothing usable is left.
        /// </summary>
        private async Task<bool> ResolveDestinationAsync(UserJob job, long totalBytes, CancellationToken token)
        {
            var route = await uploadRoutingService.ResolveTargetAsync(job, totalBytes, token);

            if (!route.HasTarget)
            {
                // Nothing left to try — a Hangfire retry would hit the same wall.
                await MarkJobFailedAsync(job, route.Message ?? "No healthy storage destination is available for this upload.");
                return false;
            }

            if (route.Rerouted)
            {
                Logger.LogWarning("{LogPrefix} Destination rerouted | JobId: {JobId} | {Message}",
                    LogPrefix, job.Id, route.Message);

                await UpdateHeartbeatAsync(job, $"Switched destination to {route.Target!.ProfileName}");
            }

            return true;
        }

        private async Task<IRedisLock?> AcquireJobLockAsync(UserJob job, CancellationToken token)
        {
            var lockKey = $"gdrive:lock:{job.Id}";
            // FIX: Extended lock time to 2 hours to cover large file uploads
            var lockExpiry = TimeSpan.FromHours(2);

            var distributedLock = await redisLockService.AcquireLockAsync(lockKey, lockExpiry, token);

            if (distributedLock == null)
            {
                Logger.LogWarning("{LogPrefix} Job is already being processed by another instance | JobId: {JobId}",
                    LogPrefix, job.Id);
                return null;
            }

            Logger.LogInformation("{LogPrefix} Acquired Redis lock | JobId: {JobId} | Expiry: {Expiry}",
                LogPrefix, job.Id, lockExpiry);

            return distributedLock;
        }

        private async Task HandleStatusTransitionAsync(UserJob job)
        {
            if (job.Status == JobStatus.PENDING_UPLOAD)
            {
                Logger.LogInformation("{LogPrefix} Job ready for upload, transitioning to UPLOADING | JobId: {JobId}", LogPrefix, job.Id);
                job.CurrentState = "Starting upload...";
                if (job.StartedAt == null) job.StartedAt = DateTime.UtcNow;
                job.LastHeartbeat = DateTime.UtcNow;

                await JobStatusService.TransitionJobStatusAsync(
                    job,
                    JobStatus.UPLOADING,
                    StatusChangeSource.Worker,
                    metadata: new { provider = "GoogleDrive", startedAt = job.StartedAt });
            }
            else if (job.Status == JobStatus.UPLOAD_RETRY)
            {
                Logger.LogInformation("{LogPrefix} Retrying job | JobId: {JobId} | Retry: {NextRetry}", LogPrefix, job.Id, job.NextRetryAt);
                job.CurrentState = "Retrying upload...";
                job.LastHeartbeat = DateTime.UtcNow;

                await JobStatusService.TransitionJobStatusAsync(
                    job,
                    JobStatus.UPLOADING,
                    StatusChangeSource.Worker,
                    metadata: new { provider = "GoogleDrive", retrying = true, previousNextRetry = job.NextRetryAt });
            }
            else if (job.Status == JobStatus.UPLOADING)
            {
                if (job.StartedAt == null)
                {
                    job.StartedAt = DateTime.UtcNow;
                    await UnitOfWork.Complete();
                }
                Logger.LogInformation("{LogPrefix} Resuming job from recovery | JobId: {JobId}", LogPrefix, job.Id);
            }
            else
            {
                // Warn but allow execution (could be manual trigger)
                Logger.LogWarning("{LogPrefix} Unexpected status: {Status} | JobId: {JobId}", LogPrefix, job.Status, job.Id);
            }
        }

        private async Task<bool> ValidateEnvironmentAsync(UserJob job)
        {
            if (string.IsNullOrEmpty(job.DownloadPath) || !Directory.Exists(job.DownloadPath))
            {
                await MarkJobFailedAsync(job, $"Download path not found: {job.DownloadPath}");
                return false;
            }

            if (job.StorageProfile == null || job.StorageProfile.ProviderType != StorageProviderType.GoogleDrive)
            {
                await MarkJobFailedAsync(job, "Invalid storage profile.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets an access token for the job's destination. When the destination rejects the
        /// refresh (revoked token, exhausted quota), fails over to another healthy profile
        /// and tries again. Returns null when the job has already been marked failed.
        /// </summary>
        private async Task<string?> AuthenticateAsync(UserJob job, long totalBytes, CancellationToken token)
        {
            await UpdateHeartbeatAsync(job, "Authenticating...");

            try
            {
                var accessToken = await googleDriveService.GetAccessTokenAsync(job.StorageProfile!, token);

                // Token refresh has already persisted changes, but ensure we save any other job updates
                await UnitOfWork.Complete();
                return accessToken;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{LogPrefix} Authentication failed | JobId: {JobId} | ProfileId: {ProfileId}",
                    LogPrefix, job.Id, job.StorageProfileId);

                var route = await uploadRoutingService.FailoverAsync(job, ex, totalBytes, token);

                if (!route.HasTarget)
                {
                    await MarkJobFailedAsync(job, route.Message ?? ex.Message);
                    return null;
                }

                await UpdateHeartbeatAsync(job, $"Switched destination to {route.Target!.ProfileName}");

                try
                {
                    var accessToken = await googleDriveService.GetAccessTokenAsync(route.Target, token);
                    await UnitOfWork.Complete();
                    return accessToken;
                }
                catch (Exception retryEx)
                {
                    Logger.LogError(retryEx, "{LogPrefix} Authentication failed after reroute | JobId: {JobId} | ProfileId: {ProfileId}",
                        LogPrefix, job.Id, route.Target.Id);

                    await storageHealthService.RecordFailureAsync(route.Target, retryEx, token);
                    await MarkJobFailedAsync(job, $"Could not authenticate with {route.Target.ProfileName}: {retryEx.Message}", hasRetries: true);
                    return null;
                }
            }
        }

        private void ConfigureProgressContext(UserJob job, long totalBytes)
        {
            progressContext.Configure(
                job.Id,
                job.StorageProfileId,
                totalBytes,
                Logger,
                async (stateMessage, percent) =>
                {
                    job.CurrentState = stateMessage;
                    job.BytesUploaded = (long)(totalBytes * percent / 100.0);
                    job.LastHeartbeat = DateTime.UtcNow;
                    await UnitOfWork.Complete();
                });
        }

        private async Task<List<FileInfo>> FilterAlreadyUploadedFilesAsync(FileInfo[] files, string downloadPath)
        {
            var filesToProcess = new List<FileInfo>();

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(downloadPath, file.FullName);
                var completedId = await progressContext.GetCompletedFileAsync(relativePath);

                if (!string.IsNullOrEmpty(completedId))
                {
                    Logger.LogDebug("{LogPrefix} Skipping {File} (Already in Redis)", LogPrefix, file.Name);
                    await progressContext.MarkFileCompletedAsync(file.Name, file.Length);
                }
                else
                {
                    filesToProcess.Add(file);
                }
            }

            return filesToProcess;
        }

        private async Task FinalizeJobAsync(UserJob job, UploadResult result, long totalBytes, int fileCount, DateTime uploadStartTime)
        {
            if (!result.AllFilesUploaded)
            {
                // Score the destination before retrying. If the failure was the drive's fault
                // (revoked token, no space), this demotes it so the Hangfire retry resolves
                // to a different profile instead of hitting the same wall.
                if (job.StorageProfile != null && result.LastError != null)
                {
                    await storageHealthService.RecordFailureAsync(job.StorageProfile, result.LastError);
                }

                await MarkJobFailedAsync(job, $"Failed to upload {result.FailedFiles} of {result.TotalFiles} files.", hasRetries: true);
                return;
            }

            if (job.StorageProfile != null)
            {
                await storageHealthService.RecordSuccessAsync(job.StorageProfile);
            }

            var duration = (DateTime.UtcNow - uploadStartTime).TotalSeconds;
            speedMetrics.RecordUploadComplete(job.Id, job.UserId, "googledrive_upload", totalBytes, duration);

            job.CompletedAt = DateTime.UtcNow;
            job.BytesUploaded = totalBytes;
            job.CurrentState = "Upload completed successfully";
            job.NextRetryAt = null;

            await JobStatusService.TransitionJobStatusAsync(
                job,
                JobStatus.COMPLETED,
                StatusChangeSource.Worker,
                metadata: new { totalBytes, filesCount = fileCount, durationSeconds = duration, completedAt = job.CompletedAt });

            Logger.LogInformation("{LogPrefix} Completed successfully | JobId: {JobId}", LogPrefix, job.Id);

            // Every file is now in Drive, so the local copy is dead weight on the
            // shared downloads volume. Runs only after COMPLETED so a failed or
            // retrying job keeps the files it still needs.
            await downloadCleanupService.CleanupAfterUploadAsync(job, CancellationToken.None);
        }

        // --- Failure Hook ---

        protected override async Task MarkJobFailedAsync(UserJob job, string errorMessage, bool hasRetries = false)
        {
            try
            {
                // Delete Redis lock before marking as failed
                await googleDriveService.DeleteUploadLockAsync(job.Id);
            }
            catch (Exception ex)
            {
                // Log but don't fail - lock might not exist or already expired
                Logger.LogWarning(ex, "{LogPrefix} Failed to delete lock on job failure | JobId: {JobId}", LogPrefix, job.Id);
            }
            finally
            {
                await base.MarkJobFailedAsync(job, errorMessage, hasRetries);
            }
        }

        // --- Helper Methods ---

        private FileInfo[] GetFilesToUpload(string downloadPath)
        {
            try
            {
                var dir = new DirectoryInfo(downloadPath);
                if (!dir.Exists) return [];

                // FIX: Only filter strictly system files, allow .torrent files if they are part of user content
                return dir.GetFiles("*", SearchOption.AllDirectories)
                    .Where(f =>
                        !f.Name.Equals("dht_nodes.cache", StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.Equals("fastresume", StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.EndsWith(".fresume", StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.EndsWith(".dht", StringComparison.OrdinalIgnoreCase)
                    )
                    .ToArray();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{LogPrefix} Error listing files", LogPrefix);
                return [];
            }
        }

        private async Task<string> EnsureRootFolderExistsAsync(UserJob job, string accessToken, CancellationToken token)
        {
            await UpdateHeartbeatAsync(job, "Checking Google Drive folder...");

            var rootId = await progressContext.GetRootFolderIdAsync(job.Id);
            if (!string.IsNullOrEmpty(rootId)) return rootId;

            var folderName = $"Torrent_{job.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var newRootId = await googleDriveService.CreateFolderAsync(folderName, null, accessToken, token);

            await progressContext.SetRootFolderIdAsync(job.Id, newRootId);
            return newRootId;
        }

        private async Task<Dictionary<string, string>> CreateFolderHierarchyAsync(
            FileInfo[] files,
            string rootPath,
            string rootId,
            string accessToken,
            CancellationToken token)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = rootId,
                ["."] = rootId
            };

            var uniqueDirs = files
                .Select(f => Path.GetRelativePath(rootPath, f.DirectoryName!))
                .Where(p => p != "." && !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p.Split(Path.DirectorySeparatorChar).Length); // Create parents first

            foreach (var relPath in uniqueDirs)
            {
                if (token.IsCancellationRequested) break;

                var parts = relPath.Split(Path.DirectorySeparatorChar);
                var parentRel = parts.Length > 1 ? string.Join(Path.DirectorySeparatorChar, parts[..^1]) : ".";
                var name = parts[^1];

                var parentId = map.TryGetValue(parentRel, out var pid) ? pid : rootId;

                try
                {
                    // Use FindOrCreateFolderAsync to check for existing folder before creating
                    var folderId = await googleDriveService.FindOrCreateFolderAsync(name, parentId, accessToken, token);
                    map[relPath] = folderId;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "{LogPrefix} Failed to create folder '{Name}', falling back to parent | JobId: {JobId}",
                        LogPrefix, name, files.FirstOrDefault()?.FullName);
                    map[relPath] = parentId; // Fallback to parent
                }
            }
            return map;
        }

        private async Task<UploadResult> UploadFilesAsync(
            UserJob job,
            FileInfo[] files,
            Dictionary<string, string> folderMap,
            string accessToken,
            CancellationToken token)
        {
            var result = new UploadResult { TotalFiles = files.Length };

            foreach (var file in files)
            {
                if (token.IsCancellationRequested) break;

                var relDir = Path.GetRelativePath(job.DownloadPath!, file.DirectoryName!);
                var relPath = Path.GetRelativePath(job.DownloadPath!, file.FullName);
                var folderId = folderMap.TryGetValue(relDir, out var fid) ? fid : folderMap["."];

                // Check Drive First (Fallback if Redis was flushed) — non-fatal
                try
                {
                    var existingFileId = await googleDriveService.CheckFileExistsAsync(folderId, file.Name, accessToken, token);
                    if (!string.IsNullOrEmpty(existingFileId))
                    {
                        await progressContext.SetCompletedFileAsync(relPath, existingFileId);
                        await progressContext.MarkFileCompletedAsync(file.Name, file.Length);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "{LogPrefix} CheckFileExists failed for {File}, proceeding with upload", LogPrefix, file.Name);
                }

                // Upload
                try
                {
                    var fileId = await googleDriveService.UploadFileAsync(
                        file.FullName, file.Name, folderId, accessToken, relPath, token);

                    await progressContext.SetCompletedFileAsync(relPath, fileId);
                    await progressContext.MarkFileCompletedAsync(file.Name, file.Length);
                }
                catch (Exception ex)
                {
                    result.FailedFiles++;
                    result.LastError = ex;
                    Logger.LogCritical(ex, "{LogPrefix} Upload failed for {File}", LogPrefix, file.Name);

                    // Recover partial progress — non-fatal
                    try
                    {
                        var resumeUri = await progressContext.GetResumeUriAsync(relPath);
                        if (!string.IsNullOrEmpty(resumeUri))
                        {
                            var resumedBytes = await googleDriveService.QueryUploadStatusAsync(resumeUri, file.Length, accessToken, token);
                            await progressContext.MarkBytesCompletedAsync(file.Name, resumedBytes);
                        }
                    }
                    catch (Exception resumeEx)
                    {
                        Logger.LogWarning(resumeEx, "{LogPrefix} Failed to recover partial progress for {File}", LogPrefix, file.Name);
                    }
                }
            }
            return result;
        }


    }
}
