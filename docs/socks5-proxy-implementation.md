# SOCKS5 Proxy Implementation Guide

## Tổng Quan

Hệ thống đã được cập nhật để hỗ trợ đầy đủ SOCKS5 proxy, khắc phục vấn đề nghiêm trọng trước đây khi `WebProxy` không hỗ trợ SOCKS5 natively.

## Vấn Đề Trước Đây

### ⚠️ CRITICAL BUG

- `ProxyHttpClientHandler` sử dụng `WebProxy` - chỉ hỗ trợ HTTP/HTTPS proxy
- SOCKS5 proxies **KHÔNG hoạt động** với `WebProxy`
- Requests có thể **BYPASS proxy** và lộ IP thật của user
- **Nguy cơ mất ẩn danh** khi sử dụng API keys

## Giải Pháp Mới

### 1. Socks5HttpHandlerFactory

**File:** `SRT2Speech.ProxyService/HttpHandlers/Socks5HttpClientHandler.cs`

Factory class tạo `SocketsHttpHandler` với custom `ConnectCallback` để:

- Kết nối trực tiếp đến SOCKS5 proxy server
- Thực hiện SOCKS5 handshake protocol (RFC 1928)
- Hỗ trợ authentication (username/password)
- Handle tất cả SOCKS5 reply codes

**Usage:**

```csharp
var handler = Socks5HttpHandlerFactory.CreateSocks5Handler(proxyInfo);
var client = new HttpClient(handler);
```

### 2. SOCKS5 Handshake Protocol

Implementation tuân thủ RFC 1928:

**Step 1: Greeting**

```
Client -> Server: [Version, NumMethods, Methods...]
Server -> Client: [Version, SelectedMethod]
```

**Step 2: Authentication (nếu cần)**

```
Client -> Server: [Version, UsernameLen, Username, PasswordLen, Password]
Server -> Client: [Version, Status]
```

**Step 3: Connection Request**

```
Client -> Server: [Version, Command, Reserved, AddressType, Address, Port]
Server -> Client: [Version, Reply, Reserved, AddressType, BoundAddress, BoundPort]
```

### 3. Auto-Detection Proxy Type

**ElevenLabsHttpClientFactory** tự động detect proxy type:

```csharp
if (_proxy.Type == ProxyType.SOCKS5)
{
    // Sử dụng SOCKS5 handler
    var handler = Socks5HttpHandlerFactory.CreateSocks5Handler(_proxy);
    client = new HttpClient(handler);
}
else
{
    // Sử dụng HTTP/HTTPS handler
    var handler = new ProxyHttpClientHandler(_proxy);
    client = new HttpClient(handler);
}
```

### 4. ProxyValidator - IP Leak Detection

**File:** `SRT2Speech.ProxyService/Services/ProxyValidator.cs`

Utility để validate proxy và detect IP leaks:

```csharp
var validator = new ProxyValidator(logger);

// Lấy real IP
var realIp = await validator.GetRealIpAddressAsync();

// Validate proxy
var result = await validator.ValidateProxyAsync(proxyInfo, realIp);

if (result.IsIpLeaked)
{
    Console.WriteLine("⚠️ IP LEAK DETECTED!");
}
```

**Features:**

- ✅ Kiểm tra proxy hoạt động
- ✅ Detect IP leaks (so sánh với real IP)
- ✅ Measure response time
- ✅ Get geo-location (country, city)
- ✅ Batch validation với concurrency control

## Cấu Hình

### ElevenlabKeyState.yaml

```yaml
apiKeys:
  - key: 'sk_your_api_key_here'
    available: true
    usedCount: 0
    priority: 100
    boundProxyEndpoint: '192.168.1.100:1080:username:password'
    # Format: host:port hoặc host:port:username:password
    cooldownUntil: null
```

### proxies.yaml

```yaml
proxies:
  - id: unique-id
    name: My SOCKS5 Proxy
    host: 192.168.1.100
    port: 1080
    type: SOCKS5 # Quan trọng!
    username: myuser
    password: mypass
    status: Active
```

## Testing

### 1. Test Proxy Connection

```csharp
var validator = new ProxyValidator();
var result = await validator.ValidateProxyAsync(proxyInfo);

Console.WriteLine(result.ToString());
// Output: [VALID] 192.168.1.100:1080 -> IP: 203.0.113.1, Location: US/New York, Response: 234ms
```

### 2. Test IP Leak

```csharp
// Lấy real IP
var realIp = await validator.GetRealIpAddressAsync();
Console.WriteLine($"Real IP: {realIp}");

// Test proxy
var result = await validator.ValidateProxyAsync(proxyInfo, realIp);

if (result.IsIpLeaked)
{
    Console.WriteLine("❌ DANGER: IP leak detected!");
    Console.WriteLine($"Real IP: {result.RealIpAddress}");
    Console.WriteLine($"Proxy IP: {result.ProxyIpAddress}");
}
else
{
    Console.WriteLine("✅ SAFE: IP properly hidden");
}
```

### 3. Batch Test Multiple Proxies

```csharp
var proxies = await proxyManager.GetAllProxiesAsync();
var realIp = await validator.GetRealIpAddressAsync();

var results = await validator.ValidateProxiesAsync(
    proxies,
    realIp,
    maxConcurrency: 10
);

foreach (var result in results.Where(r => r.IsValid))
{
    Console.WriteLine(result);
}
```

## Logging

### Console Output Examples

**SOCKS5 Connection Success:**

```
[SOCKS5_CONNECT] Connecting to api.elevenlabs.io:443 via SOCKS5 proxy 192.168.1.100:1080
[SOCKS5_SUCCESS] Successfully connected to api.elevenlabs.io:443 via SOCKS5 proxy
```

**SOCKS5 Connection Failure:**

```
[SOCKS5_CONNECT] Connecting to api.elevenlabs.io:443 via SOCKS5 proxy 192.168.1.100:1080
[SOCKS5_FAIL] Failed to connect via SOCKS5: Connection refused
[SOCKS5_ERROR] Failed to connect via SOCKS5 proxy 192.168.1.100:1080: Connection refused
```

**IP Leak Detection:**

```
[PROXY_VALIDATOR] Real IP address detected: 203.0.113.50
[PROXY_VALIDATOR] Validating proxy abc123 (192.168.1.100:1080) type SOCKS5
[ERROR] IP LEAK DETECTED for proxy abc123: Real IP=203.0.113.50, Proxy IP=203.0.113.50
```

## Error Handling

### SOCKS5 Error Codes

| Code | Message                      | Meaning                |
| ---- | ---------------------------- | ---------------------- |
| 0x00 | Success                      | Connection established |
| 0x01 | General SOCKS server failure | Server error           |
| 0x02 | Connection not allowed       | Ruleset blocked        |
| 0x03 | Network unreachable          | Network issue          |
| 0x04 | Host unreachable             | Target host down       |
| 0x05 | Connection refused           | Target refused         |
| 0x06 | TTL expired                  | Timeout                |
| 0x07 | Command not supported        | Invalid command        |
| 0x08 | Address type not supported   | Invalid address type   |

### Common Issues

**1. Authentication Failed**

```
Error: SOCKS5 authentication failed: Invalid credentials
Solution: Check username/password in proxy configuration
```

**2. Connection Timeout**

```
Error: Request timeout
Solution: Increase timeout or check proxy server availability
```

**3. IP Leak Detected**

```
Error: IP LEAK DETECTED! Proxy is not hiding real IP address
Solution: Proxy not working - check configuration or use different proxy
```

## Security Best Practices

### 1. Always Validate Proxies Before Use

```csharp
var validator = new ProxyValidator();
var realIp = await validator.GetRealIpAddressAsync();

foreach (var proxy in proxies)
{
    var result = await validator.ValidateProxyAsync(proxy, realIp);

    if (!result.IsValid || result.IsIpLeaked)
    {
        // Don't use this proxy
        await proxyManager.MarkProxyFailureAsync(proxy.Id, result.ErrorMessage);
    }
}
```

### 2. Monitor for IP Leaks

```csharp
// Periodic check
var timer = new Timer(async _ =>
{
    var realIp = await validator.GetRealIpAddressAsync();
    var activeProxy = await proxyManager.GetNextProxyAsync();
    var result = await validator.ValidateProxyAsync(activeProxy, realIp);

    if (result.IsIpLeaked)
    {
        // ALERT: IP leak detected!
        logger.LogCritical("IP LEAK DETECTED on active proxy!");
    }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
```

### 3. Binding Strategy

**Current Implementation:**

- ✅ 1 API key = 1 proxy cố định (via `BoundProxyEndpoint`)
- ✅ Không rotation = tăng ẩn danh
- ✅ Cùng key luôn từ cùng IP

**Advantages:**

- Consistent IP per API key
- Harder to detect as bot
- Better rate limit handling

**Disadvantages:**

- Single point of failure (nếu proxy die)
- Không tận dụng proxy pool

## Migration Guide

### Từ HTTP/HTTPS sang SOCKS5

**1. Update proxy configuration:**

```yaml
# Trước
type: HTTP

# Sau
type: SOCKS5
```

**2. No code changes needed!**

- System tự động detect và sử dụng đúng handler

**3. Validate after migration:**

```bash
# Run validation
dotnet run --project SRT2Speech.AppWindow -- validate-proxies
```

## Performance

### Benchmarks

**SOCKS5 vs HTTP/HTTPS:**

- Connection overhead: +50-100ms (handshake)
- Request latency: Similar once connected
- Connection pooling: Hiệu quả tương đương

**Recommendations:**

- Enable connection pooling (already implemented)
- Use `PooledConnectionLifetime` = 10 minutes
- `MaxConnectionsPerServer` = 10

## Troubleshooting

### Check Logs

```bash
# Search for SOCKS5 errors
grep "SOCKS5" logs/app.log

# Search for IP leaks
grep "IP_LEAK" logs/app.log
```

### Test Individual Proxy

```csharp
var testProxy = new ProxyInfo
{
    Host = "192.168.1.100",
    Port = 1080,
    Type = ProxyType.SOCKS5,
    Username = "user",
    Password = "pass"
};

var validator = new ProxyValidator();
var result = await validator.ValidateProxyAsync(testProxy);
Console.WriteLine(result);
```

## References

- [RFC 1928 - SOCKS Protocol Version 5](https://tools.ietf.org/html/rfc1928)
- [RFC 1929 - Username/Password Authentication](https://tools.ietf.org/html/rfc1929)
- [SocketsHttpHandler Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler)

## Support

Nếu gặp vấn đề:

1. Check logs for SOCKS5 errors
2. Validate proxy với ProxyValidator
3. Test IP leak detection
4. Check proxy credentials và network

---

**Last Updated:** 2025-01-15
**Version:** 1.0.0
