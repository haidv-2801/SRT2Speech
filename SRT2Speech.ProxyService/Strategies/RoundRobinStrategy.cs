using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Strategies;

/// <summary>
/// Round-robin rotation strategy - xoay vòng tuần tự
/// </summary>
public class RoundRobinStrategy : IRotationStrategy
{
    private int _currentIndex = 0;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public string Name => "RoundRobin";

    /// <inheritdoc/>
    public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
    {
        var proxies = availableProxies.ToList();
        if (!proxies.Any()) return null;

        lock (_lock)
        {
            var selected = proxies[_currentIndex % proxies.Count];
            _currentIndex = (_currentIndex + 1) % proxies.Count;
            return selected;
        }
    }
}