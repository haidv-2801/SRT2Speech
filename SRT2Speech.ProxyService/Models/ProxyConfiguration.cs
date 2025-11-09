namespace SRT2Speech.ProxyService.Models;

/// <summary>
/// Configuration root cho proxy service (load từ YAML)
/// </summary>
public class ProxyConfiguration
{
    /// <summary>
    /// Danh sách các proxy
    /// </summary>
    public List<ProxyInfo> Proxies { get; set; } = new();

    /// <summary>
    /// Settings cho proxy service
    /// </summary>
    public ProxyServiceSettings Settings { get; set; } = new();

    /// <summary>
    /// Thời điểm load configuration
    /// </summary>
    public DateTime LoadedAt { get; set; }

    /// <summary>
    /// Version của configuration
    /// </summary>
    public string Version { get; set; } = "1.0";
}