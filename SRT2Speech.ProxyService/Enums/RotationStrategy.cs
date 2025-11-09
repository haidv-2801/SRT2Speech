namespace SRT2Speech.ProxyService.Enums;

/// <summary>
/// Chiến lược rotation proxy
/// </summary>
public enum RotationStrategy
{
    /// <summary>
    /// Xoay vòng tuần tự
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Chọn ngẫu nhiên
    /// </summary>
    Random,

    /// <summary>
    /// Chọn proxy ít dùng nhất
    /// </summary>
    LeastUsed,

    /// <summary>
    /// Chọn dựa trên trọng số
    /// </summary>
    Weighted,

    /// <summary>
    /// Chọn thông minh dựa trên performance metrics
    /// </summary>
    Smart
}