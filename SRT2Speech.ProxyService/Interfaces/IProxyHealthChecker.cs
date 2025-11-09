using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Interfaces;

/// <summary>
/// Interface cho health checking service
/// </summary>
public interface IProxyHealthChecker
{
    /// <summary>
    /// Kiểm tra health của một proxy
    /// </summary>
    /// <param name="proxy">Proxy cần kiểm tra</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True nếu proxy healthy, false nếu không</returns>
    Task<bool> CheckProxyHealthAsync(ProxyInfo proxy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra health của tất cả proxy
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CheckAllProxiesHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start background health checking
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop background health checking
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}