using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Interfaces;

/// <summary>
/// Interface cho proxy manager - điểm trung tâm để quản lý proxy
/// </summary>
public interface IProxyManager
{
    /// <summary>
    /// Lấy proxy tiếp theo dựa trên strategy
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ProxyInfo hoặc null nếu không có proxy nào available</returns>
    Task<ProxyInfo?> GetNextProxyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy HttpClient đã được cấu hình với proxy
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HttpClient với proxy được cấu hình</returns>
    Task<HttpClient> GetHttpClientWithProxyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu proxy thành công
    /// </summary>
    /// <param name="proxyId">ID của proxy</param>
    /// <param name="responseTime">Thời gian response</param>
    Task MarkProxySuccessAsync(string proxyId, TimeSpan responseTime);

    /// <summary>
    /// Đánh dấu proxy thất bại
    /// </summary>
    /// <param name="proxyId">ID của proxy</param>
    /// <param name="reason">Lý do thất bại</param>
    Task MarkProxyFailureAsync(string proxyId, string reason);

    /// <summary>
    /// Lấy danh sách tất cả proxy
    /// </summary>
    /// <returns>Danh sách tất cả proxy</returns>
    Task<IEnumerable<ProxyInfo>> GetAllProxiesAsync();

    /// <summary>
    /// Lấy danh sách proxy healthy
    /// </summary>
    /// <returns>Danh sách proxy healthy</returns>
    Task<IEnumerable<ProxyInfo>> GetHealthyProxiesAsync();

    /// <summary>
    /// Lấy metrics tổng hợp
    /// </summary>
    /// <returns>ProxyMetrics tổng hợp</returns>
    Task<ProxyMetrics> GetAggregatedMetricsAsync();

    /// <summary>
    /// Lấy status summary
    /// </summary>
    /// <returns>Chuỗi mô tả status</returns>
    string GetStatusSummary();

    /// <summary>
    /// Reload configuration từ file
    /// </summary>
    Task ReloadConfigurationAsync();

    /// <summary>
    /// Nhập khẩu danh sách proxy binding dạng ip:port:username:password và replace toàn bộ Proxies trong file cấu hình.
    /// Trả về số lượng proxy hợp lệ đã import.
    /// </summary>
    Task<int> ImportBindingsAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default);
}