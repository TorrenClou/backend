namespace TorreClou.Core.Enums
{
    public enum RegionCode { Global, US, EU, EG, SA, IN }

    public enum UserRole { User, Admin, Support , Suspended, Banned}

    public enum StorageProviderType { GoogleDrive, OneDrive, AwsS3, Dropbox }

    public enum TransactionType { DEPOSIT, PAYMENT, REFUND, ADMIN_ADJUSTMENT, BONUS,
        DEDUCTION
    }

    public enum FileStatus { PENDING, DOWNLOADING, READY, CORRUPTED, DELETED }

    public enum DiscountType
    {
        Percentage,
        FixedAmount
    }
    public enum DepositStatus
    {
        Pending,   // اليوزر لسه فاتح صفحة الدفع
        Completed, // الفلوس وصلت وتأكدت
        Failed,    // الفيزا اترفضا
        Expired    // اللينك مدته انتهت
    }
    public enum SyncStatus
    {
        SYNC_RETRY,
        SYNCING,
        FAILED,
        COMPLETED,
        PENDING
    }
    public enum JobStatus 
    { 
        QUEUED, 
        DOWNLOADING, 
        PENDING_UPLOAD, 
        UPLOADING, 
        TORRENT_DOWNLOAD_RETRY, 
        UPLOAD_RETRY, 
        COMPLETED, 
        FAILED, 
        CANCELLED, 
        TORRENT_FAILED, 
        UPLOAD_FAILED, 
        GOOGLE_DRIVE_FAILED 
    }

    public enum JobType { Torrent,  Sync}

    public enum ViolationType
    {
        Spam,
        Abuse,
        TermsViolation,
        CopyrightInfringement,
        Other
    }

    /// <summary>
    /// Identifies the source that triggered a job/sync status change.
    /// </summary>
    public enum StatusChangeSource
    {
        /// <summary>Worker process changed the status during job execution.</summary>
        Worker,
        /// <summary>User action triggered the status change (e.g., cancellation).</summary>
        User,
        /// <summary>System/API triggered the status change (e.g., job creation).</summary>
        System,
        /// <summary>Recovery process changed the status (e.g., recovering stuck jobs).</summary>
        Recovery
    }
}