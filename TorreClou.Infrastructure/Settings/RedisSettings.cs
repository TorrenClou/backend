namespace TorreClou.Infrastructure.Settings
{
    public class RedisSettings
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public string JobChannel { get; set; } = "jobs:new";
    }
}

