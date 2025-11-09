using SRT2Speech.ProxyService.Enums;

namespace SRT2Speech.ProxyService.Models;

/// <summary>
/// Thông tin chi tiết của một proxy
/// </summary>
public class ProxyInfo
{
    /// <summary>
    /// ID duy nhất của proxy
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Tên hiển thị của proxy
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Host/IP của proxy
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Port của proxy
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Loại proxy
    /// </summary>
    public ProxyType Type { get; set; } = ProxyType.HTTP;

    /// <summary>
    /// Username để xác thực (nếu có)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password để xác thực (nếu có)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Trạng thái hiện tại của proxy
    /// </summary>
    public ProxyStatus Status { get; set; } = ProxyStatus.Active;

    /// <summary>
    /// Thông tin sức khỏe của proxy
    /// </summary>
    public ProxyHealth Health { get; set; } = new();

    /// <summary>
    /// Số lần proxy đã được sử dụng
    /// </summary>
    public int UsedCount { get; set; }

    /// <summary>
    /// Thời điểm sử dụng cuối cùng
    /// </summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// Thời điểm health check cuối cùng
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>
    /// Độ ưu tiên (số càng cao càng ưu tiên)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Trọng số cho weighted rotation (0-1000)
    /// </summary>
    public int Weight { get; set; } = 100;

    /// <summary>
    /// Thời điểm hết cooldown (nếu đang bị cooldown)
    /// </summary>
    public DateTime? CooldownUntil { get; set; }

    /// <summary>
    /// Metadata bổ sung (tags, labels, etc.)
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Kiểm tra proxy có available để sử dụng không
    /// </summary>
    public bool IsAvailable()
    {
        return Status == ProxyStatus.Active
            && (!CooldownUntil.HasValue || DateTime.UtcNow >= CooldownUntil.Value)
            && Health.ConsecutiveFailures < 5;
    }

    /// <summary>
    /// Lấy URL của proxy để cấu hình HttpClient
    /// </summary>
    public string GetProxyUrl()
    {
        var scheme = Type switch
        {
            ProxyType.HTTP => "http",
            ProxyType.HTTPS => "https",
            ProxyType.SOCKS5 => "socks5",
            _ => "http"
        };

        if (!string.IsNullOrEmpty(Username))
        {
            return $"{scheme}://{Username}:{Password}@{Host}:{Port}";
        }
        return $"{scheme}://{Host}:{Port}";
    }

    /// <summary>
    /// Lấy địa chỉ hiển thị của proxy
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Host}:{Port}) - {Status}";
    }
}