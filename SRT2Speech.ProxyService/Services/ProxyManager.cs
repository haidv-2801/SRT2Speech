using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.Exceptions;
using SRT2Speech.ProxyService.HttpHandlers;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;
using YamlDotNet.Serialization.NamingConventions;
using static SRT2Speech.ProxyService.HttpHandlers.Socks5HttpHandlerFactory;

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

        HttpMessageHandler handler;
        
        // Sử dụng handler phù hợp với proxy type
        if (proxy.Type == ProxyType.SOCKS5)
        {
            _logger.LogDebug("Creating SOCKS5 HttpClient for proxy {ProxyId} ({Host}:{Port})",
                proxy.Id, proxy.Host, proxy.Port);
            handler = Socks5HttpHandlerFactory.CreateSocks5Handler(proxy);
        }
        else
        {
            _logger.LogDebug("Creating HTTP/HTTPS HttpClient for proxy {ProxyId} ({Host}:{Port})",
                proxy.Id, proxy.Host, proxy.Port);
            handler = new ProxyHttpClientHandler(proxy);
        }
        
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
            // Ưu tiên load từ tệp nguồn trong dự án, fallback sang tệp runtime
            var workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "SRT2Speech.AppWindow", "Configs", "proxies.yaml");
            var runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "proxies.yaml");

            var configPathToRead = File.Exists(workspacePath) ? workspacePath : runtimePath;

            if (File.Exists(configPathToRead))
            {
                var yaml = await File.ReadAllTextAsync(configPathToRead);
                var config = YamlUtility.DeserializeAuto<ProxyConfiguration>(yaml);

                _proxyPool.Clear();
                foreach (var proxy in config.Proxies)
                {
                    _proxyPool.AddProxy(proxy);
                }

                _logger.LogInformation("Đã reload {Count} proxies từ {Path}", config.Proxies.Count, configPathToRead);
            }
            else
            {
                _logger.LogWarning("Không tìm thấy file config tại {Path}", configPathToRead);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi reload configuration");
            throw new ProxyConfigurationException("Lỗi khi reload configuration", ex);
        }
    }

    /// <summary>
    /// Nhập khẩu danh sách proxy binding dạng ip:port:username:password và replace toàn bộ Proxies trong file cấu hình.
    /// Trả về số lượng proxy hợp lệ đã import.
    /// </summary>
    public async Task<int> ImportBindingsAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));

        // Parse danh sách binding và loại trùng theo Host:Port
        var parsedProxies = ProxyBindingParser.ParseLines(lines, _logger);

        if (parsedProxies.Count == 0)
        {
            _logger.LogWarning("Không có proxy hợp lệ để import từ danh sách binding.");
            return 0;
        }

        try
        {
            // Xác định đường dẫn tệp cấu hình ưu tiên (nguồn dự án) và tệp runtime
            var workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "Configs", "proxies.yaml");
            var runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "proxies.yaml");

            // Đọc cấu hình hiện có từ nguồn ưu tiên nếu có, fallback runtime
            ProxyConfiguration config;
            var readPath = File.Exists(workspacePath) ? workspacePath : runtimePath;

            if (!string.IsNullOrEmpty(readPath) && File.Exists(readPath))
            {
                var yamlIn = await File.ReadAllTextAsync(readPath, cancellationToken);
                config = YamlUtility.DeserializeAuto<ProxyConfiguration>(yamlIn);
            }
            else
            {
                config = new ProxyConfiguration
                {
                    Settings = new ProxyServiceSettings(),
                    LoadedAt = DateTime.UtcNow,
                    Version = "1.0"
                };
            }

            // Replace toàn bộ danh sách Proxies, giữ nguyên Settings/Version/LoadedAt như yêu cầu
            config.Proxies = parsedProxies;

            var yamlOut = YamlUtility.SerializeToHyphenated(config);

            // Ghi vào tệp nguồn dự án nếu có thể
            var workspaceDir = Path.GetDirectoryName(workspacePath);
            if (!string.IsNullOrEmpty(workspaceDir) && !Directory.Exists(workspaceDir))
            {
                Directory.CreateDirectory(workspaceDir);
            }
            try
            {
                await File.WriteAllTextAsync(workspacePath, yamlOut, cancellationToken);
                _logger.LogInformation("Đã import {Count} proxies và cập nhật tệp nguồn: {Path}", parsedProxies.Count, workspacePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể ghi tệp nguồn tại {Path}, sẽ tiếp tục với tệp runtime", workspacePath);
            }

            // Đồng bộ tệp runtime để đảm bảo app chạy có cấu hình mới
            var runtimeDir = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrEmpty(runtimeDir) && !Directory.Exists(runtimeDir))
            {
                Directory.CreateDirectory(runtimeDir);
            }
            await File.WriteAllTextAsync(runtimePath, yamlOut, cancellationToken);
            _logger.LogInformation("Đã đồng bộ tệp runtime: {Path}", runtimePath);

            // Reload cấu hình vào pool để áp dụng ngay
            await ReloadConfigurationAsync();

            return parsedProxies.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi import danh sách proxy binding");
            throw new ProxyConfigurationException("Lỗi khi import danh sách proxy binding", ex);
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
            //LoadState();

            // Setup auto-save timer
            var interval = TimeSpan.FromMinutes(_settings.StateSaveIntervalMinutes);
            //_stateSaveTimer = new Timer(_ => SaveState(), null, interval, interval);
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
                var state = YamlUtility.DeserializeAuto<ProxyState>(yaml);

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

            var yaml = YamlUtility.SerializeToHyphenated(state);
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