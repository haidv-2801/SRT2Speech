using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.HttpHandlers;

/// <summary>
/// DelegatingHandler tích hợp Polly retry với proxy rotation
/// </summary>
public class ProxyRetryHandler : DelegatingHandler
{
    private readonly IProxyManager _proxyManager;
    private readonly ILogger<ProxyRetryHandler> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
    private static readonly ResiliencePropertyKey<string> ProxyIdKey = new("ProxyId");

    public ProxyRetryHandler(
        IProxyManager proxyManager,
        IOptions<ProxyServiceSettings> settings,
        ILogger<ProxyRetryHandler> logger)
    {
        _proxyManager = proxyManager ?? throw new ArgumentNullException(nameof(proxyManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settingsValue = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        // Configure Polly v8 retry pipeline
        _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = settingsValue.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(settingsValue.RetryDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => ShouldRetry(r.StatusCode)),
                OnRetry = async args =>
                {
                    var proxyId = args.Context.Properties.TryGetValue(ProxyIdKey, out var id)
                        ? id
                        : "unknown";

                    _logger.LogWarning(
                        "Retry {AttemptNumber}/{MaxRetries} cho proxy {ProxyId} sau {Delay}ms",
                        args.AttemptNumber, settingsValue.MaxRetries, proxyId, args.RetryDelay.TotalMilliseconds);

                    // Mark proxy as failed
                    if (!string.IsNullOrEmpty(proxyId) && proxyId != "unknown")
                    {
                        var reason = args.Outcome.Exception?.Message ??
                                   $"HTTP {args.Outcome.Result?.StatusCode}";
                        await _proxyManager.MarkProxyFailureAsync(proxyId, reason);
                    }

                    // Get new proxy for retry
                    var newProxy = await _proxyManager.GetNextProxyAsync();
                    if (newProxy != null)
                    {
                        args.Context.Properties.Set(ProxyIdKey, newProxy.Id);
                        _logger.LogInformation("Chuyển sang proxy mới: {ProxyId}", newProxy.Id);
                    }
                }
            })
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = ResilienceContextPool.Shared.Get(cancellationToken);

        try
        {
            // Get initial proxy
            var proxy = await _proxyManager.GetNextProxyAsync(cancellationToken);
            if (proxy != null)
            {
                context.Properties.Set(ProxyIdKey, proxy.Id);
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await _retryPipeline.ExecuteAsync(
                    async ctx => await base.SendAsync(request, cancellationToken),
                    context);

                stopwatch.Stop();

                // Mark success
                if (context.Properties.TryGetValue(ProxyIdKey, out var proxyId))
                {
                    await _proxyManager.MarkProxySuccessAsync(proxyId, stopwatch.Elapsed);
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Mark final failure
                if (context.Properties.TryGetValue(ProxyIdKey, out var proxyId))
                {
                    await _proxyManager.MarkProxyFailureAsync(proxyId, ex.Message);
                }

                throw;
            }
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    /// <summary>
    /// Kiểm tra có nên retry với status code này không
    /// </summary>
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