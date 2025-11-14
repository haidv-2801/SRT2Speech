using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Timers;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.Core.Utilitys;
using YamlDotNet.Serialization.NamingConventions;

namespace SRT2Speech.AppWindow.Services
{
    public enum KeySelectionAlgorithm
    {
        RoundRobin,
        Random,
        LeastUsed,
        WeightedRandom,
        PriorityBased,
        LoadBalanced
    }

    public class ApiKeyManager
    {
        private readonly List<ApiKeyInfo> _apiKeys;
        private readonly KeySelectionAlgorithm _algorithm;
        private readonly TimeSpan _defaultCooldownPeriod;
        private readonly string _keyStateFilePath;
        private int _roundRobinIndex = 0;
        private readonly object _lock = new object();
        private System.Timers.Timer _saveTimer;
        private bool _hasUnsavedChanges = false;

        public ApiKeyManager(List<ApiKeyInfo> apiKeys, KeySelectionAlgorithm algorithm, TimeSpan defaultCooldownPeriod, string keyStateFilePath = null)
        {
            _apiKeys = apiKeys ?? throw new ArgumentNullException(nameof(apiKeys));
            _algorithm = algorithm;
            _defaultCooldownPeriod = defaultCooldownPeriod;
            _keyStateFilePath = keyStateFilePath ?? Path.Combine("Configs", "ElevenlabKeyState.yaml");

            // Load existing key state if file exists
            LoadKeyState();

            // Setup periodic save timer (30 seconds)
            _saveTimer = new System.Timers.Timer(30000); // 30 seconds
            _saveTimer.Elapsed += OnSaveTimerElapsed;
            _saveTimer.AutoReset = true;
            _saveTimer.Start();
        }

        public ApiKeyInfo? GetAvailableKey()
        {
            lock (_lock)
            {
                // Reset any expired cooldowns
                ResetExpiredCooldowns();

                var availableKeys = _apiKeys.Where(k => k.IsAvailable()).ToList();
                if (!availableKeys.Any())
                {
                    return null;
                }

                ApiKeyInfo selectedKey;
                switch (_algorithm)
                {
                    case KeySelectionAlgorithm.RoundRobin:
                        selectedKey = availableKeys[_roundRobinIndex % availableKeys.Count];
                        _roundRobinIndex = (_roundRobinIndex + 1) % availableKeys.Count;
                        break;
                    case KeySelectionAlgorithm.Random:
                        var random = new Random();
                        selectedKey = availableKeys[random.Next(availableKeys.Count)];
                        break;
                    case KeySelectionAlgorithm.LeastUsed:
                        selectedKey = availableKeys.OrderBy(k => k.UsedCount).First();
                        break;
                    case KeySelectionAlgorithm.WeightedRandom:
                        selectedKey = SelectWeightedRandom(availableKeys);
                        break;
                    case KeySelectionAlgorithm.PriorityBased:
                        selectedKey = availableKeys.OrderByDescending(k => k.Priority).First();
                        break;
                    case KeySelectionAlgorithm.LoadBalanced:
                        // For LoadBalanced, use LeastUsed as base implementation
                        // Can be extended with more sophisticated load metrics
                        selectedKey = availableKeys.OrderBy(k => k.UsedCount).First();
                        break;
                    default:
                        throw new NotSupportedException($"Algorithm {_algorithm} is not supported.");
                }

                selectedKey.IncrementUsage();
                _hasUnsavedChanges = true;
                return selectedKey;
            }
        }

        public void MarkKeyExhausted(string key, TimeSpan? cooldownPeriod = null)
        {
            lock (_lock)
            {
                var apiKey = _apiKeys.FirstOrDefault(k => k.Key == key);
                if (apiKey != null)
                {
                    apiKey.MarkExhausted(cooldownPeriod ?? _defaultCooldownPeriod);
                }
            }
        }

        public void MarkKeyExhausted(string key, HttpResponseMessage response)
        {
            TimeSpan cooldownPeriod = _defaultCooldownPeriod;

            // Try to parse X-RateLimit-Reset header
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
            {
                var resetHeader = resetValues.FirstOrDefault();
                if (long.TryParse(resetHeader, out var resetTimestamp))
                {
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetTimestamp).UtcDateTime;
                    var timeUntilReset = resetTime - DateTime.UtcNow;
                    if (timeUntilReset > TimeSpan.Zero)
                    {
                        cooldownPeriod = timeUntilReset;
                    }
                }
            }

            MarkKeyExhausted(key, cooldownPeriod);
        }

        public List<ApiKeyInfo> GetAllKeys()
        {
            lock (_lock)
            {
                return _apiKeys.ToList();
            }
        }

        public string GetKeyStatusSummary()
        {
            lock (_lock)
            {
                var totalKeys = _apiKeys.Count;
                var availableKeys = _apiKeys.Count(k => k.IsAvailable());
                var exhaustedKeys = totalKeys - availableKeys;

                return $"Tổng key: {totalKeys}, Có sẵn: {availableKeys}, Key chết: {exhaustedKeys}";
            }
        }

        private void ResetExpiredCooldowns()
        {
            foreach (var key in _apiKeys)
            {
                if (!key.Available && key.CooldownUntil.HasValue && DateTime.UtcNow >= key.CooldownUntil.Value)
                {
                    key.ResetCooldown();
                }
            }
        }

        private ApiKeyInfo SelectWeightedRandom(List<ApiKeyInfo> availableKeys)
        {
            // Calculate weights based on inverse of usage count (less used = higher weight)
            var weights = availableKeys.Select(k => 1.0 / (k.UsedCount + 1)).ToList();
            var totalWeight = weights.Sum();

            var random = new Random();
            var randomValue = random.NextDouble() * totalWeight;

            double cumulativeWeight = 0;
            for (int i = 0; i < availableKeys.Count; i++)
            {
                cumulativeWeight += weights[i];
                if (randomValue <= cumulativeWeight)
                {
                    return availableKeys[i];
                }
            }

            // Fallback to first key (should not happen)
            return availableKeys.First();
        }

        private void LoadKeyState()
        {
            try
            {
                if (File.Exists(_keyStateFilePath))
                {
                    var keyState = YamlUtility.DeserializeAuto<ElevenlabKeyState>(File.ReadAllText(_keyStateFilePath));
                    if (keyState?.ApiKeys != null && keyState.ApiKeys.Any())
                    {
                        // Update existing keys with saved state
                        foreach (var savedKey in keyState.ApiKeys)
                        {
                            var existingKey = _apiKeys.FirstOrDefault(k => k.Key == savedKey.Key);
                            if (existingKey != null)
                            {
                                existingKey.UsedCount = savedKey.UsedCount;
                                existingKey.Priority = savedKey.Priority;
                                existingKey.Available = savedKey.Available;
                                existingKey.CooldownUntil = savedKey.CooldownUntil;
                                
                                // Validate BoundProxyEndpoint before assigning
                                if (!string.IsNullOrWhiteSpace(savedKey.BoundProxyEndpoint))
                                {
                                    if (BoundProxyParser.TryParseBoundEndpoint(
                                        savedKey.BoundProxyEndpoint, out _, out var error))
                                    {
                                        existingKey.BoundProxyEndpoint = savedKey.BoundProxyEndpoint;
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[BINDING_INVALID] Key {savedKey.Key} has invalid BoundProxyEndpoint '{savedKey.BoundProxyEndpoint}': {error}");
                                        // Don't assign invalid endpoint
                                    }
                                }
                                else
                                {
                                    existingKey.BoundProxyEndpoint = savedKey.BoundProxyEndpoint;
                                }
                            }
                        }

                        Console.WriteLine($"[PERSISTENCE] Key state loaded successfully - {keyState.ApiKeys.Count} keys, Last saved: {keyState.LastSaved?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}");
                    }
                }
                else
                {
                    Console.WriteLine($"[PERSISTENCE] Key state file not found, using default state: {_keyStateFilePath}");
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash - use default state
                Console.WriteLine($"[PERSISTENCE_ERROR] Error loading key state: {ex.Message}");
            }
        }

        private void SaveKeyState()
        {
            if (!_hasUnsavedChanges) return;

            try
            {
                var keyState = new ElevenlabKeyState
                {
                    ApiKeys = _apiKeys.ToList(),
                    LastSaved = DateTime.UtcNow,
                    KeySelectionAlgorithm = _algorithm
                };

                var yamlContent = YamlUtility.SerializeToHyphenated(keyState);
                File.WriteAllText(_keyStateFilePath, yamlContent);
                _hasUnsavedChanges = false;

                // Log successful save
                Console.WriteLine($"[PERSISTENCE] Key state saved successfully - {keyState.ApiKeys.Count} keys, Last saved: {keyState.LastSaved:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.WriteLine($"[PERSISTENCE_ERROR] Error saving key state: {ex.Message}");
            }
        }

        private void OnSaveTimerElapsed(object sender, ElapsedEventArgs e)
        {
            SaveKeyState();
            // Log to console for debugging (optional)
            Console.WriteLine($"[PERSISTENCE] Key state saved at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public void Dispose()
        {
            _saveTimer?.Stop();
            _saveTimer?.Dispose();
            SaveKeyState(); // Final save
        }
    }
}