namespace TorreClou.Infrastructure.Settings
{
    public class CoinremitterSettings
    {
        public string ApiUrl { get; set; } = "https://api.coinremitter.com/v1";
        public string ApiKey { get; set; } = string.Empty;
        public string ApiPassword { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;
        public string CoinSymbol { get; set; } = "TCN"; // TCN for dev, TRX for prod
        public decimal MinimumDepositAmount { get; set; } = 2m; // Minimum deposit in coin units
    }
}

