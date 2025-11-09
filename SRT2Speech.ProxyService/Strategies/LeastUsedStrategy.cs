using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Strategies;

/// <summary>
/// Least used rotation strategy - chọn proxy ít được sử dụng nhất
/// </summary>
public class LeastUsedStrategy : IRotationStrategy
{
    /// <inheritdoc/>
    public string Name => "LeastUsed";

    /// <inheritdoc/>
    public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
    {
        return availableProxies
            .OrderBy(p => p.UsedCount)
            .ThenByDescending(p => p.Health.HealthScore)
            .FirstOrDefault();
    }
}