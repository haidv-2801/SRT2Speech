using System.Collections.Concurrent;

namespace SRT2Speech.Cache
{
    public class MemCacheOptions
    {
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxItems { get; set; } = 10000;
    }

    public class MemCacheService : IMemCacheService
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly MemCacheOptions _options;

        public MemCacheService()
        {
            _options = new MemCacheOptions();
            _cache = new ConcurrentDictionary<string, CacheItem>(Environment.ProcessorCount, _options.MaxItems);
        }

        public T Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var cacheItem))
            {
                if (cacheItem.ExpirationTime > DateTime.UtcNow)
                {
                    return (T)cacheItem.Value;
                }
                else
                {
                    _cache.TryRemove(key, out _);
                }
            }

            return default;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var item = new CacheItem
            {
                Value = value,
                ExpirationTime = DateTime.UtcNow.Add(expiration ?? _options.DefaultExpiration)
            };

            _cache.AddOrUpdate(key, item, (_, _) => item);
        }

        public bool Contains(string key)
        {
            return _cache.ContainsKey(key);
        }

        public void Remove(string key)
        {
            _cache.TryRemove(key, out _);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private class CacheItem
        {
            public object Value { get; set; }
            public DateTime ExpirationTime { get; set; }
        }
    }
}
