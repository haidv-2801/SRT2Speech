# Proxy Service - Integration & Examples

## 📑 Mục Lục

1. [AppWindow Integration](#appwindow-integration)
2. [WebAPI Integration](#webapi-integration)
3. [Usage Examples](#usage-examples)
4. [Testing Strategy](#testing-strategy)
5. [Deployment Checklist](#deployment-checklist)
6. [Troubleshooting](#troubleshooting)

---

## 1. AppWindow Integration

### Step 1: Thêm Package Reference vào SRT2Speech.AppWindow.csproj

```xml
<ItemGroup>
  <ProjectReference Include="..\SRT2Speech.ProxyService\SRT2Speech.ProxyService.csproj" />
</ItemGroup>
```

### Step 2: Update App.xaml.cs để đăng ký services

```csharp
using SRT2Speech.ProxyService.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace SRT2Speech.AppWindow
{
    public partial class App : Application
    {
        private IHost? _host;

        public App()
        {
            InitializeComponent();

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                })
                .ConfigureServices((context, services) =>
                {
                    // Đăng ký Proxy Service
                    services.AddProxyService(context.Configuration);

                    // Đăng ký HttpClient cho TTS APIs với proxy
                    services.AddHttpClientWithProxy("FptTTS")
                        .ConfigureForProxy();

                    services.AddHttpClientWithProxy("VbeeTTS")
                        .ConfigureForProxy();

                    services.AddHttpClientWithProxy("ElevenLabsTTS")
                        .ConfigureForProxy();

                    services.AddHttpClientWithProxy("GoogleTTS")
                        .ConfigureForProxy();

                    // Existing services
                    services.AddSingleton<ApiKeyManager>();
                    services.AddTransient<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host!.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            using (_host)
            {
                await _host!.StopAsync(TimeSpan.FromSeconds(5));
            }

            base.OnExit(e);
        }
    }
}
```

### Step 3: Update appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=localhost;Initial Catalog=MyDatabase;Integrated Security=True"
  },
  "ApiSettings": {
    "BaseUrl": "https://api.example.com/v1",
    "Ngrok": {
      "FptWebhook": "https://81cf-14-191-165-222.ngrok-free.app",
      "VbeeWebhook": "https://81cf-14-191-165-222.ngrok-free.app"
    }
  },
  "ProxyService": {
    "DefaultStrategy": "RoundRobin",
    "EnableHealthCheck": true,
    "HealthCheckIntervalSeconds": 30,
    "HealthCheckUrl": "https://httpbin.org/ip",
    "HealthCheckTimeoutSeconds": 10,
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 1000,
    "DefaultCooldownMinutes": 5,
    "MaxConsecutiveFailures": 5,
    "EnableStatePersistence": true,
    "StateSaveIntervalMinutes": 5,
    "StateFilePath": "Configs/proxy-state.yaml",
    "EnableDetailedLogging": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SRT2Speech.ProxyService": "Debug",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Step 4: Sử dụng trong View hoặc ViewModel

```csharp
using SRT2Speech.ProxyService.Interfaces;

namespace SRT2Speech.AppWindow.ViewModels
{
    public class TranslateControlViewModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProxyManager _proxyManager;
        private readonly ILogger<TranslateControlViewModel> _logger;

        public TranslateControlViewModel(
            IHttpClientFactory httpClientFactory,
            IProxyManager proxyManager,
            ILogger<TranslateControlViewModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _proxyManager = proxyManager;
            _logger = logger;
        }

        public async Task<string> TranslateTextAsync(string text, string targetLanguage)
        {
            try
            {
                // Lấy HttpClient với proxy rotation
                using var client = _httpClientFactory.CreateClient("GoogleTTS");

                var requestBody = new { text, target = targetLanguage };
                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                // Request sẽ tự động rotate proxy và retry nếu fail
                var response = await client.PostAsync(
                    "https://translation.googleapis.com/language/translate/v2",
                    content);

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();

                // Log proxy status
                _logger.LogInformation("Proxy status: {Status}",
                    _proxyManager.GetStatusSummary());

                return result;
            }
            catch (NoAvailableProxyException ex)
            {
                _logger.LogError(ex, "Không có proxy nào available");
                throw new InvalidOperationException(
                    "Tất cả proxy đều không khả dụng. Vui lòng kiểm tra cấu hình.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi translate text");
                throw;
            }
        }

        public async Task<List<ProxyInfo>> GetProxyStatusAsync()
        {
            var proxies = await _proxyManager.GetAllProxiesAsync();
            return proxies.ToList();
        }
    }
}
```

---

## 2. WebAPI Integration

### Step 1: Update Program.cs

```csharp
using SRT2Speech.ProxyService.Extensions;
using SRT2Speech.WebAPI.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Đăng ký Proxy Service
builder.Services.AddProxyService(builder.Configuration);

// Đăng ký HttpClients với proxy
builder.Services.AddHttpClientWithProxy("FptAPI")
    .ConfigureForProxy();

builder.Services.AddHttpClientWithProxy("VbeeAPI")
    .ConfigureForProxy();

builder.Services.AddHttpClientWithProxy("ElevenLabsAPI")
    .ConfigureForProxy();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessageHub>("/messageHub");

app.Run();
```

### Step 2: Tạo ProxyController để monitor

```csharp
using Microsoft.AspNetCore.Mvc;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProxyController : ControllerBase
    {
        private readonly IProxyManager _proxyManager;
        private readonly ILogger<ProxyController> _logger;

        public ProxyController(
            IProxyManager proxyManager,
            ILogger<ProxyController> logger)
        {
            _proxyManager = proxyManager;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tất cả proxy
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProxyInfo>>> GetAllProxies()
        {
            var proxies = await _proxyManager.GetAllProxiesAsync();
            return Ok(proxies);
        }

        /// <summary>
        /// Lấy danh sách proxy healthy
        /// </summary>
        [HttpGet("healthy")]
        public async Task<ActionResult<IEnumerable<ProxyInfo>>> GetHealthyProxies()
        {
            var proxies = await _proxyManager.GetHealthyProxiesAsync();
            return Ok(proxies);
        }

        /// <summary>
        /// Lấy status summary
        /// </summary>
        [HttpGet("status")]
        public ActionResult<string> GetStatus()
        {
            var status = _proxyManager.GetStatusSummary();
            return Ok(new { status, timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Lấy aggregated metrics
        /// </summary>
        [HttpGet("metrics")]
        public async Task<ActionResult<ProxyMetrics>> GetMetrics()
        {
            var metrics = await _proxyManager.GetAggregatedMetricsAsync();
            return Ok(metrics);
        }

        /// <summary>
        /// Reload proxy configuration
        /// </summary>
        [HttpPost("reload")]
        public async Task<ActionResult> ReloadConfiguration()
        {
            await _proxyManager.ReloadConfigurationAsync();
            _logger.LogInformation("Proxy configuration reloaded");
            return Ok(new { message = "Configuration reloaded successfully" });
        }

        /// <summary>
        /// Test một proxy cụ thể
        /// </summary>
        [HttpPost("test/{proxyId}")]
        public async Task<ActionResult> TestProxy(string proxyId)
        {
            var proxies = await _proxyManager.GetAllProxiesAsync();
            var proxy = proxies.FirstOrDefault(p => p.Id == proxyId);

            if (proxy == null)
            {
                return NotFound(new { message = $"Proxy {proxyId} not found" });
            }

            try
            {
                using var client = await _proxyManager.GetHttpClientWithProxyAsync();
                var response = await client.GetAsync("https://httpbin.org/ip");
                var content = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    proxyId,
                    success = response.IsSuccessStatusCode,
                    statusCode = response.StatusCode,
                    response = content
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    proxyId,
                    success = false,
                    error = ex.Message
                });
            }
        }
    }
}
```

### Step 3: Sử dụng trong existing controllers

```csharp
namespace SRT2Speech.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SendMessageController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProxyManager _proxyManager;
        private readonly ILogger<SendMessageController> _logger;

        public SendMessageController(
            IHttpClientFactory httpClientFactory,
            IProxyManager proxyManager,
            ILogger<SendMessageController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _proxyManager = proxyManager;
            _logger = logger;
        }

        [HttpPost("fpt/tts")]
        public async Task<IActionResult> SendToFptTTS([FromBody] TTSRequest request)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("FptAPI");

                var response = await client.PostAsJsonAsync(
                    "https://api.fpt.ai/hmi/tts/v5",
                    request);

                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("FPT TTS request completed. Proxy status: {Status}",
                    _proxyManager.GetStatusSummary());

                return Ok(new { success = true, data = content });
            }
            catch (NoAvailableProxyException ex)
            {
                _logger.LogError(ex, "No available proxy for FPT TTS");
                return StatusCode(503, new { error = "Service temporarily unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling FPT TTS API");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
```

---

## 3. Usage Examples

### Example 1: Simple GET Request với Proxy

```csharp
public class SimpleProxyExample
{
    private readonly IHttpClientFactory _httpClientFactory;

    public async Task<string> GetWithProxyAsync()
    {
        using var client = _httpClientFactory.CreateClient("MyAPIClient");

        // Request tự động sử dụng proxy rotation
        var response = await client.GetAsync("https://api.example.com/data");
        return await response.Content.ReadAsStringAsync();
    }
}
```

### Example 2: POST Request với Retry

```csharp
public class RetryProxyExample
{
    private readonly IProxyManager _proxyManager;

    public async Task<ApiResponse> PostWithRetryAsync(ApiRequest request)
    {
        // GetHttpClientWithProxyAsync tự động include retry logic
        using var client = await _proxyManager.GetHttpClientWithProxyAsync();

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Nếu request fail, sẽ tự động retry với proxy khác
        var response = await client.PostAsync(
            "https://api.example.com/process",
            content);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse>();
    }
}
```

### Example 3: Manual Proxy Selection

```csharp
public class ManualProxyExample
{
    private readonly IProxyManager _proxyManager;

    public async Task<string> GetWithSpecificProxyAsync()
    {
        // Lấy proxy theo manual selection
        var proxy = await _proxyManager.GetNextProxyAsync();

        if (proxy == null)
        {
            throw new InvalidOperationException("No proxy available");
        }

        Console.WriteLine($"Using proxy: {proxy.Host}:{proxy.Port}");

        // Tạo HttpClient với proxy cụ thể
        var handler = new ProxyHttpClientHandler(proxy);
        using var client = new HttpClient(handler);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await client.GetAsync("https://api.example.com/data");
            stopwatch.Stop();

            // Mark success
            await _proxyManager.MarkProxySuccessAsync(
                proxy.Id,
                stopwatch.Elapsed);

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Mark failure
            await _proxyManager.MarkProxyFailureAsync(
                proxy.Id,
                ex.Message);

            throw;
        }
    }
}
```

### Example 4: Monitoring và Logging

```csharp
public class ProxyMonitoringExample
{
    private readonly IProxyManager _proxyManager;
    private readonly ILogger _logger;

    public async Task MonitorProxiesAsync()
    {
        // Get all proxies
        var allProxies = await _proxyManager.GetAllProxiesAsync();

        _logger.LogInformation("=== Proxy Status Report ===");
        _logger.LogInformation(_proxyManager.GetStatusSummary());

        foreach (var proxy in allProxies)
        {
            _logger.LogInformation(
                "Proxy: {Id} | Status: {Status} | Health: {Health}% | Used: {Used} times | Avg Response: {ResponseTime}ms",
                proxy.Id,
                proxy.Status,
                proxy.Health.HealthScore,
                proxy.UsedCount,
                proxy.Health.AverageResponseTime);
        }

        // Get aggregated metrics
        var metrics = await _proxyManager.GetAggregatedMetricsAsync();

        _logger.LogInformation(
            "Total Requests: {Total} | Success: {Success} | Failed: {Failed} | Success Rate: {Rate}%",
            metrics.TotalRequests,
            metrics.SuccessfulRequests,
            metrics.FailedRequests,
            metrics.TotalRequests > 0
                ? (metrics.SuccessfulRequests * 100.0 / metrics.TotalRequests)
                : 0);
    }
}
```

---

## 4. Testing Strategy

### Unit Tests

#### Test ProxyPool

```csharp
[TestClass]
public class ProxyPoolTests
{
    [TestMethod]
    public void AddProxy_ShouldAddProxyToPool()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ProxyPool>>();
        var pool = new ProxyPool(logger);
        var proxy = new ProxyInfo
        {
            Id = "test-1",
            Host = "proxy.test.com",
            Port = 8080
        };

        // Act
        pool.AddProxy(proxy);

        // Assert
        Assert.AreEqual(1, pool.Count);
        Assert.IsNotNull(pool.GetProxy("test-1"));
    }

    [TestMethod]
    public void GetAvailableProxies_ShouldReturnOnlyAvailableProxies()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ProxyPool>>();
        var pool = new ProxyPool(logger);

        pool.AddProxy(new ProxyInfo
        {
            Id = "available",
            Status = ProxyStatus.Active
        });
        pool.AddProxy(new ProxyInfo
        {
            Id = "unhealthy",
            Status = ProxyStatus.Unhealthy
        });

        // Act
        var available = pool.GetAvailableProxies();

        // Assert
        Assert.AreEqual(1, available.Count());
        Assert.AreEqual("available", available.First().Id);
    }
```
