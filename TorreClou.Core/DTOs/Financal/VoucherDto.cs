using TorreClou.Core.Enums;

namespace TorreClou.Core.DTOs.Financal
{
    public class VoucherDto
    {
        public string Code { get; set; } = string.Empty;
        public DiscountType Type { get; set; }
        public decimal Value { get; set; }
        public decimal DiscountAmount { get; set; }
    }
}

