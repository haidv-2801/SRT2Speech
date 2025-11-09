using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Interfaces;

/// <summary>
/// Interface cho rotation strategy
/// </summary>
public interface IRotationStrategy
{
    /// <summary>
    /// Chọn proxy tiếp theo từ danh sách available proxies
    /// </summary>
    /// <param name="availableProxies">Danh sách proxy available</param>
    /// <returns>Proxy được chọn hoặc null nếu không có proxy nào</returns>
    ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies);

    /// <summary>
    /// Tên của strategy
    /// </summary>
    string Name { get; }
}