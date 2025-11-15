using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.ProxyService.HttpHandlers;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.AppWindow.Services
{
    /// <summary>
    /// Factory để tạo và quản lý HttpClient cho ElevenLabs API
    /// Sử dụng pooling để tránh tạo mới HttpClient mỗi request
    /// </summary>
    public class ElevenLabsHttpClientFactory : IDisposable
    {
        private readonly Dictionary<string, HttpClientPool> _pools;
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public ElevenLabsHttpClientFactory()
        {
            _pools = new Dictionary<string, HttpClientPool>();
            // Setup cleanup timer để dọn dẹp các client không sử dụng sau 5 phút
            _cleanupTimer = new Timer(CleanupUnusedClients, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Lấy HttpClient cho API key và proxy cụ thể
        /// </summary>
        public HttpClient GetClient(string apiKey, ProxyInfo proxy)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            if (proxy == null)
                throw new ArgumentNullException(nameof(proxy));

            var poolKey = GetPoolKey(apiKey, proxy);
            
            lock (_lock)
            {
                if (!_pools.TryGetValue(poolKey, out var pool))
                {
                    pool = new HttpClientPool(apiKey, proxy);
                    _pools[poolKey] = pool;
                }

                return pool.GetClient();
            }
        }

        /// <summary>
        /// Lấy HttpClient không proxy cho API key (vẫn dùng pooling)
        /// </summary>
        public HttpClient GetClientWithoutProxy(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            var poolKey = GetNoProxyPoolKey(apiKey);
            
            lock (_lock)
            {
                if (!_pools.TryGetValue(poolKey, out var pool))
                {
                    pool = new HttpClientPool(apiKey, null); // null proxy = không proxy
                    _pools[poolKey] = pool;
                }

                return pool.GetClient();
            }
        }

        /// <summary>
        /// Trả HttpClient về pool để tái sử dụng
        /// </summary>
        public void ReturnClient(HttpClient client, string apiKey, ProxyInfo proxy)
        {
            if (client == null || string.IsNullOrWhiteSpace(apiKey) || proxy == null)
                return;

            var poolKey = GetPoolKey(apiKey, proxy);
            
            lock (_lock)
            {
                if (_pools.TryGetValue(poolKey, out var pool))
                {
                    pool.ReturnClient(client);
                }
            }
        }

        /// <summary>
        /// Trả HttpClient không proxy về pool
        /// </summary>
        public void ReturnClientWithoutProxy(HttpClient client, string apiKey)
        {
            if (client == null || string.IsNullOrWhiteSpace(apiKey))
                return;

            var poolKey = GetNoProxyPoolKey(apiKey);
            
            lock (_lock)
            {
                if (_pools.TryGetValue(poolKey, out var pool))
                {
                    pool.ReturnClient(client);
                }
            }
        }

        /// <summary>
        /// Đánh dấu proxy bị lỗi để tất cả clients trong pool được xử lý
        /// </summary>
        public void MarkProxyError(ProxyInfo proxy, string error)
        {
            if (proxy == null) return;

            lock (_lock)
            {
                var affectedPools = _pools.Where(p => p.Value.Proxy.Host == proxy.Host && p.Value.Proxy.Port == proxy.Port).ToList();
                
                foreach (var pool in affectedPools)
                {
                    pool.Value.MarkProxyError(error);
                }
            }
        }

        /// <summary>
        /// Lấy thống kê về các pools
        /// </summary>
        public string GetPoolStatistics()
        {
            lock (_lock)
            {
                var totalPools = _pools.Count;
                var totalClients = _pools.Values.Sum(p => p.TotalClients);
                var availableClients = _pools.Values.Sum(p => p.AvailableClients);
                var activeClients = totalClients - availableClients;

                return $"Pools: {totalPools}, Total Clients: {totalClients}, Active: {activeClients}, Available: {availableClients}";
            }
        }

        private string GetPoolKey(string apiKey, ProxyInfo proxy)
        {
            // Tạo key duy nhất cho pool dựa trên API key và proxy
            var proxyKey = $"{proxy.Host}:{proxy.Port}";
            var apiKeyHash = apiKey.GetHashCode().ToString("X");
            return $"{apiKeyHash}_{proxyKey}";
        }

        private string GetNoProxyPoolKey(string apiKey)
        {
            // Tạo key duy nhất cho pool không proxy
            var apiKeyHash = apiKey.GetHashCode().ToString("X");
            return $"nopool_{apiKeyHash}";
        }

        private void CleanupUnusedClients(object? state)
        {
            if (_disposed) return;

            lock (_lock)
            {
                var poolsToRemove = new List<string>();

                foreach (var kvp in _pools)
                {
                    var pool = kvp.Value;
                    
                    // Nếu pool không có clients active trong 10 phút, xóa pool
                    if (pool.LastUsedAt < DateTime.UtcNow.AddMinutes(-10))
                    {
                        pool.Dispose();
                        poolsToRemove.Add(kvp.Key);
                    }
                    else
                    {
                        // Cleanup các clients không sử dụng trong pool
                        pool.CleanupUnusedClients();
                    }
                }

                // Xóa các pools không sử dụng
                foreach (var key in poolsToRemove)
                {
                    _pools.Remove(key);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cleanupTimer?.Dispose();

            lock (_lock)
            {
                foreach (var pool in _pools.Values)
                {
                    pool.Dispose();
                }
                _pools.Clear();
            }
        }

        /// <summary>
        /// Pool để quản lý các HttpClient cho một API key và proxy cụ thể
        /// </summary>
        private class HttpClientPool : IDisposable
        {
            private readonly Queue<HttpClient> _availableClients;
            private readonly HashSet<HttpClient> _activeClients;
            private readonly string _apiKey;
            private readonly ProxyInfo _proxy;
            private readonly object _poolLock = new object();
            private bool _disposed = false;
            private string _lastError = string.Empty;

            public DateTime LastUsedAt { get; private set; } = DateTime.UtcNow;
            public int TotalClients => _availableClients.Count + _activeClients.Count;
            public int AvailableClients => _availableClients.Count;
            public ProxyInfo Proxy => _proxy;

            public HttpClientPool(string apiKey, ProxyInfo? proxy)
            {
                _apiKey = apiKey;
                _proxy = proxy;
                _availableClients = new Queue<HttpClient>();
                _activeClients = new HashSet<HttpClient>();
            }

            public HttpClient GetClient()
            {
                lock (_poolLock)
                {
                    HttpClient client;

                    if (_availableClients.Count > 0)
                    {
                        client = _availableClients.Dequeue();
                    }
                    else
                    {
                        // Tạo client mới nếu không có sẵn
                        client = CreateNewClient();
                    }

                    _activeClients.Add(client);
                    LastUsedAt = DateTime.UtcNow;
                    return client;
                }
            }

            public void ReturnClient(HttpClient client)
            {
                if (client == null) return;

                lock (_poolLock)
                {
                    if (_activeClients.Contains(client))
                    {
                        _activeClients.Remove(client);
                        
                        // Nếu client không có lỗi và pool chưa bị dispose, trả về pool
                        if (!_disposed && string.IsNullOrEmpty(_lastError))
                        {
                            _availableClients.Enqueue(client);
                        }
                        else
                        {
                            // Dispose client nếu có lỗi hoặc pool bị dispose
                            try
                            {
                                client.Dispose();
                            }
                            catch { /* ignore */ }
                        }
                    }
                }
            }

            public void MarkProxyError(string error)
            {
                lock (_poolLock)
                {
                    _lastError = error;
                    
                    // Dispose tất cả clients hiện tại
                    foreach (var client in _availableClients)
                    {
                        try
                        {
                            client.Dispose();
                        }
                        catch { /* ignore */ }
                    }
                    _availableClients.Clear();
                }
            }

            public void CleanupUnusedClients()
            {
                lock (_poolLock)
                {
                    // Gi lại tối đa 5 clients trong pool, dispose các client thừa
                    while (_availableClients.Count > 5)
                    {
                        var client = _availableClients.Dequeue();
                        try
                        {
                            client.Dispose();
                        }
                        catch { /* ignore */ }
                    }
                }
            }

            private HttpClient CreateNewClient()
            {
                HttpClient client;
                
                if (_proxy != null)
                {
                    // Tạo client với proxy - hỗ trợ cả HTTP/HTTPS và SOCKS5
                    if (_proxy.Type == SRT2Speech.ProxyService.Enums.ProxyType.SOCKS5)
                    {
                        // Sử dụng SOCKS5 handler
                        Console.WriteLine($"[HTTP_CLIENT_FACTORY] Creating SOCKS5 client for proxy {_proxy.Host}:{_proxy.Port}");
                        var handler = SRT2Speech.ProxyService.HttpHandlers.Socks5HttpHandlerFactory.CreateSocks5Handler(_proxy);
                        client = new HttpClient(handler)
                        {
                            Timeout = TimeSpan.FromSeconds(30)
                        };
                    }
                    else
                    {
                        // Sử dụng HTTP/HTTPS proxy handler
                        Console.WriteLine($"[HTTP_CLIENT_FACTORY] Creating HTTP/HTTPS client for proxy {_proxy.Host}:{_proxy.Port}");
                        var handler = new ProxyHttpClientHandler(_proxy);
                        client = new HttpClient(handler)
                        {
                            Timeout = TimeSpan.FromSeconds(30)
                        };
                    }
                }
                else
                {
                    // Tạo client không proxy
                    Console.WriteLine($"[HTTP_CLIENT_FACTORY] Creating direct connection client (no proxy)");
                    client = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(30)
                    };
                }

                // Setup headers cho ElevenLabs API
                client.DefaultRequestHeaders.Add("xi-api-key", _apiKey);
                client.DefaultRequestHeaders.Add("Accept", "audio/mpeg");

                return client;
            }

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;

                lock (_poolLock)
                {
                    // Dispose tất cả clients
                    foreach (var client in _availableClients)
                    {
                        try
                        {
                            client.Dispose();
                        }
                        catch { /* ignore */ }
                    }
                    _availableClients.Clear();

                    foreach (var client in _activeClients)
                    {
                        try
                        {
                            client.Dispose();
                        }
                        catch { /* ignore */ }
                    }
                    _activeClients.Clear();
                }
            }
        }
    }
}