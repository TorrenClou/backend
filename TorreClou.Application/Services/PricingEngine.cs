using TorreClou.Core.Enums;
using TorreClou.Core.Interfaces;
using TorreClou.Core.Models.Pricing;

namespace TorreClou.Application.Services
{

    public class PricingEngine : IPricingEngine
    {
        private const decimal BASE_RATE_PER_GB = 0.05m;
        private const decimal MINIMUM_CHARGE = 0.20m;

        public PricingSnapshot CalculatePrice(long sizeBytes, RegionCode region, double healthMultiplier, bool isCached = false)
        {
            var snapshot = new PricingSnapshot
            {
                BaseRatePerGb = BASE_RATE_PER_GB,
                UserRegion = region.ToString(),
                HealthMultiplier = healthMultiplier,
                IsCacheHit = isCached
            };

            // 1. حساب الحجم بالجيجا
            double sizeInGb = sizeBytes / (1024.0 * 1024.0 * 1024.0);
            if (sizeInGb < 0.1) sizeInGb = 0.1;

            // 2. معامل المنطقة (Region Multiplier)
            snapshot.RegionMultiplier = GetRegionMultiplier(region);

           

            // --- المعادلة الأساسية ---
            // Price = (Size * Rate * Region) * Health
            decimal rawPrice = (decimal)sizeInGb * BASE_RATE_PER_GB * (decimal)snapshot.RegionMultiplier;

            // تطبيق معامل الصحة
            rawPrice *= (decimal)snapshot.HealthMultiplier;

            // 4. خصم الكاش (Cache Hit)
            if (isCached)
            {
                // لو موجود، خصم 50% (أو يدفع تكلفة الـ Upload فقط)
                decimal discount = rawPrice * 0.50m;
                snapshot.CacheDiscountAmount = discount;
                rawPrice -= discount;
            }

            // التأكد من الحد الأدنى
            snapshot.FinalPrice = Math.Max(rawPrice, MINIMUM_CHARGE);

            // تقريب لأقرب رقمين عشريين
            snapshot.FinalPrice = Math.Round(snapshot.FinalPrice, 2);

            return snapshot;
        }

        private double GetRegionMultiplier(RegionCode region)
        {
            return region switch
            {
                RegionCode.EG => 0.4, // مصر خصم 60%
                RegionCode.IN => 0.4, // الهند
                RegionCode.SA => 0.8, // السعودية
                RegionCode.US or RegionCode.EU => 1.0, // أمريكا وأوروبا سعر كامل
                _ => 1.0
            };
        }

      
    }
}