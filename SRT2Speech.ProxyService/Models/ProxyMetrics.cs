namespace SRT2Speech.ProxyService.Models;

/// <summary>
/// Metrics chi tiết của proxy
/// </summary>
public class ProxyMetrics
{
    /// <summary>
    /// ID của proxy
    /// </summary>
    public string ProxyId { get; set; } = string.Empty;

    /// <summary>
    /// Tổng số request
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Số request thành công
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// Số request thất bại
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// Thời gian response tối thiểu (milliseconds)
    /// </summary>
    public double MinResponseTime { get; set; }

    /// <summary>
    /// Thời gian response tối đa (milliseconds)
    /// </summary>
    public double MaxResponseTime { get; set; }

    /// <summary>
    /// Thời gian response trung bình (milliseconds)
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Thời gian response percentile 95 (milliseconds)
    /// </summary>
    public double P95ResponseTime { get; set; }

    /// <summary>
    /// Tổng bytes nhận được
    /// </summary>
    public long TotalBytesReceived { get; set; }

    /// <summary>
    /// Tổng bytes gửi đi
    /// </summary>
    public long TotalBytesSent { get; set; }

    /// <summary>
    /// Thời điểm sử dụng đầu tiên
    /// </summary>
    public DateTime FirstUsed { get; set; }

    /// <summary>
    /// Thời điểm sử dụng cuối cùng
    /// </summary>
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Tổng thời gian hoạt động
    /// </summary>
    public TimeSpan TotalUptime { get; set; }

    /// <summary>
    /// Thời điểm bị rate limit cuối cùng
    /// </summary>
    public DateTime? LastRateLimitHit { get; set; }

    /// <summary>
    /// Số lần bị rate limit
    /// </summary>
    public int RateLimitHitCount { get; set; }
}