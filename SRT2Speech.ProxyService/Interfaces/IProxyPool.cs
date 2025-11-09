using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Interfaces;

/// <summary>
/// Interface cho proxy pool management
/// </summary>
public interface IProxyPool
{
    /// <summary>
    /// Thêm proxy vào pool
    /// </summary>
    /// <param name="proxy">Proxy cần thêm</param>
    void AddProxy(ProxyInfo proxy);

    /// <summary>
    /// Xóa proxy khỏi pool
    /// </summary>
    /// <param name="proxyId">ID của proxy cần xóa</param>
    /// <returns>True nếu xóa thành công, false nếu không tìm thấy</returns>
    bool RemoveProxy(string proxyId);

    /// <summary>
    /// Lấy proxy theo ID
    /// </summary>
    /// <param name="proxyId">ID của proxy</param>
    /// <returns>ProxyInfo hoặc null nếu không tìm thấy</returns>
    ProxyInfo? GetProxy(string proxyId);

    /// <summary>
    /// Lấy tất cả proxy
    /// </summary>
    /// <returns>Danh sách tất cả proxy</returns>
    IEnumerable<ProxyInfo> GetAllProxies();

    /// <summary>
    /// Lấy proxy available (có thể sử dụng)
    /// </summary>
    /// <returns>Danh sách proxy available</returns>
    IEnumerable<ProxyInfo> GetAvailableProxies();

    /// <summary>
    /// Update proxy info
    /// </summary>
    /// <param name="proxy">Proxy cần update</param>
    void UpdateProxy(ProxyInfo proxy);

    /// <summary>
    /// Clear tất cả proxy
    /// </summary>
    void Clear();

    /// <summary>
    /// Số lượng proxy trong pool
    /// </summary>
    int Count { get; }
}