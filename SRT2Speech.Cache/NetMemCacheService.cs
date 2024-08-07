//using System;
//using Microsoft.Extensions.Caching.Memory;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;

//namespace SRT2Speech.Cache
//{
//    public class NetMemCacheService : IMemCacheService
//    {
//        private readonly IMemoryCache _memoryCache;
//        private readonly MemCacheOptions _options;

//        public MemCacheService(IMemoryCache memoryCache)
//        {
//            _memoryCache = memoryCache;
//            _options = new MemCacheOptions();
//        }

//        public T Get<T>(string key)
//        {
//            if (_memoryCache.TryGetValue(key, out T value))
//            {
//                return value;
//            }

//            return default;
//        }

//        public void Set<T>(string key, T value, TimeSpan? expiration = null)
//        {
//            var options = new MemoryCacheEntryOptions
//            {
//                AbsoluteExpiration = DateTime.UtcNow.Add(expiration ?? _options.DefaultExpiration),
//                Priority = CacheItemPriority.Normal,
//                SlidingExpiration = expiration ?? _options.DefaultExpiration
//            };

//            _memoryCache.Set(key, value, options);
//        }

//        public bool Contains(string key)
//        {
//            return _memoryCache.TryGetValue(key, out _);
//        }

//        public void Remove(string key)
//        {
//            _memoryCache.Remove(key);
//        }

//        public void Clear()
//        {
//            _memoryCache.Dispose();
//        }
//    }
//}
