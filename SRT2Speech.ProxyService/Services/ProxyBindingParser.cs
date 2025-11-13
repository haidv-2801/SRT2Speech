using Microsoft.Extensions.Logging;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Services
{
    /// <summary>
    /// Parser cho danh sách proxy binding dạng "host:port:username:password".
    /// Cung cấp TryParseLine cho từng dòng và ParseLines cho danh sách, loại trùng theo Host:Port.
    /// </summary>
    public static class ProxyBindingParser
    {
        /// <summary>
        /// Parse một dòng binding thành ProxyInfo. Hợp lệ khi có đủ 4 phần và port trong [1..65535].
        /// Cho phép hostname/IP bất kỳ, không ràng buộc định dạng IPv4 cứng.
        /// </summary>
        public static bool TryParseLine(string line, out ProxyInfo? proxy, out string? error)
        {
            proxy = null;
            error = null;

            if (string.IsNullOrWhiteSpace(line))
            {
                error = "Empty line";
                return false;
            }

            line = line.Trim();

            // Bỏ qua dòng comment
            if (line.StartsWith("#"))
            {
                error = "Comment line";
                return false;
            }

            var parts = line.Split(':');
            if (parts.Length < 4)
            {
                error = "Invalid format, expected 'host:port:username:password'";
                return false;
            }

            var host = parts[0].Trim();
            var portStr = parts[1].Trim();
            var username = parts[2].Trim();
            var password = parts[3].Trim();

            if (string.IsNullOrWhiteSpace(host))
            {
                error = "Host is empty";
                return false;
            }

            if (!int.TryParse(portStr, out var port) || port <= 0 || port > 65535)
            {
                error = "Port invalid";
                return false;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                error = "Username is empty";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                error = "Password is empty";
                return false;
            }

            proxy = new ProxyInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Binding {host}:{port}",
                Host = host,
                Port = port,
                Type = ProxyType.SOCKS5,
                Username = username,
                Password = password,
                Status = ProxyStatus.Active,
                Priority = 100,
                Weight = 100,
                Tags = new Dictionary<string, string>
                {
                    ["Provider"] = "Binding",
                    ["Region"] = "Unknown"
                }
            };

            return true;
        }

        /// <summary>
        /// Parse danh sách các dòng binding, loại bỏ dòng không hợp lệ và trùng lặp theo Host:Port.
        /// Có thể ghi log cảnh báo nếu cung cấp logger.
        /// </summary>
        public static List<ProxyInfo> ParseLines(IEnumerable<string> lines, ILogger? logger = null)
        {
            var result = new List<ProxyInfo>();
            var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in lines)
            {
                if (!TryParseLine(raw, out var proxy, out var error))
                {
                    // Bỏ qua dòng trống, comment, và sai định dạng - log cảnh báo cho sai định dạng
                    if (!string.IsNullOrEmpty(error) && error != "Empty line" && error != "Comment line")
                    {
                        logger?.LogWarning("Bỏ qua dòng binding không hợp lệ: '{Line}' - Lý do: {Reason}", raw, error);
                    }
                    continue;
                }

                var key = $"{proxy!.Host}:{proxy.Port}";
                if (!dedup.Add(key))
                {
                    logger?.LogWarning("Bỏ qua bản ghi trùng lặp Host:Port: {Key}", key);
                    continue;
                }

                result.Add(proxy);
            }

            return result;
        }
    }
}