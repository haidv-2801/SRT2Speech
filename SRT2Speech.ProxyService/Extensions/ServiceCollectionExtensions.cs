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
using YamlDotNet.Serialization.NamingConventions;

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
        services.AddSingleton<IProxyManager, ProxyManager>();
        services.AddSingleton<IProxyPool, ProxyPool>();
        services.AddSingleton<IProxyMetricsCollector, ProxyMetricsCollector>();

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
}