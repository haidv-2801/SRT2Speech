# Proxy Service - Usage Guide & Examples

## 📑 Mục Lục

1. [Rotation Strategies](#rotation-strategies)
2. [HTTP Handlers](#http-handlers)
3. [Dependency Injection Setup](#dependency-injection-setup)
4. [Configuration Examples](#configuration-examples)
5. [Usage Examples](#usage-examples)
6. [Integration Guide](#integration-guide)

---

## 1. Rotation Strategies

### RoundRobinStrategy.cs

```csharp
namespace SRT2Speech.ProxyService.Strategies
{
    public class RoundRobinStrategy : IRotationStrategy
    {
        private int _currentIndex = 0;
        private readonly object _lock = new();

        public string Name => "RoundRobin";

        public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
        {
            var proxies = availableProxies.ToList();
            if (!proxies.Any()) return null;

            lock (_lock)
            {
                var selected = proxies[_currentIndex % proxies.Count];
                _currentIndex = (_currentIndex + 1) % proxies.Count;
                return selected;
            }
        }
    }
}
```

### RandomStrategy.cs

```csharp
namespace SRT2Speech.ProxyService.Strategies
{
    public class RandomStrategy : IRotationStrategy
    {
        private readonly Random _random = new();
        private readonly object _lock = new();

        public string Name => "Random";

        public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
        {
            var proxies = availableProxies.ToList();
            if (!proxies.Any()) return null;

            lock (_lock)
            {
                var index = _random.Next(proxies.Count);
                return proxies[index];
            }
        }
    }
}
```

### LeastUsedStrategy.cs

```csharp
namespace SRT2Speech.ProxyService.Strategies
{
    public class LeastUsedStrategy : IRotationStrategy
    {
        public string Name => "LeastUsed";

        public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
        {
            return availableProxies
                .OrderBy(p => p.UsedCount)
                .ThenByDescending(p => p.Health.HealthScore)
                .FirstOrDefault();
        }
    }
}
```

### WeightedStrategy.cs

```csharp
namespace SRT2Speech.ProxyService.Strategies
{
    public class WeightedStrategy : IRotationStrategy
    {
        private readonly Random _random = new();
        private readonly object _lock = new();

        public string Name => "Weighted";

        public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
        {
            var proxies = availableProxies.ToList();
            if (!proxies.Any()) return null;

            // Calculate weights based on health score và usage
            var weights = proxies.Select(p =>
            {
                var baseWeight = p.Weight;
                var healthBonus = p.Health.HealthScore / 100.0;
                var usagepenalty = 1.0 / (p.UsedCount + 1);
                return baseWeight * healthBonus * usagepenalty;
            }).ToList();

            var totalWeight = weights.Sum();
            if (totalWeight <= 0) return proxies.First();

            lock (_lock)
            {
                var randomValue = _random.NextDouble() * totalWeight;
                double cumulativeWeight = 0;

                for (int i = 0; i < proxies.Count; i++)
                {
                    cumulativeWeight += weights[i];
                    if (randomValue <= cumulativeWeight)
                    {
                        return proxies[i];
                    }
                }

                return proxies.Last();
            }
        }
    }
}
```

### SmartStrategy.cs

```csharp
namespace SRT2Speech.ProxyService.Strategies
{
    /// <summary>
    /// Smart strategy chọn proxy dựa trên multiple factors:
    /// - Health score
    /// - Response time
    /// - Success rate
    /// - Recent performance
    /// </summary>
    public class SmartStrategy : IRotationStrategy
    {
        private readonly IProxyMetricsCollector _metricsCollector;

        public SmartStrategy(IProxyMetricsCollector metricsCollector)
        {
            _metricsCollector = metricsCollector;
        }

        public string Name => "Smart";

        public ProxyInfo? SelectProxy(IEnumerable<ProxyInfo> availableProxies)
        {
            var proxies = availableProxies.ToList();
            if (!proxies.Any()) return null;

            // Calculate composite score cho mỗi proxy
            var scoredProxies = proxies.Select(p =>
            {
                var metrics = _metricsCollector.GetMetrics(p.Id);
                var score = CalculateProxyScore(p, metrics);
                return new { Proxy = p, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ToList();

            return scoredProxies.First().Proxy;
        }

        private double CalculateProxyScore(ProxyInfo proxy, ProxyMetrics? metrics)
        {
            double score = 0;

            // Health score (40% weight)
            score += proxy.Health.HealthScore * 0.4;

            // Success rate (30% weight)
            score += proxy.Health.SuccessRate * 0.3;

            // Response time (20% weight) - faster is better
            if (metrics != null && metrics.AverageResponseTime > 0)
            {
                var responseScore = Math.Max(0, 100 - (metrics.AverageResponseTime / 100));
                score += responseScore * 0.2;
            }
            else
            {
                score += 50 * 0.2; // Default score
            }

            // Usage balance (10% weight) - less used is better
            var usageScore = Math.Max(0, 100 - proxy.UsedCount);
            score += usageScore * 0.1;

            return score;
        }
    }
}
```

---

## 2. HTTP Handlers

### ProxyHttpClientHandler.cs

```csharp
namespace SRT2Speech.ProxyService.HttpHandlers
{
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

        public string ProxyId => _proxyInfo.Id;
    }
}
```

### ProxyRetryHandler.cs

```csharp
namespace SRT2Speech.ProxyService.HttpHandlers
{
    /// <summary>
    /// DelegatingHandler tích hợp Polly retry với proxy rotation
    /// </summary>
    public class ProxyRetryHandler : DelegatingHandler
    {
        private readonly IProxyManager _proxyManager;
        private readonly ILogger<ProxyRetryHandler> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public ProxyRetryHandler(
            IProxyManager proxyManager,
            IOptions<ProxyServiceSettings> settings,
            ILogger<ProxyRetryHandler> logger)
        {
            _proxyManager = proxyManager;
            _logger = logger;

            var maxRetries = settings.Value.MaxRetries;
            var retryDelay = settings.Value.RetryDelayMilliseconds;

            // Configure Polly retry policy
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .OrResult(r => !r.IsSuccessStatusCode && ShouldRetry(r.StatusCode))
                .WaitAndRetryAsync(
                    maxRetries,
                    retryAttempt =>
                    {
                        // Exponential backoff with jitter
                        var delay = TimeSpan.FromMilliseconds(retryDelay * Math.Pow(2, retryAttempt));
                        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                        return delay + jitter;
                    },
                    onRetry: async (outcome, timespan, retryCount, context) =>
                    {
                        var proxyId = context.ContainsKey("ProxyId")
                            ? context["ProxyId"].ToString()
                            : "unknown";

                        _logger.LogWarning(
                            "Retry {RetryCount}/{MaxRetries} cho proxy {ProxyId} sau {Delay}ms",
                            retryCount, maxRetries, proxyId, timespan.TotalMilliseconds);

                        // Mark proxy as failed
                        if (!string.IsNullOrEmpty(proxyId))
                        {
                            await _proxyManager.MarkProxyFailureAsync(
                                proxyId,
                                outcome.Exception?.Message ?? $"HTTP {outcome.Result?.StatusCode}");
                        }

                        // Get new proxy for retry
                        var newProxy = await _proxyManager.GetNextProxyAsync();
                        if (newProxy != null)
                        {
                            context["ProxyId"] = newProxy.Id;
                            _logger.LogInformation("Chuyển sang proxy mới: {ProxyId}", newProxy.Id);
                        }
                    });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var context = new Context();

            // Get initial proxy
            var proxy = await _proxyManager.GetNextProxyAsync(cancellationToken);
            if (proxy != null)
            {
                context["ProxyId"] = proxy.Id;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await _retryPolicy.ExecuteAsync(
                    async (ctx, ct) => await base.SendAsync(request, ct),
                    context,
                    cancellationToken);

                stopwatch.Stop();

                // Mark success
                if (context.ContainsKey("ProxyId"))
                {
                    var proxyId = context["ProxyId"].ToString()!;
                    await _proxyManager.MarkProxySuccessAsync(proxyId, stopwatch.Elapsed);
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Mark final failure
                if (context.ContainsKey("ProxyId"))
                {
                    var proxyId = context["ProxyId"].ToString()!;
                    await _proxyManager.MarkProxyFailureAsync(proxyId, ex.Message);
                }

                throw;
            }
        }

        private bool ShouldRetry(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.RequestTimeout => true,
                HttpStatusCode.TooManyRequests => true,
                HttpStatusCode.ServiceUnavailable => true,
                HttpStatusCode.GatewayTimeout => true,
                HttpStatusCode.ProxyAuthenticationRequired => false, // Không retry auth issues
                _ => false
            };
        }
    }
}
```

---

## 3. Dependency Injection Setup

### ServiceCollectionExtensions.cs

```csharp
namespace SRT2Speech.ProxyService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký Proxy Service vào DI container
        /// </summary>
        public static IServiceCollection AddProxyService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind configuration
            services.Configure<ProxyServiceSettings>(
                configuration.GetSection("ProxyService"));

            // Register core services
            services.AddSingleton<IProxyPool, ProxyPool>();
            services.AddSingleton<IProxyMetricsCollector, ProxyMetricsCollector>();
            services.AddSingleton<IProxyManager, ProxyManager>();

            // Register health checker as hosted service
            services.AddHostedService<ProxyHealthChecker>();
            services.AddSingleton<IProxyHealthChecker>(sp =>
                sp.GetServices<IHostedService>()
                  .OfType<ProxyHealthChecker>()
                  .First());

            // Register rotation strategy based on configuration
            services.AddSingleton<IRotationStrategy>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<ProxyServiceSettings>>().Value;
                return settings.DefaultStrategy switch
                {
                    RotationStrategy.RoundRobin => new RoundRobinStrategy(),
                    RotationStrategy.Random => new RandomStrategy(),
                    RotationStrategy.LeastUsed => new LeastUsedStrategy(),
                    RotationStrategy.Weighted => new WeightedStrategy(),
                    RotationStrategy.Smart => new SmartStrategy(
                        sp.GetRequiredService<IProxyMetricsCollector>()),
                    _ => new RoundRobinStrategy()
                };
            });

            // Load initial proxy configuration
            var proxyPool = services.BuildServiceProvider().GetRequiredService<IProxyPool>();
            LoadProxyConfiguration(proxyPool, configuration);

            return services;
        }

        /// <summary>
        /// Đăng ký HttpClient với Proxy support
        /// </summary>
        public static IHttpClientBuilder AddHttpClientWithProxy(
            this IServiceCollection services,
            string name)
        {
            return services.AddHttpClient(name)
                .AddHttpMessageHandler<ProxyRetryHandler>();
        }

        private static void LoadProxyConfiguration(IProxyPool proxyPool, IConfiguration configuration)
        {
            var configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Configs",
                "proxies.yaml");

            if (File.Exists(configPath))
            {
                var yaml = File.ReadAllText(configPath);
                var config = YamlUtility.Deserialize<ProxyConfiguration>(yaml);

                foreach (var proxy in config.Proxies)
                {
                    proxyPool.AddProxy(proxy);
                }
            }
        }
    }
}
```

### HttpClientBuilderExtensions.cs

```csharp
namespace SRT2Speech.ProxyService.Extensions
{
    public static class HttpClientBuilderExtensions
    {
        /// <summary>
        /// Thêm proxy rotation vào HttpClient
        /// </summary>
        public static IHttpClientBuilder AddProxyRotation(this IHttpClientBuilder builder)
        {
            return builder.AddHttpMessageHandler<ProxyRetryHandler>();
        }

        /// <summary>
        /// Configure HttpClient với proxy-specific settings
        /// </summary>
        public static IHttpClientBuilder ConfigureForProxy(this IHttpClientBuilder builder)
        {
            return builder
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent", "SRT2Speech/1.0");
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        }
    }
}
```

---

## 4. Configuration Examples

### proxies.yaml

```yaml
# Proxy Service Configuration
version: '1.0'

settings:
  defaultStrategy: RoundRobin # RoundRobin, Random, LeastUsed, Weighted, Smart
  enableHealthCheck: true
  healthCheckIntervalSeconds: 30
  healthCheckUrl: https://httpbin.org/ip
  healthCheckTimeoutSeconds: 10
  maxRetries: 3
  retryDelayMilliseconds: 1000
  defaultCooldownMinutes: 5
  maxConsecutiveFailures: 5
  enableStatePersistence: true
  stateSaveIntervalMinutes: 5
  stateFilePath: Configs/proxy-state.yaml
  enableDetailedLogging: false

proxies:
  # Bright Data Proxy
  - id: brightdata-1
    name: BrightData US Proxy 1
    host: brd.superproxy.io
    port: 22225
    type: HTTP
    username: your-username-country-us
    password: your-password
    priority: 100
    weight: 100
    tags:
      country: US
      provider: BrightData

  - id: brightdata-2
    name: BrightData UK Proxy
    host: brd.superproxy.io
    port: 22225
    type: HTTP
    username: your-username-country-gb
    password: your-password
    priority: 90
    weight: 90
    tags:
      country: GB
      provider: BrightData

  # Oxylabs Proxy
  - id: oxylabs-1
    name: Oxylabs Residential US
    host: pr.oxylabs.io
    port: 7777
    type: HTTP
    username: customer-username-cc-us
    password: your-password
    priority: 95
    weight: 95
    tags:
      country: US
      provider: Oxylabs
      type: residential

  - id: oxylabs-2
    name: Oxylabs Datacenter SG
    host: dc.oxylabs.io
    port: 8001
    type: HTTP
    username: customer-username
    password: your-password
    priority: 85
    weight: 85
    tags:
      country: SG
      provider: Oxylabs
      type: datacenter

  # SmartProxy
  - id: smartproxy-1
    name: SmartProxy Rotating
    host: gate.smartproxy.com
    port: 7000
    type: HTTP
    username: your-username
    password: your-password
    priority: 80
    weight: 80
    tags:
      provider: SmartProxy
      type: rotating
```

### appsettings.json (cho
