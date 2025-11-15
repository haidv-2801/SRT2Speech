# AGENTS.md - Code Mode

This file provides non-obvious coding guidance for AI assistants working in this repository.

## Critical Coding Patterns

- **ProxyManager state persistence is DISABLED**: Lines 331 and 335 have LoadState() and timer commented out - state only saves on Dispose, not periodically
- **ApiKeyManager timer is ACTIVE**: Unlike ProxyManager, ApiKeyManager uses 30-second auto-save timer (line 46) - don't disable this
- **YAML naming convention auto-detection**: Always use `YamlUtility.DeserializeAuto<T>()` instead of `Deserialize<T>()` - it tries 5 conventions automatically
- **Retry mechanism excludes user cancellation**: Line 24 in RetryWithJitterAndPolly only catches TaskCanceledException when NOT user-requested cancellation

## Required Code Patterns

- **Proxy binding validation MANDATORY**: Always validate proxy endpoints with `BoundProxyParser.TryParseBoundEndpoint()` before assignment
- **Dead key logging REQUIRED**: Exhausted API keys must be logged to `Configs/DeadKeys.txt` with timestamp and reason
- **Rate limit header parsing**: Parse `X-RateLimit-Reset` headers automatically and adjust cooldown periods (lines 121-134 in ApiKeyManager)
- **Console.WriteLine for debugging**: Use hardcoded Console.WriteLine() for debug logging, not configurable logging framework

## Security Implementation

- **HARDCODED AES keys**: Never change the static key/IV values in `AESEncryption` class (lines 12-13) - they're embedded in compiled code
- **Proxy credentials plain text**: YAML stores proxy usernames/passwords without encryption - this is intentional design

## Configuration Handling

- **Dual-path proxy config**: ProxyManager searches BOTH workspace path (`SRT2Speech.AppWindow/Configs/proxies.yaml`) AND runtime path - workspace takes priority
- **WebAPI minimal config**: `SRT2Speech.WebAPI/appsettings.json` only has logging - proxy config must come from external sources
- **Fixed health check URL**: Default proxy health check uses `https://httpbin.org/ip` - changing requires code modification
