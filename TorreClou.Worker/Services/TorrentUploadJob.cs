using TorreClou.Core.Entities.Jobs;
using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Specifications;
using Microsoft.Extensions.Logging;
using Hangfire;

namespace TorreClou.Worker.Services
{
    /// <summary>
    /// Hangfire job that handles uploading downloaded torrent files to the user's cloud storage.
    /// This is chained after TorrentDownloadJob completes successfully.
    /// </summary>
    public class TorrentUploadJob : BaseJob<TorrentUploadJob>
    {
        protected override string LogPrefix => "[UPLOAD]";

        public TorrentUploadJob(
            IUnitOfWork unitOfWork,
            ILogger<TorrentUploadJob> logger)
            : base(unitOfWork, logger)
        {
        }

        protected override void ConfigureSpecification(BaseSpecification<UserJob> spec)
        {
            spec.AddInclude(j => j.StorageProfile);
            spec.AddInclude(j => j.User);
        }

        [DisableConcurrentExecution(timeoutInSeconds: 3600)] // 1 hour max for large uploads
        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
        [Queue("torrents")]
        public new async Task ExecuteAsync(int jobId, CancellationToken cancellationToken = default)
        {
            await base.ExecuteAsync(jobId, cancellationToken);
        }

        protected override async Task ExecuteCoreAsync(UserJob job, CancellationToken cancellationToken)
        {
            // 1. Validate job state - must be in UPLOADING status
            if (job.Status != JobStatus.UPLOADING)
            {
                Logger.LogWarning("{LogPrefix} Unexpected job status | JobId: {JobId} | Status: {Status}", 
                    LogPrefix, job.Id, job.Status);
            }

            // 2. Validate download path exists
            if (string.IsNullOrEmpty(job.DownloadPath) || !Directory.Exists(job.DownloadPath))
            {
                await MarkJobFailedAsync(job, "Download path not found. Files may have been deleted.");
                return;
            }

            // 3. Update heartbeat
            await UpdateHeartbeatAsync(job, "Preparing upload...");

            // 4. Get files to upload
            var filesToUpload = GetFilesToUpload(job.DownloadPath);
            if (filesToUpload.Length == 0)
            {
                await MarkJobFailedAsync(job, "No files found in download path.");
                return;
            }

            Logger.LogInformation("{LogPrefix} Found {FileCount} files to upload | JobId: {JobId} | TotalSize: {SizeMB:F2} MB",
                LogPrefix, filesToUpload.Length, job.Id, filesToUpload.Sum(f => f.Length) / (1024.0 * 1024.0));

            // 5. Upload to storage provider
            // TODO: Implement actual upload logic based on job.StorageProfile
            await UploadFilesAsync(job, filesToUpload, cancellationToken);

            // 6. Mark as completed
            job.Status = JobStatus.COMPLETED;
            job.CompletedAt = DateTime.UtcNow;
            job.CurrentState = "Upload completed successfully";
            await UnitOfWork.Complete();

            Logger.LogInformation("{LogPrefix} Job completed successfully | JobId: {JobId}", LogPrefix, job.Id);

            // 7. Optionally cleanup local files
            // CleanupDownloadedFiles(job);
        }

        private FileInfo[] GetFilesToUpload(string downloadPath)
        {
            var directory = new DirectoryInfo(downloadPath);

            // Get all files excluding MonoTorrent metadata files
            return directory.GetFiles("*", SearchOption.AllDirectories)
                .Where(f => !f.Name.EndsWith(".fresume") &&
                           !f.Name.EndsWith(".dht") &&
                           !f.Name.EndsWith(".torrent"))
                .ToArray();
        }

        private async Task UploadFilesAsync(UserJob job, FileInfo[] files, CancellationToken cancellationToken)
        {
            var totalFiles = files.Length;
            var uploadedFiles = 0;

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                uploadedFiles++;
                var progress = (uploadedFiles * 100.0) / totalFiles;

                // Update progress
                job.CurrentState = $"Uploading: {progress:F1}% ({uploadedFiles}/{totalFiles} files)";
                job.LastHeartbeat = DateTime.UtcNow;
                await UnitOfWork.Complete();

                Logger.LogInformation("{LogPrefix} Uploading file | JobId: {JobId} | File: {File} | Progress: {Progress:F1}%",
                    LogPrefix, job.Id, file.Name, progress);

                // TODO: Implement actual upload based on StorageProfile.Provider
                // Example:
                // switch (job.StorageProfile.Provider)
                // {
                //     case StorageProviderType.GoogleDrive:
                //         await _googleDriveService.UploadAsync(file, job.StorageProfile, cancellationToken);
                //         break;
                //     case StorageProviderType.OneDrive:
                //         await _oneDriveService.UploadAsync(file, job.StorageProfile, cancellationToken);
                //         break;
                //     // etc.
                // }

                // Simulate upload delay for now
                await Task.Delay(100, cancellationToken);
            }
        }

        private void CleanupDownloadedFiles(UserJob job)
        {
            if (string.IsNullOrEmpty(job.DownloadPath))
                return;

            try
            {
                if (Directory.Exists(job.DownloadPath))
                {
                    Directory.Delete(job.DownloadPath, recursive: true);
                    Logger.LogInformation("{LogPrefix} Cleaned up download path | JobId: {JobId} | Path: {Path}",
                        LogPrefix, job.Id, job.DownloadPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{LogPrefix} Failed to cleanup download path | JobId: {JobId}", LogPrefix, job.Id);
            }
        }
    }
}
