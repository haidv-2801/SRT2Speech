using System.Net;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.HttpHandlers;

/// <summary>
/// Custom HttpClientHandler được cấu hình với proxy
/// TODO: CRITICAL - SOCKS5 support chưa hoàn thiện!
/// WebProxy không hỗ trợ SOCKS5 natively. Cần implement custom SocketsHttpHandler
/// hoặc dùng thư viện như SocksSharp (requires custom DelegatingHandler).
/// Hiện tại: SOCKS5 proxies sẽ FAIL khi connect!
/// </summary>
public class ProxyHttpClientHandler : HttpClientHandler
{
    private readonly ProxyInfo _proxyInfo;

    public ProxyHttpClientHandler(ProxyInfo proxyInfo)
    {
        _proxyInfo = proxyInfo ?? throw new ArgumentNullException(nameof(proxyInfo));

        // TODO: Add proper SOCKS5 support
        // Current limitation: WebProxy không support SOCKS5
        if (_proxyInfo.Type == ProxyType.SOCKS5)
        {
            // WARNING: This will NOT work properly for SOCKS5!
            // Keeping for backward compatibility but needs fix
            Console.WriteLine($"[PROXY_WARNING] SOCKS5 proxy {_proxyInfo.Host}:{_proxyInfo.Port} may not work properly with WebProxy!");
        }

        // Configure proxy (works for HTTP/HTTPS only)
        Proxy = new WebProxy(_proxyInfo.GetProxyUrl());
        UseProxy = true;

        // Configure credentials if provided
        if (!string.IsNullOrEmpty(_proxyInfo.Username))
        {
            Proxy.Credentials = new NetworkCredential(
                _proxyInfo.Username,
                _proxyInfo.Password);
        }

        // Other configurations
        AllowAutoRedirect = true;
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        UseCookies = true;
    }

    /// <summary>
    /// ID của proxy được sử dụng
    /// </summary>
    public string ProxyId => _proxyInfo.Id;

    /// <summary>
    /// Thông tin proxy
    /// </summary>
    public ProxyInfo ProxyInfo => _proxyInfo;
}