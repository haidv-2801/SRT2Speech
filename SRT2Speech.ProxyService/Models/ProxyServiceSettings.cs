using SRT2Speech.ProxyService.Enums;

namespace SRT2Speech.ProxyService.Models;

/// <summary>
/// Cấu hình cho Proxy Service
/// </summary>
public class ProxyServiceSettings
{
    /// <summary>
    /// Chiến lược rotation mặc định
    /// </summary>
    public RotationStrategy DefaultStrategy { get; set; } = RotationStrategy.RoundRobin;

    /// <summary>
    /// Bật health check tự động
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// Khoảng thời gian giữa các lần health check (giây)
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// URL để test health check
    /// </summary>
    public string HealthCheckUrl { get; set; } = "https://httpbin.org/ip";

    /// <summary>
    /// Timeout cho health check (giây)
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Số lần retry tối đa khi request fail
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Độ trễ giữa các lần retry (milliseconds)
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Thời gian cooldown mặc định khi proxy fail (phút)
    /// </summary>
    public int DefaultCooldownMinutes { get; set; } = 5;

    /// <summary>
    /// Số lần fail liên tiếp tối đa trước khi mark unhealthy
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>
    /// Bật lưu trữ state
    /// </summary>
    public bool EnableStatePersistence { get; set; } = true;

    /// <summary>
    /// Khoảng thời gian tự động save state (phút)
    /// </summary>
    public int StateSaveIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Đường dẫn file lưu state
    /// </summary>
    public string StateFilePath { get; set; } = "Configs/proxy-state.yaml";

    /// <summary>
    /// Bật logging chi tiết
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}