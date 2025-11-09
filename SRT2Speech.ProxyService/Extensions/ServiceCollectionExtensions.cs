using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.HttpHandlers;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;
using SRT2Speech.ProxyService.Services;
using SRT2Speech.ProxyService.Strategies;

namespace SRT2Speech.ProxyService.Extensions;

/// <summary>
/// Extension methods cho IServiceCollection
/// </summary>
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
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProxyServiceSettings>>().Value;
            var metricsCollector = sp.GetRequiredService<IProxyMetricsCollector>();

            return settings.DefaultStrategy switch
            {
                RotationStrategy.RoundRobin => new RoundRobinStrategy(),
                RotationStrategy.Random => new RandomStrategy(),
                RotationStrategy.LeastUsed => new LeastUsedStrategy(),
                RotationStrategy.Weighted => new WeightedStrategy(),
                RotationStrategy.Smart => new SmartStrategy(metricsCollector),
                _ => new RoundRobinStrategy()
            };
        });

        // Register ProxyRetryHandler
        services.AddTransient<ProxyRetryHandler>();

        // Load initial proxy configuration
        LoadProxyConfiguration(services, configuration);

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

    /// <summary>
    /// Load proxy configuration từ file
    /// </summary>
    private static void LoadProxyConfiguration(IServiceCollection services, IConfiguration configuration)
    {
        var configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Configs",
            "proxies.yaml");

        if (File.Exists(configPath))
        {
            try
            {
                var yaml = File.ReadAllText(configPath);
                var config = YamlUtility.Deserialize<ProxyConfiguration>(yaml);

                // Create a temporary service provider to get the proxy pool
                var serviceProvider = services.BuildServiceProvider();
                var proxyPool = serviceProvider.GetService<IProxyPool>();

                if (proxyPool != null)
                {
                    foreach (var proxy in config.Proxies)
                    {
                        proxyPool.AddProxy(proxy);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow app to start without proxies
                Console.WriteLine($"Warning: Could not load proxy configuration: {ex.Message}");
            }
        }
    }
}