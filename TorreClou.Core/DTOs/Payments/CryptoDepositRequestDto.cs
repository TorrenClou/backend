using System.ComponentModel.DataAnnotations;

namespace TorreClou.Core.DTOs.Payments
{
    public record CryptoDepositRequestDto
    {
        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(1, 10000, ErrorMessage = "أقل مبلغ للشحن هو 1 دولار")]
        public decimal Amount { get; init; } // المبلغ بالدولار (USD)

        [Required(ErrorMessage = "يرجى اختيار العملة")]
        public string Currency { get; init; } // USDT, USDC, DAI (stablecoins) + LTC (dev only for testing)
    }
}