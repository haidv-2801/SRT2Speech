namespace SRT2Speech.ProxyService.Models;

/// <summary>
/// Thông tin sức khỏe và metrics của proxy
/// </summary>
public class ProxyHealth
{
    /// <summary>
    /// Proxy có đang healthy không
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Thời điểm cuối cùng proxy healthy
    /// </summary>
    public DateTime? LastHealthyTime { get; set; }

    /// <summary>
    /// Số lần fail liên tiếp
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Tổng số lần fail
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// Thời điểm fail cuối cùng
    /// </summary>
    public DateTime? LastFailureTime { get; set; }

    /// <summary>
    /// Lý do fail cuối cùng
    /// </summary>
    public string? LastFailureReason { get; set; }

    /// <summary>
    /// Tổng số lần success
    /// </summary>
    public int TotalSuccesses { get; set; }

    /// <summary>
    /// Thời điểm success cuối cùng
    /// </summary>
    public DateTime? LastSuccessTime { get; set; }

    /// <summary>
    /// Thời gian response trung bình (milliseconds)
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Tỷ lệ thành công (0-100)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Điểm sức khỏe tổng hợp (0-100)
    /// </summary>
    public int HealthScore { get; set; } = 100;

    /// <summary>
    /// Ghi nhận một request thành công
    /// </summary>
    public void RecordSuccess(TimeSpan responseTime)
    {
        ConsecutiveFailures = 0;
        TotalSuccesses++;
        LastSuccessTime = DateTime.UtcNow;
        LastHealthyTime = DateTime.UtcNow;
        IsHealthy = true;

        // Update average response time (rolling average)
        AverageResponseTime = AverageResponseTime == 0
            ? responseTime.TotalMilliseconds
            : (AverageResponseTime * 0.7 + responseTime.TotalMilliseconds * 0.3);

        UpdateHealthScore();
    }

    /// <summary>
    /// Ghi nhận một request thất bại
    /// </summary>
    public void RecordFailure(string reason)
    {
        ConsecutiveFailures++;
        TotalFailures++;
        LastFailureTime = DateTime.UtcNow;
        LastFailureReason = reason;

        if (ConsecutiveFailures >= 3)
        {
            IsHealthy = false;
        }

        UpdateHealthScore();
    }

    /// <summary>
    /// Cập nhật health score dựa trên metrics
    /// </summary>
    private void UpdateHealthScore()
    {
        var total = TotalSuccesses + TotalFailures;
        if (total > 0)
        {
            SuccessRate = (double)TotalSuccesses / total * 100;

            // Health score based on success rate and consecutive failures
            HealthScore = (int)(SuccessRate * 0.7) - (ConsecutiveFailures * 10);
            HealthScore = Math.Max(0, Math.Min(100, HealthScore));
        }
    }
}