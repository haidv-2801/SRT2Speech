using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.HttpHandlers;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Services;

/// <summary>
/// Validator để kiểm tra proxy hoạt động đúng và không bị IP leak
/// </summary>
public class ProxyValidator
{
    private readonly ILogger<ProxyValidator>? _logger;
    private const string IP_CHECK_URL = "https://api.ipify.org?format=json";
    private const string IP_CHECK_DETAILED_URL = "https://ipapi.co/json/";

    public ProxyValidator(ILogger<ProxyValidator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Kiểm tra proxy có hoạt động không và lấy IP address đang sử dụng
    /// </summary>
    public async Task<ProxyValidationResult> ValidateProxyAsync(
        ProxyInfo proxyInfo,
        string? realIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProxyValidationResult
        {
            ProxyInfo = proxyInfo,
            IsValid = false,
            RealIpAddress = realIpAddress
        };

        try
        {
            _logger?.LogInformation("Validating proxy {ProxyId} ({Host}:{Port}) type {Type}",
                proxyInfo.Id, proxyInfo.Host, proxyInfo.Port, proxyInfo.Type);

            // Tạo HttpClient với proxy
            HttpClient client;
            if (proxyInfo.Type == ProxyType.SOCKS5)
            {
                var handler = Socks5HttpHandlerFactory.CreateSocks5Handler(proxyInfo);
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            }
            else
            {
                var handler = new ProxyHttpClientHandler(proxyInfo);
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            }

            using (client)
            {
                // Test 1: Kiểm tra kết nối cơ bản
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await client.GetAsync(IP_CHECK_URL, cancellationToken);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"HTTP request failed with status {response.StatusCode}";
                    _logger?.LogWarning("Proxy validation failed: {Error}", result.ErrorMessage);
                    return result;
                }

                // Đọc IP address từ response
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var ipData = JsonSerializer.Deserialize<IpifyResponse>(content);
                
                if (ipData?.Ip == null)
                {
                    result.ErrorMessage = "Failed to parse IP address from response";
                    _logger?.LogWarning("Proxy validation failed: {Error}", result.ErrorMessage);
                    return result;
                }

                result.ProxyIpAddress = ipData.Ip;
                result.ResponseTime = stopwatch.Elapsed;

                // Test 2: Kiểm tra IP leak (nếu có real IP)
                if (!string.IsNullOrEmpty(realIpAddress))
                {
                    if (result.ProxyIpAddress.Equals(realIpAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        result.IsIpLeaked = true;
                        result.ErrorMessage = "IP LEAK DETECTED! Proxy is not hiding real IP address";
                        _logger?.LogError("IP LEAK DETECTED for proxy {ProxyId}: Real IP={RealIp}, Proxy IP={ProxyIp}",
                            proxyInfo.Id, realIpAddress, result.ProxyIpAddress);
                        return result;
                    }
                }

                // Test 3: Lấy thông tin chi tiết về IP (location, ISP, etc.)
                try
                {
                    var detailedResponse = await client.GetAsync(IP_CHECK_DETAILED_URL, cancellationToken);
                    if (detailedResponse.IsSuccessStatusCode)
                    {
                        var detailedContent = await detailedResponse.Content.ReadAsStringAsync(cancellationToken);
                        var detailedData = JsonSerializer.Deserialize<IpDetailedResponse>(detailedContent);
                        
                        if (detailedData != null)
                        {
                            result.Country = detailedData.Country;
                            result.City = detailedData.City;
                            result.Organization = detailedData.Org;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("Failed to get detailed IP info: {Error}", ex.Message);
                    // Không fail validation nếu không lấy được detailed info
                }

                // Validation thành công
                result.IsValid = true;
                result.IsIpLeaked = false;
                
                _logger?.LogInformation(
                    "Proxy validation SUCCESS for {ProxyId}: IP={ProxyIp}, ResponseTime={ResponseTime}ms, Location={Country}/{City}",
                    proxyInfo.Id, result.ProxyIpAddress, result.ResponseTime.TotalMilliseconds, 
                    result.Country ?? "Unknown", result.City ?? "Unknown");
            }
        }
        catch (ProxyConnectionException ex)
        {
            result.ErrorMessage = $"SOCKS5 connection error: {ex.Message}";
            _logger?.LogError(ex, "SOCKS5 proxy validation failed for {ProxyId}", proxyInfo.Id);
        }
        catch (HttpRequestException ex)
        {
            result.ErrorMessage = $"HTTP request error: {ex.Message}";
            _logger?.LogError(ex, "Proxy validation failed for {ProxyId}", proxyInfo.Id);
        }
        catch (TaskCanceledException ex)
        {
            result.ErrorMessage = "Request timeout";
            _logger?.LogWarning("Proxy validation timeout for {ProxyId}: {Error}", proxyInfo.Id, ex.Message);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            _logger?.LogError(ex, "Unexpected error during proxy validation for {ProxyId}", proxyInfo.Id);
        }

        return result;
    }

    /// <summary>
    /// Lấy real IP address của máy (không qua proxy)
    /// </summary>
    public async Task<string?> GetRealIpAddressAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync(IP_CHECK_URL, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var ipData = JsonSerializer.Deserialize<IpifyResponse>(content);
            
            _logger?.LogInformation("Real IP address detected: {RealIp}", ipData?.Ip);
            return ipData?.Ip;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get real IP address");
            return null;
        }
    }

    /// <summary>
    /// Batch validate nhiều proxies cùng lúc
    /// </summary>
    public async Task<List<ProxyValidationResult>> ValidateProxiesAsync(
        IEnumerable<ProxyInfo> proxies,
        string? realIpAddress = null,
        int maxConcurrency = 5,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProxyValidationResult>();
        var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = proxies.Select(async proxy =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ValidateProxyAsync(proxy, realIpAddress, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        results.AddRange(await Task.WhenAll(tasks));
        
        var validCount = results.Count(r => r.IsValid);
        var leakedCount = results.Count(r => r.IsIpLeaked);
        
        _logger?.LogInformation(
            "Batch validation completed: Total={Total}, Valid={Valid}, Leaked={Leaked}, Failed={Failed}",
            results.Count, validCount, leakedCount, results.Count - validCount);

        return results;
    }

    // Response models
    private class IpifyResponse
    {
        public string? Ip { get; set; }
    }

    private class IpDetailedResponse
    {
        public string? Ip { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("org")]
        public string? Org { get; set; }
    }
}

/// <summary>
/// Kết quả validation proxy
/// </summary>
public class ProxyValidationResult
{
    public ProxyInfo ProxyInfo { get; set; } = default!;
    public bool IsValid { get; set; }
    public bool IsIpLeaked { get; set; }
    public string? ProxyIpAddress { get; set; }
    public string? RealIpAddress { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Organization { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }

    public override string ToString()
    {
        if (!IsValid)
            return $"[INVALID] {ProxyInfo.Host}:{ProxyInfo.Port} - {ErrorMessage}";

        if (IsIpLeaked)
            return $"[IP_LEAK] {ProxyInfo.Host}:{ProxyInfo.Port} - Real IP exposed!";

        return $"[VALID] {ProxyInfo.Host}:{ProxyInfo.Port} -> IP: {ProxyIpAddress}, " +
               $"Location: {Country}/{City}, Response: {ResponseTime.TotalMilliseconds:F0}ms";
    }
}