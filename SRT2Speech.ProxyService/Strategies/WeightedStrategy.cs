using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Strategies;

/// <summary>
/// Weighted rotation strategy - chọn dựa trên trọng số
/// </summary>
public class WeightedStrategy : IRotationStrategy
{
    private readonly Random _random = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public string Name => "Weighted";

    /// <inheritdoc/>
    public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
    {
        var proxies = availableProxies.ToList();
        if (!proxies.Any()) return null;

        // Calculate weights based on health score và usage
        var weights = proxies.Select(p =>
        {
            var baseWeight = p.Weight;
            var healthBonus = p.Health.HealthScore / 100.0;
            var usagePenalty = 1.0 / (p.UsedCount + 1);
            return baseWeight * healthBonus * usagePenalty;
        }).ToList();

        var totalWeight = weights.Sum();
        if (totalWeight <= 0) return proxies.First();

        lock (_lock)
        {
            var randomValue = _random.NextDouble() * totalWeight;
            double cumulativeWeight = 0;

            for (int i = 0; i < proxies.Count; i++)
            {
                cumulativeWeight += weights[i];
                if (randomValue <= cumulativeWeight)
                {
                    return proxies[i];
                }
            }

            return proxies.Last();
        }
    }
}