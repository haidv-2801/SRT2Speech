using Microsoft.Extensions.DependencyInjection;
using SRT2Speech.ProxyService.HttpHandlers;

namespace SRT2Speech.ProxyService.Extensions;

/// <summary>
/// Extension methods cho IHttpClientBuilder
/// </summary>
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

    /// <summary>
    /// Thêm proxy rotation và configure settings
    /// </summary>
    public static IHttpClientBuilder WithProxy(this IHttpClientBuilder builder)
    {
        return builder
            .AddProxyRotation()
            .ConfigureForProxy();
    }
}