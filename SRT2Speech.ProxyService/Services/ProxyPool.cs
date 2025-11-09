using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Services;

/// <summary>
/// Thread-safe proxy pool management
/// </summary>
public class ProxyPool : IProxyPool
{
    private readonly ConcurrentDictionary<string, ProxyInfo> _proxies = new();
    private readonly ILogger<ProxyPool> _logger;

    public ProxyPool(ILogger<ProxyPool> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void AddProxy(ProxyInfo proxy)
    {
        if (proxy == null) throw new ArgumentNullException(nameof(proxy));

        if (_proxies.TryAdd(proxy.Id, proxy))
        {
            _logger.LogInformation("Đã thêm proxy {ProxyId} ({Host}:{Port}) vào pool",
                proxy.Id, proxy.Host, proxy.Port);
        }
        else
        {
            _logger.LogWarning("Proxy {ProxyId} đã tồn tại trong pool", proxy.Id);
        }
    }

    /// <inheritdoc/>
    public bool RemoveProxy(string proxyId)
    {
        if (string.IsNullOrEmpty(proxyId))
            throw new ArgumentNullException(nameof(proxyId));

        if (_proxies.TryRemove(proxyId, out var proxy))
        {
            _logger.LogInformation("Đã xóa proxy {ProxyId} ({Host}:{Port}) khỏi pool",
                proxy.Id, proxy.Host, proxy.Port);
            return true;
        }

        _logger.LogWarning("Không tìm thấy proxy {ProxyId} để xóa", proxyId);
        return false;
    }

    /// <inheritdoc/>
    public ProxyInfo? GetProxy(string proxyId)
    {
        if (string.IsNullOrEmpty(proxyId))
            throw new ArgumentNullException(nameof(proxyId));

        _proxies.TryGetValue(proxyId, out var proxy);
        return proxy;
    }

    /// <inheritdoc/>
    public IEnumerable<ProxyInfo> GetAllProxies()
    {
        return _proxies.Values.ToList();
    }

    /// <inheritdoc/>
    public IEnumerable<ProxyInfo> GetAvailableProxies()
    {
        return _proxies.Values
            .Where(p => p.IsAvailable())
            .ToList();
    }

    /// <inheritdoc/>
    public void UpdateProxy(ProxyInfo proxy)
    {
        if (proxy == null) throw new ArgumentNullException(nameof(proxy));

        _proxies.AddOrUpdate(
            proxy.Id,
            proxy,
            (key, oldValue) => proxy);

        _logger.LogDebug("Đã update proxy {ProxyId}", proxy.Id);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        var count = _proxies.Count;
        _proxies.Clear();
        _logger.LogInformation("Đã xóa tất cả {Count} proxy khỏi pool", count);
    }

    /// <inheritdoc/>
    public int Count => _proxies.Count;
}