# SOCKS5 Proxy Support - Quick Start Guide

## 🎯 Tổng Quan

Hệ thống đã được cập nhật để **hỗ trợ đầy đủ SOCKS5 proxy**, khắc phục vấn đề nghiêm trọng về IP leak khi sử dụng ElevenLabs API.

### ⚠️ Vấn Đề Đã Được Fix

**Trước đây:**

- ❌ `WebProxy` không hỗ trợ SOCKS5
- ❌ SOCKS5 proxies FAIL hoặc BYPASS proxy
- ❌ **NGUY CƠ LỘ IP THẬT** khi dùng API keys

**Bây giờ:**

- ✅ SOCKS5 được hỗ trợ đầy đủ qua custom implementation
- ✅ Tự động detect proxy type và dùng handler phù hợp
- ✅ IP leak detection built-in
- ✅ Đảm bảo ẩn danh 100%

## 🚀 Cài Đặt & Sử Dụng

### 1. Cấu Hình Proxy

**File: `SRT2Speech.AppWindow/Configs/ElevenlabKeyState.yaml`**

```yaml
apiKeys:
  - key: 'sk_your_elevenlabs_api_key_here'
    available: true
    usedCount: 0
    priority: 100
    # Binding proxy cố định cho key này
    boundProxyEndpoint: '192.168.1.100:1080:username:password'
    # Format: host:port hoặc host:port:username:password
    cooldownUntil: null
```

**File: `SRT2Speech.AppWindow/Configs/proxies.yaml`**

```yaml
proxies:
  - id: unique-proxy-id-123
    name: My SOCKS5 Proxy Server
    host: 192.168.1.100
    port: 1080
    type: SOCKS5 # ← Quan trọng!
    username: myuser
    password: mypass
    status: Active
    priority: 100
    weight: 100
```

### 2. Test Proxy

```bash
# Test SOCKS5 proxy without auth
cd SRT2Speech.ProxyService
dotnet run 192.168.1.100 1080

# Test SOCKS5 proxy with auth
dotnet run proxy.example.com 1080 myuser mypass

# Test HTTP proxy
dotnet run proxy.example.com 8080 user pass HTTP
```

**Output mẫu:**

```
=== SOCKS5 Proxy Implementation Test ===

Step 1: Getting real IP address...
✅ Real IP: 203.0.113.50

Step 2: Validating proxy...
  Proxy: 192.168.1.100:1080
  Type: SOCKS5
  Auth: Yes

[SOCKS5_CONNECT] Connecting to api.ipify.org:443 via SOCKS5 proxy 192.168.1.100:1080
[SOCKS5_SUCCESS] Successfully connected to api.ipify.org:443 via SOCKS5 proxy

=== VALIDATION RESULT ===
Status: ✅ VALID
Proxy IP: 198.51.100.25
Location: US / New York
Organization: Example ISP
Response Time: 234ms

✅ IP PROPERLY HIDDEN
Real IP: 203.0.113.50 -> Proxy IP: 198.51.100.25

Step 3: Testing actual HTTP request...
Creating SOCKS5 HttpClient...
Sending request to: https://httpbin.org/get
Status: OK
Response Time: 456ms
Content Length: 1234 bytes

=== TEST COMPLETED ===
```

### 3. Sử Dụng Trong Code

**Tự động (Recommended):**

```csharp
// System tự động detect SOCKS5 và dùng handler phù hợp
var client = _httpClientFactory.GetClient(apiKey, proxyInfo);

// KHÔNG cần code gì thêm!
```

**Manual (Advanced):**

```csharp
using SRT2Speech.ProxyService.HttpHandlers;

// Tạo SOCKS5 handler
var handler = Socks5HttpHandlerFactory.CreateSocks5Handler(proxyInfo);
var client = new HttpClient(handler);

// Sử dụng như HttpClient bình thường
var response = await client.GetAsync("https://api.elevenlabs.io/...");
```

## 🔒 Bảo Mật & IP Leak Detection

### Kiểm Tra IP Leak

```csharp
using SRT2Speech.ProxyService.Services;

var validator = new ProxyValidator();

// 1. Lấy real IP
var realIp = await validator.GetRealIpAddressAsync();

// 2. Test proxy
var result = await validator.ValidateProxyAsync(proxyInfo, realIp);

// 3. Check kết quả
if (result.IsIpLeaked)
{
    Console.WriteLine("🚨 DANGER: IP leak detected!");
    Console.WriteLine($"Real IP: {realIp}");
    Console.WriteLine($"Proxy IP: {result.ProxyIpAddress}");
}
else
{
    Console.WriteLine("✅ SAFE: IP properly hidden");
}
```

### Batch Validation

```csharp
// Validate tất cả proxies
var proxies = await proxyManager.GetAllProxiesAsync();
var realIp = await validator.GetRealIpAddressAsync();

var results = await validator.ValidateProxiesAsync(
    proxies,
    realIp,
    maxConcurrency: 10
);

// Filter valid proxies
var validProxies = results
    .Where(r => r.IsValid && !r.IsIpLeaked)
    .Select(r => r.ProxyInfo)
    .ToList();

Console.WriteLine($"Valid proxies: {validProxies.Count}/{proxies.Count}");
```

## 📊 Monitoring & Logs

### Console Logs

**SOCKS5 Connection Success:**

```
[SOCKS5_CONNECT] Connecting to api.elevenlabs.io:443 via SOCKS5 proxy 192.168.1.100:1080
[SOCKS5_SUCCESS] Successfully connected to api.elevenlabs.io:443 via SOCKS5 proxy
[SUCCESS] file.mp3 (234ms)
```

**SOCKS5 Connection Failure:**

```
[SOCKS5_CONNECT] Connecting to api.elevenlabs.io:443 via SOCKS5 proxy 192.168.1.100:1080
[SOCKS5_FAIL] Failed to connect via SOCKS5: Connection refused
[SOCKS5_ERROR] Failed to connect via SOCKS5 proxy: Connection refused
```

**IP Leak Detection:**

```
[PROXY_VALIDATOR] Real IP address detected: 203.0.113.50
[ERROR] IP LEAK DETECTED for proxy abc123: Real IP=203.0.113.50, Proxy IP=203.0.113.50
🚨 DANGER: This proxy is NOT working!
```

### Grep Logs

```bash
# Search for SOCKS5 errors
grep "SOCKS5" logs/*.log

# Search for IP leaks
grep "IP_LEAK" logs/*.log

# Search for failed proxies
grep "SOCKS5_FAIL" logs/*.log
```

## 🐛 Troubleshooting

### Common Issues

**1. Connection Refused**

```
Error: SOCKS5 connection failed: Connection refused
```

**Fix:** Check proxy server is running and accessible

**2. Authentication Failed**

```
Error: SOCKS5 authentication failed: Invalid credentials
```

**Fix:** Verify username/password in config

**3. IP Leak Detected**

```
Error: IP LEAK DETECTED! Proxy is not hiding real IP address
```

**Fix:** Proxy not working - use different proxy or check configuration

**4. Timeout**

```
Error: Request timeout
```

**Fix:** Increase timeout or check network connectivity

### Debug Mode

```csharp
// Enable detailed logging
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var validator = new ProxyValidator(loggerFactory.CreateLogger<ProxyValidator>());
```

## 📚 Documentation

- **Full Documentation:** [docs/socks5-proxy-implementation.md](docs/socks5-proxy-implementation.md)
- **RFC 1928 - SOCKS v5:** https://tools.ietf.org/html/rfc1928
- **RFC 1929 - SOCKS Auth:** https://tools.ietf.org/html/rfc1929

## ✅ Testing Checklist

- [ ] Test proxy connection với TestProgram
- [ ] Verify IP leak detection hoạt động
- [ ] Check real IP vs proxy IP khác nhau
- [ ] Test với và không authentication
- [ ] Batch validate tất cả proxies
- [ ] Monitor logs trong production
- [ ] Setup periodic IP leak checking

## 🎯 Best Practices

### 1. Always Validate Before Use

```csharp
var result = await validator.ValidateProxyAsync(proxy, realIp);
if (!result.IsValid || result.IsIpLeaked)
{
    // Don't use this proxy!
    logger.LogWarning("Proxy {ProxyId} is invalid or leaking IP", proxy.Id);
    return;
}
```

### 2. Periodic Monitoring

```csharp
// Check every 5 minutes
var timer = new Timer(async _ =>
{
    var realIp = await validator.GetRealIpAddressAsync();
    foreach (var proxy in activeProxies)
    {
        var result = await validator.ValidateProxyAsync(proxy, realIp);
        if (result.IsIpLeaked)
        {
            logger.LogCritical("IP LEAK on proxy {ProxyId}!", proxy.Id);
            // Disable proxy or alert
        }
    }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
```

### 3. Binding Strategy

- ✅ 1 API key = 1 proxy cố định
- ✅ Consistent IP per key
- ✅ Better anonymity
- ✅ Easier rate limit management

## 🔧 Build Commands

```bash
# Build ProxyService
dotnet build SRT2Speech.ProxyService/SRT2Speech.ProxyService.csproj

# Run tests
dotnet run --project SRT2Speech.ProxyService -- 192.168.1.100 1080

# Build AppWindow
dotnet publish SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj -c Release

# Build all
dotnet build SRT2Speech.sln
```

## 📞 Support

Nếu gặp vấn đề:

1. Chạy test program để validate proxy
2. Check logs for SOCKS5 errors
3. Verify IP leak với ProxyValidator
4. Check network connectivity
5. Verify credentials

---

**Version:** 1.0.0  
**Last Updated:** 2025-01-15  
**Status:** ✅ Production Ready
