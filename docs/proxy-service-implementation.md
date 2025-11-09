# Proxy Service - Chi Tiết Implementation

## 📑 Mục Lục

1. [Interfaces](#interfaces)
2. [Core Services](#core-services)
3. [Rotation Strategies](#rotation-strategies)
4. [HTTP Handlers](#http-handlers)
5. [Extensions](#extensions)
6. [Configuration Examples](#configuration-examples)

---

## 1. Interfaces

### IProxyManager.cs

```csharp
namespace SRT2Speech.ProxyService.Interfaces
{
    public interface IProxyManager
    {
        /// <summary>
        /// Lấy proxy tiếp theo dựa trên strategy
        /// </summary>
        Task<ProxyInfo?> GetNextProxyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy HttpClient đã được cấu hình với proxy
        /// </summary>
        Task<HttpClient> GetHttpClientWithProxyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Đánh dấu proxy thành công
        /// </summary>
        Task MarkProxySuccessAsync(string proxyId, TimeSpan responseTime);

        /// <summary>
        /// Đánh dấu proxy thất bại
        /// </summary>
        Task MarkProxyFailureAsync(string proxyId, string reason);

        /// <summary>
        /// Lấy danh sách tất cả proxy
        /// </summary>
        Task<IEnumerable<ProxyInfo>> GetAllProxiesAsync();

        /// <summary>
        /// Lấy danh sách proxy healthy
        /// </summary>
        Task<IEnumerable<ProxyInfo>> GetHealthyProxiesAsync();

        /// <summary>
        /// Lấy metrics tổng hợp
        /// </summary>
        Task<ProxyMetrics> GetAggregatedMetricsAsync();

        /// <summary>
        /// Lấy status summary
        /// </summary>
        string GetStatusSummary();

        /// <summary>
        /// Reload configuration từ file
        /// </summary>
        Task ReloadConfigurationAsync();
    }
}
```

### IProxyPool.cs

```csharp
namespace SRT2Speech.ProxyService.Interfaces
{
    public interface IProxyPool
    {
        /// <summary>
        /// Thêm proxy vào pool
        /// </summary>
        void AddProxy(ProxyInfo proxy);

        /// <summary>
        /// Xóa proxy khỏi pool
        /// </summary>
        bool RemoveProxy(string proxyId);

        /// <summary>
        /// Lấy proxy theo ID
        /// </summary>
        ProxyInfo? GetProxy(string proxyId);

        /// <summary>
        /// Lấy tất cả proxy
        /// </summary>
        IEnumerable<ProxyInfo> GetAllProxies();

        /// <summary>
        /// Lấy proxy available
        /// </summary>
        IEnumerable<ProxyInfo> GetAvailableProxies();

        /// <summary>
        /// Update proxy info
        /// </summary>
        void UpdateProxy(ProxyInfo proxy);

        /// <summary>
        /// Clear tất cả proxy
        /// </summary>
        void Clear();

        /// <summary>
        /// Số lượng proxy
        /// </summary>
        int Count { get; }
    }
}
```

### IRotationStrategy.cs

```csharp
namespace SRT2Speech.ProxyService.Interfaces
{
    public interface IRotationStrategy
    {
        /// <summary>
        /// Chọn proxy tiếp theo từ danh sách available proxies
        /// </summary>
        ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies);

        /// <summary>
        /// Tên strategy
        /// </summary>
        string Name { get; }
    }
}
```

### IProxyHealthChecker.cs

```csharp
namespace SRT2Speech.ProxyService.Interfaces
{
    public interface IProxyHealthChecker
    {
        /// <summary>
        /// Kiểm tra health của một proxy
        /// </summary>
        Task<bool> CheckProxyHealthAsync(ProxyInfo proxy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm tra health của tất cả proxy
        /// </summary>
        Task CheckAllProxiesHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Start background health checking
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop background health checking
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
```

### IProxyMetricsCollector.cs

```csharp
namespace SRT2Speech.ProxyService.Interfaces
{
    public interface IProxyMetricsCollector
    {
        /// <summary>
        /// Record request metrics
        /// </summary>
        void RecordRequest(string proxyId, bool success, TimeSpan responseTime);

        /// <summary>
        /// Lấy metrics của một proxy
        /// </summary>
        ProxyMetrics? GetMetrics(string proxyId);

        /// <summary>
        /// Lấy tất cả metrics
        /// </summary>
        Dictionary<string, ProxyMetrics> GetAllMetrics();

        /// <summary>
        /// Reset metrics
        /// </summary>
        void ResetMetrics(string proxyId);

        /// <summary>
        /// Reset tất cả metrics
        /// </summary>
        void ResetAllMetrics();
    }
}
```

---

## 2. Core Services

### ProxyManager.cs

```csharp
namespace SRT2Speech.ProxyService.Services
{
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

                    _logger.LogDebug("Đã chọn proxy: {ProxyId} ({Host}:{Port})",
                        selectedProxy.Id, selectedProxy.Host, selectedProxy.Port);
                }

                return selectedProxy;
            }
        }

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

        public Task<IEnumerable<ProxyInfo>> GetAllProxiesAsync()
        {
            return Task.FromResult(_proxyPool.GetAllProxies());
        }

        public Task<IEnumerable<ProxyInfo>> GetHealthyProxiesAsync()
        {
            var healthyProxies = _proxyPool.GetAllProxies()
                .Where(p => p.Status == ProxyStatus.Active && p.Health.IsHealthy);
            return Task.FromResult(healthyProxies);
        }

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

        public string GetStatusSummary()
        {
            var allProxies = _proxyPool.GetAllProxies().ToList();
            var activeCount = allProxies.Count(p => p.Status == ProxyStatus.Active);
            var unhealthyCount = allProxies.Count(p => p.Status == ProxyStatus.Unhealthy);
            var disabledCount = allProxies.Count(p => p.Status == ProxyStatus.Disabled);

            return $"Tổng: {allProxies.Count} | Active: {activeCount} | Unhealthy: {unhealthyCount} | Disabled: {disabledCount}";
        }

        public async Task ReloadConfigurationAsync()
        {
            _logger.LogInformation("Đang reload proxy configuration...");

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
        }

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

        public void Dispose()
        {
            _stateSaveTimer?.Dispose();
            SaveState(); // Final save
        }
    }
}
```

### ProxyPool.cs

```csharp
namespace SRT2Speech.ProxyService.Services
{
    public class ProxyPool : IProxyPool
    {
        private readonly ConcurrentDictionary<string, ProxyInfo> _proxies = new();
        private readonly ILogger<ProxyPool> _logger;

        public ProxyPool(ILogger<ProxyPool> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void AddProxy(ProxyInfo proxy)
        {
            if (proxy == null) throw new ArgumentNullException(nameof(proxy));

            if (_proxies.TryAdd(proxy.Id, proxy))
            {
                _logger.LogDebug("Đã thêm proxy: {ProxyId} ({Host}:{Port})",
                    proxy.Id, proxy.Host, proxy.Port);
            }
            else
            {
                _logger.LogWarning("Proxy {ProxyId} đã tồn tại trong pool", proxy.Id);
            }
        }

        public bool RemoveProxy(string proxyId)
        {
            if (_proxies.TryRemove(proxyId, out var removed))
            {
                _logger.LogDebug("Đã xóa proxy: {ProxyId}", proxyId);
                return true;
            }
            return false;
        }

        public ProxyInfo? GetProxy(string proxyId)
        {
            _proxies.TryGetValue(proxyId, out var proxy);
            return proxy;
        }

        public IEnumerable<ProxyInfo> GetAllProxies()
        {
            return _proxies.Values.ToList();
        }

        public IEnumerable<ProxyInfo> GetAvailableProxies()
        {
            return _proxies.Values.Where(p => p.IsAvailable()).ToList();
        }

        public void UpdateProxy(ProxyInfo proxy)
        {
            if (proxy == null) throw new ArgumentNullException(nameof(proxy));

            _proxies[proxy.Id] = proxy;
        }

        public void Clear()
        {
            _proxies.Clear();
            _logger.LogInformation("Đã clear tất cả proxy từ pool");
        }

        public int Count => _proxies.Count;
    }
}
```

### ProxyHealthChecker.cs

```csharp
namespace SRT2Speech.ProxyService.Services
{
    public class ProxyHealthChecker : BackgroundService, IProxyHealthChecker
    {
        private readonly IProxyPool _proxyPool;
        private readonly ProxyServiceSettings _settings;
        private readonly ILogger<ProxyHealthChecker> _logger;
        private readonly HttpClient _healthCheckClient;

        public ProxyHealthChecker(
            IProxyPool proxyPool,
            IOptions<ProxyServiceSettings> settings,
            ILogger<ProxyHealthChecker> logger)
        {
            _proxyPool = proxyPool ?? throw new ArgumentNullException(nameof(proxyPool));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _healthCheckClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_settings.HealthCheckTimeoutSeconds)
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.EnableHealthCheck)
            {
                _logger.LogInformation("Health check bị disabled");
                return;
            }

            _logger.LogInformation("Proxy health checker đã bắt đầu");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAllProxiesHealthAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong health check cycle");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds),
                    stoppingToken);
            }
        }

        public async Task<bool> CheckProxyHealthAsync(ProxyInfo proxy, CancellationToken cancellationToken = default)
        {
            try
            {
                proxy.Status = ProxyStatus.Testing;
                proxy.LastHealthCheck = DateTime.UtcNow;

                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(proxy.GetProxyUrl()),
                    UseProxy = true
                };

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
                    _proxyPool.UpdateProxy(proxy);

                    _logger.LogDebug("Health check thành công cho proxy {ProxyId} -
```
