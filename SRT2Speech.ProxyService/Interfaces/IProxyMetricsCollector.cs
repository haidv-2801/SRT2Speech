using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Interfaces;

/// <summary>
/// Interface cho metrics collection
/// </summary>
public interface IProxyMetricsCollector
{
    /// <summary>
    /// Ghi nhận một request
    /// </summary>
    /// <param name="proxyId">ID của proxy</param>
    /// <param name="success">Request có thành công không</param>
    /// <param name="responseTime">Thời gian response</param>
    void RecordRequest(string proxyId, bool success, TimeSpan responseTime);

    /// <summary>
    /// Lấy metrics của một proxy
    /// </summary>
    /// <param name="proxyId">ID của proxy</param>
    /// <returns>ProxyMetrics hoặc null nếu không tìm thấy</returns>
    ProxyMetrics? GetMetrics(string proxyId);

    /// <summary>
    /// Lấy tất cả metrics
    /// </summary>
    /// <returns>Dictionary với key là proxyId và value là ProxyMetrics</returns>
    Dictionary<string, ProxyMetrics> GetAllMetrics();

    /// <summary>
    /// Reset metrics của một proxy
    /// </summary>
    /// <param name="proxyId">ID của proxy</param>
    void ResetMetrics(string proxyId);

    /// <summary>
    /// Reset tất cả metrics
    /// </summary>
    void ResetAllMetrics();
}