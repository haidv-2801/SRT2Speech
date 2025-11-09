using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Strategies;

/// <summary>
/// Random rotation strategy - chọn ngẫu nhiên
/// </summary>
public class RandomStrategy : IRotationStrategy
{
    private readonly Random _random = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public string Name => "Random";

    /// <inheritdoc/>
    public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
    {
        var proxies = availableProxies.ToList();
        if (!proxies.Any()) return null;

        lock (_lock)
        {
            var index = _random.Next(proxies.Count);
            return proxies[index];
        }
    }
}