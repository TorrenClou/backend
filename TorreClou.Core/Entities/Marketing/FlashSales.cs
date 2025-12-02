using TorreClou.Core.Enums;

namespace TorreClou.Core.Entities.Marketing
{
    public class FlashSale : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public decimal DiscountPercentage { get; set; }

        public RegionCode? TargetRegion { get; set; }

        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }

        public bool IsActive { get; set; } = true;
    }
}