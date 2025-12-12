namespace TorreClou.Core.Options
{
    public class GoogleDriveSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = Array.Empty<string>();
    }
}

