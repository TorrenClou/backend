using TorrenClou.Core.DTOs.Common;

namespace TorrenClou.Core.Interfaces
{
    public interface IHealthCheckService
    {
        Task<HealthStatus> GetCachedHealthStatusAsync();
        Task<DetailedHealthStatus> GetDetailedHealthStatusAsync(CancellationToken ct = default);
    }
}
