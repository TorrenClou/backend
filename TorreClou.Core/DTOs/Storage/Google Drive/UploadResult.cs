namespace TorreClou.GoogleDrive.Worker.Services
{


    public sealed class UploadResult
    {
        public int TotalFiles;
        public int FailedFiles;

        /// <summary>
        /// Last exception thrown while uploading a file. Used to classify the destination's
        /// health so the next attempt can be routed to a different drive.
        /// </summary>
        public Exception? LastError;

        public bool AllFilesUploaded => TotalFiles > 0 && FailedFiles == 0;
    }


}
