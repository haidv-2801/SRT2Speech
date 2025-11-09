using SRT2Speech.ProxyService.Enums;

namespace SRT2Speech.ProxyService.Models;

/// <summary>
/// State của proxy service để persist
/// </summary>
public class ProxyState
{
    /// <summary>
    /// Danh sách proxy với state hiện tại
    /// </summary>
    public List<ProxyInfo> Proxies { get; set; } = new();

    /// <summary>
    /// Thời điểm save state cuối cùng
    /// </summary>
    public DateTime LastSaved { get; set; }

    /// <summary>
    /// Strategy đang sử dụng
    /// </summary>
    public RotationStrategy Strategy { get; set; }

    /// <summary>
    /// Metrics của tất cả proxy
    /// </summary>
    public Dictionary<string, ProxyMetrics> Metrics { get; set; } = new();
}