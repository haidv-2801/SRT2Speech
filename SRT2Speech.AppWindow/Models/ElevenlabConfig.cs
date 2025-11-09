using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SRT2Speech.AppWindow.Services;

namespace SRT2Speech.AppWindow.Models
{
    public class ElevenlabConfig
    {
        public string Url { get; set; } = "https://api.elevenlabs.io/v1/text-to-speech"; // URL for the API
        public string VoiceId { get; set; } = "pNInz6obpgDQGcFmaJgB"; // Voice ID
        public string OptimizeStreamingLatency { get; set; } = "0"; // Latency optimization
        public string OutputFormat { get; set; } = "mp3_22050_32"; // Output format
        public string ModelId { get; set; } = "eleven_turbo_v2"; // Model ID
        public KeySelectionAlgorithm KeySelectionAlgorithm { get; set; } = KeySelectionAlgorithm.RoundRobin;
        public int QuotaResetTimeMinutes { get; set; } = 60; // Default cooldown period in minutes
        public int MaxThreads { get; set; }
        public int SleepTime { get; set; }
        public string LanguageCode { get; set; } = default!;
        public VoiceSettings VoiceSettings { get; set; } = new VoiceSettings(); // Voice settings
        
        // Proxy configuration
        public bool EnableProxyRotation { get; set; } = false;
        public string ProxyRotationStrategy { get; set; } = "RoundRobin";
        public int ProxyMaxRetries { get; set; } = 3;
        public int ProxyRetryDelayMilliseconds { get; set; } = 1000;
        public bool EnableProxyHealthCheck { get; set; } = true;
        public int ProxyHealthCheckIntervalSeconds { get; set; } = 30;
        public string ProxyHealthCheckUrl { get; set; } = "https://httpbin.org/ip";
        public int ProxyHealthCheckTimeoutSeconds { get; set; } = 10;
    }

    public class VoiceSettings
    {
        public double Stability { get; set; } = 0; // Stability of the voice
        public double SimilarityBoost { get; set; } = 1; // Similarity boost
        public double Style { get; set; } = 0; // Style parameter
        public bool UseSpeakerBoost { get; set; } = true; // Use speaker boost
    }
}
