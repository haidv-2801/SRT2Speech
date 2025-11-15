using Microsoft.Extensions.Logging;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.HttpHandlers;
using SRT2Speech.ProxyService.Models;
using SRT2Speech.ProxyService.Services;

namespace SRT2Speech.ProxyService;

/// <summary>
/// Test program để validate SOCKS5 proxy implementation
/// Run: dotnet run --project SRT2Speech.ProxyService
/// </summary>
public class TestProgram
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== SOCKS5 Proxy Implementation Test ===\n");

        // Parse arguments
        var testProxy = ParseArguments(args);
        
        if (testProxy == null)
        {
            ShowUsage();
            return;
        }

        // Create logger factory
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<ProxyValidator>();
        var validator = new ProxyValidator(logger);

        Console.WriteLine("Step 1: Getting real IP address...");
        var realIp = await validator.GetRealIpAddressAsync();
        
        if (realIp == null)
        {
            Console.WriteLine("❌ Failed to get real IP address");
            return;
        }
        
        Console.WriteLine($"✅ Real IP: {realIp}\n");

        Console.WriteLine("Step 2: Validating proxy...");
        Console.WriteLine($"  Proxy: {testProxy.Host}:{testProxy.Port}");
        Console.WriteLine($"  Type: {testProxy.Type}");
        Console.WriteLine($"  Auth: {(!string.IsNullOrEmpty(testProxy.Username) ? "Yes" : "No")}\n");

        var result = await validator.ValidateProxyAsync(testProxy, realIp);

        Console.WriteLine("\n=== VALIDATION RESULT ===");
        Console.WriteLine($"Status: {(result.IsValid ? "✅ VALID" : "❌ INVALID")}");
        
        if (result.IsValid)
        {
            Console.WriteLine($"Proxy IP: {result.ProxyIpAddress}");
            Console.WriteLine($"Location: {result.Country ?? "Unknown"} / {result.City ?? "Unknown"}");
            Console.WriteLine($"Organization: {result.Organization ?? "Unknown"}");
            Console.WriteLine($"Response Time: {result.ResponseTime.TotalMilliseconds:F0}ms");
            
            if (result.IsIpLeaked)
            {
                Console.WriteLine("\n🚨 IP LEAK DETECTED! 🚨");
                Console.WriteLine($"Real IP: {result.RealIpAddress}");
                Console.WriteLine($"Proxy IP: {result.ProxyIpAddress}");
                Console.WriteLine("⚠️ This proxy is NOT hiding your real IP address!");
            }
            else
            {
                Console.WriteLine("\n✅ IP PROPERLY HIDDEN");
                Console.WriteLine($"Real IP: {realIp} -> Proxy IP: {result.ProxyIpAddress}");
            }
        }
        else
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }

        // Test actual HTTP request through proxy
        Console.WriteLine("\n\nStep 3: Testing actual HTTP request...");
        await TestHttpRequestAsync(testProxy);

        Console.WriteLine("\n=== TEST COMPLETED ===");
    }

    private static ProxyInfo? ParseArguments(string[] args)
    {
        if (args.Length < 2)
            return null;

        var host = args[0];
        
        if (!int.TryParse(args[1], out var port))
        {
            Console.WriteLine("Invalid port number");
            return null;
        }

        var proxy = new ProxyInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Test Proxy {host}:{port}",
            Host = host,
            Port = port,
            Type = ProxyType.SOCKS5, // Default to SOCKS5
            Priority = 100,
            Weight = 100
        };

        // Optional username and password
        if (args.Length >= 4)
        {
            proxy.Username = args[2];
            proxy.Password = args[3];
        }

        // Optional proxy type
        if (args.Length >= 5)
        {
            if (Enum.TryParse<ProxyType>(args[4], true, out var proxyType))
            {
                proxy.Type = proxyType;
            }
        }

        return proxy;
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run <host> <port> [username] [password] [type]");
        Console.WriteLine("\nExamples:");
        Console.WriteLine("  dotnet run 192.168.1.100 1080");
        Console.WriteLine("  dotnet run proxy.example.com 1080 myuser mypass");
        Console.WriteLine("  dotnet run proxy.example.com 8080 user pass HTTP");
        Console.WriteLine("\nProxy Types:");
        Console.WriteLine("  - SOCKS5 (default)");
        Console.WriteLine("  - HTTP");
        Console.WriteLine("  - HTTPS");
    }

    private static async Task TestHttpRequestAsync(ProxyInfo proxyInfo)
    {
        try
        {
            HttpClient client;
            
            if (proxyInfo.Type == ProxyType.SOCKS5)
            {
                Console.WriteLine("Creating SOCKS5 HttpClient...");
                var handler = Socks5HttpHandlerFactory.CreateSocks5Handler(proxyInfo);
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            }
            else
            {
                Console.WriteLine("Creating HTTP/HTTPS HttpClient...");
                var handler = new ProxyHttpClientHandler(proxyInfo);
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            }

            using (client)
            {
                var testUrl = "https://httpbin.org/get";
                Console.WriteLine($"Sending request to: {testUrl}");
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await client.GetAsync(testUrl);
                stopwatch.Stop();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response Time: {stopwatch.ElapsedMilliseconds}ms");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Content Length: {content.Length} bytes");
                    
                    // Show first 500 characters of response
                    if (content.Length > 500)
                        content = content.Substring(0, 500) + "...";
                    
                    Console.WriteLine("\nResponse Preview:");
                    Console.WriteLine(content);
                }
                else
                {
                    Console.WriteLine($"❌ Request failed: {response.StatusCode}");
                }
            }
        }
        catch (ProxyConnectionException ex)
        {
            Console.WriteLine($"❌ SOCKS5 Connection Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}: {ex.Message}");
        }
    }
}