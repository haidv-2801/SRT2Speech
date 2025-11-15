# AGENTS.md

This file provides guidance to agents when working with code in this repository.

## Non-Obvious Build Commands

- **Single-file builds require special flags**: All projects use `/p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true` for single-file deployment
- **Proxy config dual-path loading**: ProxyManager searches BOTH workspace path (`SRT2Speech.AppWindow/Configs/proxies.yaml`) AND runtime path (`AppDomain.CurrentDomain.BaseDirectory/Configs/proxies.yaml`) - workspace takes priority
- **Build output structure is enforced**: `build-all.bat` creates specific directory structure under `../Production/` with separate folders for each component

## Hidden Code Patterns

- **ApiKeyManager auto-save is DISABLED**: The timer for periodic saves is commented out (lines 46, 335 in ProxyManager) - only saves on explicit calls or Dispose
- **Proxy state persistence is DISABLED**: State save timer is commented out (line 335 in ProxyManager) despite EnableStatePersistence setting
- **YAML utility has auto-detection**: `YamlUtility.DeserializeAuto<T>()` tries 5 different naming conventions in sequence before failing
- **Retry mechanism excludes user cancellation**: `TaskCanceledException` is only caught when `CancellationToken` is default or not user-requested (line 24 in RetryWithJitterAndPolly)

## Critical Project-Specific Conventions

- **Proxy binding validation is MANDATORY**: `BoundProxyParser.TryParseBoundEndpoint()` must validate proxy endpoints before assignment - invalid endpoints are silently rejected
- **Dead key logging is automatic**: Exhausted API keys are ALWAYS logged to `Configs/DeadKeys.txt` with timestamp and reason
- **Rate limit header parsing**: ApiKeyManager automatically parses `X-RateLimit-Reset` headers and adjusts cooldown periods accordingly
- **Console logging is hardcoded**: Many services use `Console.WriteLine()` for debugging (not configurable via appsettings)

## Configuration Gotchas

- **WebAPI has minimal config**: `SRT2Speech.WebAPI/appsettings.json` only contains logging settings - proxy configuration must come from external sources
- **Proxy health check URL is fixed**: Default health check uses `https://httpbin.org/ip` - changing this requires code modification
- **State file paths are relative**: All state files (`proxy-state.yaml`, `ElevenlabKeyState.yaml`) use relative paths that resolve differently in development vs production

## Testing Requirements

- **NO test framework detected**: Project has empty `Tests` folders but no xUnit, NUnit, or MSTest configuration
- **Manual testing only**: All testing appears to be manual through the WPF interface

## Security Patterns

- **AES encryption uses HARDCODED keys**: `AESEncryption` class has static key/IV values embedded in code (lines 12-13)
- **Proxy credentials in plain text**: YAML configuration stores proxy usernames/passwords without encryption
