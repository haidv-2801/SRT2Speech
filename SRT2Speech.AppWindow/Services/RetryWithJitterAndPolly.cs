using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.AppWindow.Services
{
    internal class RetryWithJitterAndPolly
    {
        public static async Task<TResult> ExecuteWithRetryAndJitterAsync<TResult>(Func<Task<TResult>> operation, Func<TResult, bool> isResultValid, int maxRetries = 5, int baseDelayMs = 1000, double jitterFactor = 0.5)
        {
            Random jitterer = new Random();
            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<TResult>(r => !isResultValid(r))
                .WaitAndRetryAsync(maxRetries,
                  retryAttempt =>
                  {
                      var delay = baseDelayMs * TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt));
                      delay = delay + TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (jitterFactor * (new Random().NextDouble() * 2 - 1)));
                      Console.WriteLine($"Delay {delay}");
                      return delay;
                  }
              );

            return await retryPolicy.ExecuteAsync(operation);
        }

        public static async Task ExecuteWithRetryAndJitterAsync(Func<Task> operation, int maxRetries = 5, int baseDelayMs = 1000, double jitterFactor = 0.5)
        {
            await ExecuteWithRetryAndJitterAsync(() => Task.Run(operation), maxRetries, baseDelayMs, jitterFactor);
        }
    }
}
