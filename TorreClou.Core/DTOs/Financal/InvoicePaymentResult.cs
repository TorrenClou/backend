namespace TorreClou.Core.DTOs.Financal
{
    public class InvoicePaymentResult
    {
        public int WalletTransaction { get; set; }
        public int InvoiceId { get; set; }
        public int JobId { get; set; }
        public decimal TotalAmountInNCurruncy { get; set; }
        public bool HasStorageProfileWarning { get; set; }
        public string? StorageProfileWarningMessage { get; set; }
    }
}
