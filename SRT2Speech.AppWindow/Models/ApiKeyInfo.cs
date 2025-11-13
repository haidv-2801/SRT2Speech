using System;

namespace SRT2Speech.AppWindow.Models
{
    public class ApiKeyInfo
    {
        public string Key { get; set; } = string.Empty;
        public bool Available { get; set; } = true;
        public DateTime? CooldownUntil { get; set; }
        public int UsedCount { get; set; } = 0;
        public int Priority { get; set; } = 0; // Higher value means higher priority

        /// <summary>
        /// Proxy endpoint được gắn cố định với key này.
        /// Format hợp lệ:
        /// - "host:port"
        /// - "host:port:username:password"
        /// Nếu null hoặc rỗng: theo yêu cầu business, key sẽ KHÔNG được sử dụng (không fallback).
        /// </summary>
        public string? BoundProxyEndpoint { get; set; }

        public bool IsAvailable()
        {
            return Available && (CooldownUntil == null || DateTime.UtcNow >= CooldownUntil);
        }

        public void MarkExhausted(TimeSpan cooldownPeriod)
        {
            Available = false;
            CooldownUntil = DateTime.UtcNow.Add(cooldownPeriod);
        }

        public void ResetCooldown()
        {
            Available = true;
            CooldownUntil = null;
        }

        public void IncrementUsage()
        {
            UsedCount++;
        }
    }
}