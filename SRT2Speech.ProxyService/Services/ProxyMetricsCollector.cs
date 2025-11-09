using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Services;

/// <summary>
/// Service để thu thập và quản lý metrics của proxy
/// </summary>
public class ProxyMetricsCollector : IProxyMetricsCollector
{
    private readonly ConcurrentDictionary<string, ProxyMetrics> _metrics = new();
    private readonly ConcurrentDictionary<string, List<double>> _responseTimes = new();
    private readonly ILogger<ProxyMetricsCollector> _logger;

    public ProxyMetricsCollector(ILogger<ProxyMetricsCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void RecordRequest(string proxyId, bool success, TimeSpan responseTime)
    {
        if (string.IsNullOrEmpty(proxyId))
            throw new ArgumentNullException(nameof(proxyId));

        var metrics = _metrics.GetOrAdd(proxyId, id => new ProxyMetrics
        {
            ProxyId = id,
            FirstUsed = DateTime.UtcNow,
            MinResponseTime = double.MaxValue
        });

        // Update counters
        metrics.TotalRequests++;
        if (success)
        {
            metrics.SuccessfulRequests++;
        }
        else
        {
            metrics.FailedRequests++;
        }

        // Update response time metrics
        if (success && responseTime.TotalMilliseconds > 0)
        {
            var responseMs = responseTime.TotalMilliseconds;

            // Update min/max
            if (responseMs < metrics.MinResponseTime)
                metrics.MinResponseTime = responseMs;
            
            if (responseMs > metrics.MaxResponseTime)
                metrics.MaxResponseTime = responseMs;

            // Update average (rolling average)
            if (metrics.AverageResponseTime == 0)
            {
                metrics.AverageResponseTime = responseMs;
            }
            else
            {
                metrics.AverageResponseTime = 
                    (metrics.AverageResponseTime * 0.8) + (responseMs * 0.2);
            }

            // Store for P95 calculation
            var times = _responseTimes.GetOrAdd(proxyId, _ => new List<double>());
            lock (times)
            {
                times.Add(responseMs);
                
                // Keep only last 100 samples
                if (times.Count > 100)
                {
                    times.RemoveAt(0);
                }

                // Calculate P95
                metrics.P95ResponseTime = CalculatePercentile(times, 0.95);
            }
        }

        metrics.LastUsed = DateTime.UtcNow;

        _logger.LogDebug("Ghi nhận request cho proxy {ProxyId}: Success={Success}, ResponseTime={ResponseTime}ms",
            proxyId, success, responseTime.TotalMilliseconds);
    }

    /// <inheritdoc/>
    public ProxyMetrics? GetMetrics(string proxyId)
    {
        if (string.IsNullOrEmpty(proxyId))
            throw new ArgumentNullException(nameof(proxyId));

        _metrics.TryGetValue(proxyId, out var metrics);
        return metrics;
    }

    /// <inheritdoc/>
    public Dictionary<string, ProxyMetrics> GetAllMetrics()
    {
        return new Dictionary<string, ProxyMetrics>(_metrics);
    }

    /// <inheritdoc/>
    public void ResetMetrics(string proxyId)
    {
        if (string.IsNullOrEmpty(proxyId))
            throw new ArgumentNullException(nameof(proxyId));

        _metrics.TryRemove(proxyId, out _);
        _responseTimes.TryRemove(proxyId, out _);

        _logger.LogInformation("Đã reset metrics cho proxy {ProxyId}", proxyId);
    }

    /// <inheritdoc/>
    public void ResetAllMetrics()
    {
        var count = _metrics.Count;
        _metrics.Clear();
        _responseTimes.Clear();

        _logger.LogInformation("Đã reset tất cả metrics ({Count} proxy)", count);
    }

    /// <summary>
    /// Tính percentile từ danh sách giá trị
    /// </summary>
    private double CalculatePercentile(List<double> values, double percentile)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(sorted.Count - 1, index));

        return sorted[index];
    }
}