using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.Exceptions;
using SRT2Speech.ProxyService.HttpHandlers;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.Services;

/// <summary>
/// Main proxy manager - điểm trung tâm để quản lý proxy
/// </summary>
public class ProxyManager : IProxyManager, IDisposable
{
    private readonly IProxyPool _proxyPool;
    private readonly IRotationStrategy _rotationStrategy;
    private readonly IProxyHealthChecker _healthChecker;
    private readonly IProxyMetricsCollector _metricsCollector;
    private readonly ProxyServiceSettings _settings;
    private readonly ILogger<ProxyManager> _logger;
    private readonly object _lock = new();
    private Timer? _stateSaveTimer;
    private bool _disposed = false;

    public ProxyManager(
        IProxyPool proxyPool,
        IRotationStrategy rotationStrategy,
        IProxyHealthChecker healthChecker,
        IProxyMetricsCollector metricsCollector,
        IOptions<ProxyServiceSettings> settings,
        ILogger<ProxyManager> logger)
    {
        _proxyPool = proxyPool ?? throw new ArgumentNullException(nameof(proxyPool));
        _rotationStrategy = rotationStrategy ?? throw new ArgumentNullException(nameof(rotationStrategy));
        _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeStatePersistence();
    }

    /// <inheritdoc/>
    public async Task<ProxyInfo?> GetNextProxyAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var availableProxies = _proxyPool.GetAvailableProxies().ToList();

            if (!availableProxies.Any())
            {
                _logger.LogWarning("Không có proxy nào available");
                return null;
            }

            var selectedProxy = _rotationStrategy.SelectProxy(availableProxies);

            if (selectedProxy != null)
            {
                selectedProxy.UsedCount++;
                selectedProxy.LastUsed = DateTime.UtcNow;
                _proxyPool.UpdateProxy(selectedProxy);

                _logger.LogDebug("Đã chọn proxy: {ProxyId} ({Host}:{Port}) bằng strategy {Strategy}",
                    selectedProxy.Id, selectedProxy.Host, selectedProxy.Port, _rotationStrategy.Name);
            }

            return selectedProxy;
        }
    }

    /// <inheritdoc/>
    public async Task<HttpClient> GetHttpClientWithProxyAsync(CancellationToken cancellationToken = default)
    {
        var proxy = await GetNextProxyAsync(cancellationToken);

        if (proxy == null)
        {
            throw new NoAvailableProxyException("Không có proxy nào available");
        }

        var handler = new ProxyHttpClientHandler(proxy);
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        return client;
    }

    /// <inheritdoc/>
    public Task MarkProxySuccessAsync(string proxyId, TimeSpan responseTime)
    {
        lock (_lock)
        {
            var proxy = _proxyPool.GetProxy(proxyId);
            if (proxy != null)
            {
                proxy.Health.RecordSuccess(responseTime);
                proxy.Status = ProxyStatus.Active;
                _proxyPool.UpdateProxy(proxy);

                _metricsCollector.RecordRequest(proxyId, true, responseTime);

                _logger.LogDebug("Proxy {ProxyId} thành công - Response time: {ResponseTime}ms",
                    proxyId, responseTime.TotalMilliseconds);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkProxyFailureAsync(string proxyId, string reason)
    {
        lock (_lock)
        {
            var proxy = _proxyPool.GetProxy(proxyId);
            if (proxy != null)
            {
                proxy.Health.RecordFailure(reason);

                if (proxy.Health.ConsecutiveFailures >= _settings.MaxConsecutiveFailures)
                {
                    proxy.Status = ProxyStatus.Unhealthy;
                    proxy.CooldownUntil = DateTime.UtcNow.AddMinutes(_settings.DefaultCooldownMinutes);

                    _logger.LogWarning("Proxy {ProxyId} đã bị đánh dấu unhealthy - Lý do: {Reason}",
                        proxyId, reason);
                }

                _proxyPool.UpdateProxy(proxy);
                _metricsCollector.RecordRequest(proxyId, false, TimeSpan.Zero);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ProxyInfo>> GetAllProxiesAsync()
    {
        return Task.FromResult(_proxyPool.GetAllProxies());
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ProxyInfo>> GetHealthyProxiesAsync()
    {
        var healthyProxies = _proxyPool.GetAllProxies()
            .Where(p => p.Status == ProxyStatus.Active && p.Health.IsHealthy);
        return Task.FromResult(healthyProxies);
    }

    /// <inheritdoc/>
    public Task<ProxyMetrics> GetAggregatedMetricsAsync()
    {
        var allMetrics = _metricsCollector.GetAllMetrics().Values;

        var aggregated = new ProxyMetrics
        {
            ProxyId = "aggregated",
            TotalRequests = allMetrics.Sum(m => m.TotalRequests),
            SuccessfulRequests = allMetrics.Sum(m => m.SuccessfulRequests),
            FailedRequests = allMetrics.Sum(m => m.FailedRequests),
            AverageResponseTime = allMetrics.Any() ? allMetrics.Average(m => m.AverageResponseTime) : 0
        };

        return Task.FromResult(aggregated);
    }

    /// <inheritdoc/>
    public string GetStatusSummary()
    {
        var allProxies = _proxyPool.GetAllProxies().ToList();
        var activeCount = allProxies.Count(p => p.Status == ProxyStatus.Active);
        var unhealthyCount = allProxies.Count(p => p.Status == ProxyStatus.Unhealthy);
        var disabledCount = allProxies.Count(p => p.Status == ProxyStatus.Disabled);

        return $"Tổng: {allProxies.Count} | Active: {activeCount} | Unhealthy: {unhealthyCount} | Disabled: {disabledCount}";
    }

    /// <inheritdoc/>
    public async Task ReloadConfigurationAsync()
    {
        _logger.LogInformation("Đang reload proxy configuration...");

        try
        {
            // Load từ file và update pool
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "proxies.yaml");
            if (File.Exists(configPath))
            {
                var yaml = await File.ReadAllTextAsync(configPath);
                var config = YamlUtility.Deserialize<ProxyConfiguration>(yaml);

                _proxyPool.Clear();
                foreach (var proxy in config.Proxies)
                {
                    _proxyPool.AddProxy(proxy);
                }

                _logger.LogInformation("Đã reload {Count} proxies", config.Proxies.Count);
            }
            else
            {
                _logger.LogWarning("Không tìm thấy file config tại {Path}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi reload configuration");
            throw new ProxyConfigurationException("Lỗi khi reload configuration", ex);
        }
    }

    /// <summary>
    /// Khởi tạo state persistence
    /// </summary>
    private void InitializeStatePersistence()
    {
        if (_settings.EnableStatePersistence)
        {
            // Load state từ file nếu có
            LoadState();

            // Setup auto-save timer
            var interval = TimeSpan.FromMinutes(_settings.StateSaveIntervalMinutes);
            _stateSaveTimer = new Timer(_ => SaveState(), null, interval, interval);
        }
    }

    /// <summary>
    /// Load state từ file
    /// </summary>
    private void LoadState()
    {
        try
        {
            var statePath = _settings.StateFilePath;
            if (File.Exists(statePath))
            {
                var yaml = File.ReadAllText(statePath);
                var state = YamlUtility.Deserialize<ProxyState>(yaml);

                // Update proxy states
                foreach (var savedProxy in state.Proxies)
                {
                    var existingProxy = _proxyPool.GetProxy(savedProxy.Id);
                    if (existingProxy != null)
                    {
                        existingProxy.UsedCount = savedProxy.UsedCount;
                        existingProxy.Health = savedProxy.Health;
                        existingProxy.Status = savedProxy.Status;
                        existingProxy.LastUsed = savedProxy.LastUsed;
                        _proxyPool.UpdateProxy(existingProxy);
                    }
                }

                _logger.LogInformation("Đã load proxy state từ {Path}", statePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi load proxy state");
        }
    }

    /// <summary>
    /// Save state ra file
    /// </summary>
    private void SaveState()
    {
        try
        {
            var state = new ProxyState
            {
                Proxies = _proxyPool.GetAllProxies().ToList(),
                LastSaved = DateTime.UtcNow,
                Strategy = _settings.DefaultStrategy,
                Metrics = _metricsCollector.GetAllMetrics()
            };

            var yaml = YamlUtility.Serialize(state);
            var statePath = _settings.StateFilePath;
            var directory = Path.GetDirectoryName(statePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(statePath, yaml);

            _logger.LogDebug("Đã save proxy state tới {Path}", statePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi save proxy state");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _stateSaveTimer?.Dispose();
        SaveState(); // Final save

        _disposed = true;
        _logger.LogInformation("ProxyManager disposed");
    }
}