using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Strategies;

/// <summary>
/// Smart strategy - chọn proxy dựa trên multiple factors:
/// - Health score
/// - Response time
/// - Success rate
/// - Recent performance
/// </summary>
public class SmartStrategy : IRotationStrategy
{
    private readonly IProxyMetricsCollector _metricsCollector;

    public SmartStrategy(IProxyMetricsCollector metricsCollector)
    {
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
    }

    /// <inheritdoc/>
    public string Name => "Smart";

    /// <inheritdoc/>
    public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
    {
        var proxies = availableProxies.ToList();
        if (!proxies.Any()) return null;

        // Calculate composite score cho mỗi proxy
        var scoredProxies = proxies.Select(p =>
        {
            var metrics = _metricsCollector.GetMetrics(p.Id);
            var score = CalculateProxyScore(p, metrics);
            return new { Proxy = p, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        return scoredProxies.First().Proxy;
    }

    /// <summary>
    /// Tính composite score cho proxy
    /// </summary>
    private double CalculateProxyScore(ProxyInfo proxy, ProxyMetrics? metrics)
    {
        double score = 0;

        // Health score (40% weight)
        score += proxy.Health.HealthScore * 0.4;

        // Success rate (30% weight)
        score += proxy.Health.SuccessRate * 0.3;

        // Response time (20% weight) - faster is better
        if (metrics != null && metrics.AverageResponseTime > 0)
        {
            var responseScore = Math.Max(0, 100 - (metrics.AverageResponseTime / 100));
            score += responseScore * 0.2;
        }
        else
        {
            score += 50 * 0.2; // Default score
        }

        // Usage balance (10% weight) - less used is better
        var usageScore = Math.Max(0, 100 - proxy.UsedCount);
        score += usageScore * 0.1;

        return score;
    }
}