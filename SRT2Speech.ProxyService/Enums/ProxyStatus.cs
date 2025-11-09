namespace SRT2Speech.ProxyService.Enums;

/// <summary>
/// Trạng thái của proxy
/// </summary>
public enum ProxyStatus
{
    /// <summary>
    /// Proxy đang hoạt động bình thường
    /// </summary>
    Active,

    /// <summary>
    /// Proxy có vấn đề nhưng vẫn có thể retry
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Proxy bị vô hiệu hóa thủ công
    /// </summary>
    Disabled,

    /// <summary>
    /// Đang trong quá trình health check
    /// </summary>
    Testing
}