namespace TorreClou.Core.DTOs.Storage
{
    public class StorageProfileDto
    {
        public int Id { get; set; }
        public string ProfileName { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

