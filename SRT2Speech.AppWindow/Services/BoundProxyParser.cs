using System;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.AppWindow.Services
{
    /// <summary>
    /// Parser endpoint proxy cố định cho API key.
    /// Hỗ trợ format:
    ///  - host:port
    ///  - host:port:username:password
    /// Trả về ProxyInfo đã điền thông tin phù hợp để cấu hình ProxyHttpClientHandler.
    /// </summary>
    public static class BoundProxyParser
    {
        public static bool TryParseBoundEndpoint(string? endpoint, out ProxyInfo? proxy, out string? error)
        {
            proxy = null;
            error = null;

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                error = "Endpoint is empty";
                return false;
            }

            var parts = endpoint.Trim().Split(':');

            if (parts.Length != 2 && parts.Length != 4)
            {
                error = "Invalid format. Expected 'host:port' or 'host:port:username:password'";
                return false;
            }

            var host = parts[0].Trim();
            var portStr = parts[1].Trim();

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

            string? username = null;
            string? password = null;

            if (parts.Length == 4)
            {
                username = parts[2].Trim();
                password = parts[3].Trim();

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
            }

            proxy = new ProxyInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Bound {host}:{port}",
                Host = host,
                Port = port,
                Type = ProxyType.SOCKS5,
                Username = username,
                Password = password,
                Priority = 100,
                Weight = 100
            };

            return true;
        }

        public static string ToHostPortKey(string host, int port)
            => $"{host}:{port}";

        public static string? NormalizeToHostPortKey(string? endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return null;
            var parts = endpoint.Trim().Split(':');
            if (parts.Length != 2 && parts.Length != 4) return null;
            var host = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), out var port)) return null;
            return $"{host}:{port}";
        }
    }
}