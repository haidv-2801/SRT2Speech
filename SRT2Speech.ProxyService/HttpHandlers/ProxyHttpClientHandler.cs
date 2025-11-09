using System.Net;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.HttpHandlers;

/// <summary>
/// Custom HttpClientHandler được cấu hình với proxy
/// </summary>
public class ProxyHttpClientHandler : HttpClientHandler
{
    private readonly ProxyInfo _proxyInfo;

    public ProxyHttpClientHandler(ProxyInfo proxyInfo)
    {
        _proxyInfo = proxyInfo ?? throw new ArgumentNullException(nameof(proxyInfo));

        // Configure proxy
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