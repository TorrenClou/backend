using System.Text.Json.Serialization;

namespace TorreClou.Core.DTOs.Payments
{
    // Generic response wrapper
    public class CoinremitterResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    // Create Invoice Request
    public class CreateInvoiceRequest
    {
        [JsonPropertyName("amount")]
        public string Amount { get; set; } = string.Empty;

        [JsonPropertyName("notify_url")]
        public string? NotifyUrl { get; set; }

        [JsonPropertyName("success_url")]
        public string? SuccessUrl { get; set; }

        [JsonPropertyName("fail_url")]
        public string? FailUrl { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("custom_data1")]
        public string? CustomData1 { get; set; } // Used to store deposit ID

        [JsonPropertyName("custom_data2")]
        public string? CustomData2 { get; set; }

        [JsonPropertyName("expiry_time_in_minutes")]
        public int? ExpiryTimeInMinutes { get; set; }
    }

    // Create Invoice Response Data
    public class CreateInvoiceData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("invoice_id")]
        public string? InvoiceId { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("total_amount")]
        public Dictionary<string, string>? TotalAmount { get; set; }

        [JsonPropertyName("paid_amount")]
        public Dictionary<string, string>? PaidAmount { get; set; }

        [JsonPropertyName("usd_amount")]
        public string? UsdAmount { get; set; }

        [JsonPropertyName("amount")]
        public string? Amount { get; set; }

        [JsonPropertyName("coin")]
        public string? Coin { get; set; }

        [JsonPropertyName("coin_symbol")]
        public string? CoinSymbol { get; set; }

        [JsonPropertyName("wallet_name")]
        public string? WalletName { get; set; }

        [JsonPropertyName("wallet_id")]
        public string? WalletId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("status_code")]
        public int? StatusCode { get; set; }

        [JsonPropertyName("expire_on")]
        public string? ExpireOn { get; set; }

        [JsonPropertyName("expire_on_timestamp")]
        public long? ExpireOnTimestamp { get; set; }

        [JsonPropertyName("invoice_date")]
        public string? InvoiceDate { get; set; }

        [JsonPropertyName("invoice_timestamp")]
        public long? InvoiceTimestamp { get; set; }

        [JsonPropertyName("custom_data1")]
        public string? CustomData1 { get; set; }

        [JsonPropertyName("custom_data2")]
        public string? CustomData2 { get; set; }
    }

    // Get Invoice Request
    public class GetInvoiceRequest
    {
        [JsonPropertyName("invoice_id")]
        public string InvoiceId { get; set; } = string.Empty;
    }

    // Get Invoice Response Data (extends CreateInvoiceData with payment history)
    public class GetInvoiceData : CreateInvoiceData
    {
        [JsonPropertyName("payment_history")]
        public List<PaymentHistoryItem>? PaymentHistory { get; set; }
    }

    public class PaymentHistoryItem
    {
        [JsonPropertyName("txid")]
        public string? Txid { get; set; }

        [JsonPropertyName("explorer_url")]
        public string? ExplorerUrl { get; set; }

        [JsonPropertyName("amount")]
        public string? Amount { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("confirmation")]
        public int? Confirmation { get; set; }

        [JsonPropertyName("required_confirmations")]
        public int? RequiredConfirmations { get; set; }
    }

    // Coinremitter Webhook DTO
    // Webhook is sent when invoice status changes
    public class CoinremitterWebhookDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("invoice_id")]
        public string? InvoiceId { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("total_amount")]
        public Dictionary<string, string>? TotalAmount { get; set; }

        [JsonPropertyName("paid_amount")]
        public Dictionary<string, string>? PaidAmount { get; set; }

        [JsonPropertyName("usd_amount")]
        public string? UsdAmount { get; set; }

        [JsonPropertyName("amount")]
        public string? Amount { get; set; }

        [JsonPropertyName("coin")]
        public string? Coin { get; set; }

        [JsonPropertyName("coin_symbol")]
        public string? CoinSymbol { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("status_code")]
        public int? StatusCode { get; set; }

        [JsonPropertyName("custom_data1")]
        public string? CustomData1 { get; set; } // Contains deposit ID

        [JsonPropertyName("custom_data2")]
        public string? CustomData2 { get; set; }
    }

    // Invoice Data - Used by IPaymentGateway interface for payment verification
    public class InvoiceData
    {
        public string? id { get; set; }
        public string? invoice_id { get; set; }
        public string? url { get; set; }
        public string? status { get; set; }
        public int? status_code { get; set; } // 0=Pending, 1=Paid, 2=Under Paid, 3=Over Paid, 4=Expired, 5=Cancelled
        public string? coin { get; set; }
        public Dictionary<string, string>? total_amount { get; set; }
    }

    // Stablecoin minimum amount DTO for API response
    public class StablecoinMinAmountDto
    {
        public string Currency { get; set; } = string.Empty;
        public decimal MinAmount { get; set; }
        public string? FiatEquivalent { get; set; }
    }
}

