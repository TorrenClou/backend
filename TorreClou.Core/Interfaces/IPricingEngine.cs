using TorreClou.Core.Enums;
using TorreClou.Core.Models.Pricing;

namespace TorreClou.Core.Interfaces
{
    public interface IPricingEngine
    {
        PricingSnapshot CalculatePrice(long sizeBytes, RegionCode region, double muliplayer, bool isCached = false);
    }
}