namespace TorreClou.Infrastructure.Settings
{
    /// <summary>
    /// Configuration settings for job lease management.
    /// </summary>
    public class JobLeaseSettings
    {
        /// <summary>
        /// Default lease duration in minutes. Default: 5 minutes.
        /// </summary>
        public int LeaseDurationMinutes { get; set; } = 5;

        /// <summary>
        /// Interval in seconds for refreshing the lease during job execution. Default: 30 seconds.
        /// </summary>
        public int LeaseRefreshIntervalSeconds { get; set; } = 30;
    }
}

