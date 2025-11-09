using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Services;

/// <summary>
/// Background service để kiểm tra health của proxy
/// </summary>
public class ProxyHealthChecker : BackgroundService, IProxyHealthChecker
{
    private readonly IProxyPool _proxyPool;
    private readonly ProxyServiceSettings _settings;
    private readonly ILogger<ProxyHealthChecker> _logger;

    public ProxyHealthChecker(
        IProxyPool proxyPool,
        IOptions<ProxyServiceSettings> settings,
        ILogger<ProxyHealthChecker> logger)
    {
        _proxyPool = proxyPool ?? throw new ArgumentNullException(nameof(proxyPool));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> CheckProxyHealthAsync(ProxyInfo proxy, CancellationToken cancellationToken = default)
    {
        if (proxy == null) throw new ArgumentNullException(nameof(proxy));

        try
        {
            proxy.Status = ProxyStatus.Testing;
            proxy.LastHealthCheck = DateTime.UtcNow;

            var handler = new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(proxy.GetProxyUrl()),
                UseProxy = true,
                AllowAutoRedirect = true
            };

            if (!string.IsNullOrEmpty(proxy.Username))
            {
                handler.Proxy.Credentials = new System.Net.NetworkCredential(
                    proxy.Username,
                    proxy.Password);
            }

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(_settings.HealthCheckTimeoutSeconds)
            };

            var stopwatch = Stopwatch.StartNew();
            var response = await client.GetAsync(_settings.HealthCheckUrl, cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                proxy.Health.RecordSuccess(stopwatch.Elapsed);
                proxy.Status = ProxyStatus.Active;
                proxy.CooldownUntil = null;

                _logger.LogDebug("Proxy {ProxyId} health check PASSED - Response time: {ResponseTime}ms",
                    proxy.Id, stopwatch.Elapsed.TotalMilliseconds);

                return true;
            }
            else
            {
                proxy.Health.RecordFailure($"HTTP {response.StatusCode}");
                proxy.Status = ProxyStatus.Unhealthy;

                _logger.LogWarning("Proxy {ProxyId} health check FAILED - Status: {StatusCode}",
                    proxy.Id, response.StatusCode);

                return false;
            }
        }
        catch (Exception ex)
        {
            proxy.Health.RecordFailure(ex.Message);
            proxy.Status = ProxyStatus.Unhealthy;

            _logger.LogError(ex, "Lỗi khi health check proxy {ProxyId}", proxy.Id);

            return false;
        }
        finally
        {
            _proxyPool.UpdateProxy(proxy);
        }
    }

    /// <inheritdoc/>
    public async Task CheckAllProxiesHealthAsync(CancellationToken cancellationToken = default)
    {
        var proxies = _proxyPool.GetAllProxies().ToList();

        _logger.LogInformation("Bắt đầu health check cho {Count} proxy", proxies.Count);

        var tasks = proxies.Select(proxy => CheckProxyHealthAsync(proxy, cancellationToken));
        var results = await Task.WhenAll(tasks);

        var healthyCount = results.Count(r => r);
        var unhealthyCount = results.Count(r => !r);

        _logger.LogInformation("Health check hoàn thành - Healthy: {Healthy}, Unhealthy: {Unhealthy}",
            healthyCount, unhealthyCount);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnableHealthCheck)
        {
            _logger.LogInformation("Health check bị vô hiệu hóa");
            return;
        }

        _logger.LogInformation("ProxyHealthChecker service đang khởi động...");

        // Wait a bit before first check
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllProxiesHealthAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong health check loop");
            }

            // Wait for next check
            await Task.Delay(
                TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInformation("ProxyHealthChecker service đang dừng...");
    }

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProxyHealthChecker starting...");
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProxyHealthChecker stopping...");
        return base.StopAsync(cancellationToken);
    }
}