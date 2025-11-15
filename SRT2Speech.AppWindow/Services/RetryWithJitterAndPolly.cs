using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SRT2Speech.AppWindow.Services
{
    internal class RetryWithJitterAndPolly
    {
        public static async Task<TResult> ExecuteWithRetryAndJitterAsync<TResult>(Func<Task<TResult>> operation, Func<TResult, bool> isResultValid, int maxRetries = 5, int baseDelayMs = 1000, double jitterFactor = 0.5, Action<string>? logCallback = null)
        {
            Random jitterer = new Random();
            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>() // Proxy timeout
                .Or<SocketException>() // Network/proxy connection issues
                .Or<WebException>() // Proxy authentication/connection errors
                .Or<TaskCanceledException>(ex => ex.CancellationToken == default || !ex.CancellationToken.IsCancellationRequested) // Request timeout (không phải user cancel)
                .OrResult<TResult>(r => !isResultValid(r))
                .WaitAndRetryAsync(maxRetries,
                  retryAttempt =>
                  {
                      var delay = baseDelayMs * TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt));
                      delay = delay + TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (jitterFactor * (new Random().NextDouble() * 2 - 1)));
                      var logMsg = $"[RETRY] Attempt {retryAttempt}/{maxRetries}, delay: {delay.TotalSeconds:F2}s";
                      Console.WriteLine(logMsg);
                      logCallback?.Invoke(logMsg);
                      return delay;
                  },
                  onRetry: (outcome, timespan, retryCount, context) =>
                  {
                      string logMsg;
                      if (outcome.Exception != null)
                      {
                          logMsg = $"[RETRY_ERROR] Retry {retryCount} due to: {outcome.Exception.GetType().Name} - {outcome.Exception.Message}";
                      }
                      else
                      {
                          logMsg = $"[RETRY_INVALID] Retry {retryCount} due to invalid result";
                      }
                      Console.WriteLine(logMsg);
                      logCallback?.Invoke(logMsg);
                  }
              );

            return await retryPolicy.ExecuteAsync(operation);
        }

        public static async Task ExecuteWithRetryAndJitterAsync(Func<Task> operation, int maxRetries = 5, int baseDelayMs = 1000, double jitterFactor = 0.5)
        {
            await ExecuteWithRetryAndJitterAsync(() => Task.Run(operation), maxRetries, baseDelayMs, jitterFactor);
        }

        // Overload hỗ trợ CancellationToken để có thể hủy ngay lập tức cả thời gian chờ giữa các lần retry
        public static async Task<TResult> ExecuteWithRetryAndJitterAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            Func<TResult, bool> isResultValid,
            CancellationToken ct,
            int maxRetries = 5,
            int baseDelayMs = 1000,
            double jitterFactor = 0.5,
            Action<string>? logCallback = null)
        {
            Random jitterer = new Random();
            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>() // Proxy timeout
                .Or<SocketException>() // Network/proxy connection issues
                .Or<WebException>() // Proxy authentication/connection errors
                .Or<TaskCanceledException>(ex => !ct.IsCancellationRequested) // Request timeout (không phải user cancel)
                .OrResult<TResult>(r => !isResultValid(r))
                .WaitAndRetryAsync(
                    maxRetries,
                    retryAttempt =>
                    {
                        var baseDelay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, retryAttempt));
                        var jitter = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * (jitterFactor * (new Random().NextDouble() * 2 - 1)));
                        var delay = baseDelay + jitter;
                        var logMsg = $"[RETRY] Attempt {retryAttempt}/{maxRetries}, delay: {delay.TotalSeconds:F2}s";
                        Console.WriteLine(logMsg);
                        logCallback?.Invoke(logMsg);
                        return delay;
                    },
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        string logMsg;
                        if (outcome.Exception != null)
                        {
                            logMsg = $"[RETRY_ERROR] Retry {retryCount} due to: {outcome.Exception.GetType().Name} - {outcome.Exception.Message}";
                        }
                        else
                        {
                            logMsg = $"[RETRY_INVALID] Retry {retryCount} due to invalid result";
                        }
                        Console.WriteLine(logMsg);
                        logCallback?.Invoke(logMsg);
                    }
                );

            // Lưu ý: Không bắt TaskCanceledException/OperationCanceledException tại đây để cho phép hủy ngay lập tức
            return await retryPolicy.ExecuteAsync((token) => operation(token), ct);
        }
    }
}
