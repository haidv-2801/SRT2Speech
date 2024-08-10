using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.Cache
{
    public interface IMicrosoftCacheService
    {
        T Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null);
        void Remove(string key);
        bool TryGetValue<T>(string key, out T value);
        Task SetCacheAsync<T>(string key, T value, TimeSpan cacheDuration);
        Task<T> GetFromCacheAsync<T>(string key);
    }
    public class MicrosoftCacheService : IMicrosoftCacheService
    {
        private readonly IMemoryCache _memoryCache;

        public MicrosoftCacheService(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public T Get<T>(string key)
        {
            return _memoryCache.Get<T>(key);
        }

        public void Set<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions();

            if (absoluteExpirationRelativeToNow.HasValue)
            {
                cacheEntryOptions.SetAbsoluteExpiration(absoluteExpirationRelativeToNow.Value);
            }
            else
            {
                cacheEntryOptions.SetSlidingExpiration(TimeSpan.FromMinutes(10));
            }

            _memoryCache.Set(key, value, cacheEntryOptions);
        }

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            return _memoryCache.TryGetValue(key, out value);
        }

       
        public async Task<T> GetFromCacheAsync<T>(string key)
        {
            if (_memoryCache.TryGetValue(key, out T cacheEntry))
            {
                return await Task.FromResult(cacheEntry);
            }

            return default;
        }

      
        public async Task SetCacheAsync<T>(string key, T value, TimeSpan cacheDuration)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(cacheDuration);

            _memoryCache.Set(key, value, cacheEntryOptions);
            await Task.CompletedTask;
        }
    }
}
