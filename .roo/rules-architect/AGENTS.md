# AGENTS.md - Architect Mode

This file provides non-obvious architectural guidance for AI assistants working in this repository.

## Hidden Architectural Decisions

- **ProxyManager state persistence is DISABLED**: Despite EnableStatePersistence setting, the timer for periodic saves is commented out (line 335) - state only saves on Dispose
- **ApiKeyManager vs ProxyManager persistence difference**: ApiKeyManager uses 30-second auto-save timer while ProxyManager disables it - this inconsistency is intentional
- **Dual-path configuration loading**: ProxyManager searches BOTH workspace path (`SRT2Speech.AppWindow/Configs/proxies.yaml`) AND runtime path - workspace takes priority for development
- **ServiceLocator static pattern**: Uses static ServiceProvider property instead of dependency injection throughout the WPF app

## Critical Architectural Constraints

- **HARDCODED AES encryption keys**: Static key/IV values in AESEncryption class (lines 12-13) are embedded in compiled code - cannot be changed without recompilation
- **Proxy credentials in plain text**: YAML configuration intentionally stores usernames/passwords without encryption for performance
- **Fixed health check URL**: Default proxy health check uses `https://httpbin.org/ip` - changing requires code modification, not configuration
- **Console.WriteLine debugging**: Hardcoded console logging used throughout instead of configurable logging framework

## Configuration Architecture Gotchas

- **WebAPI minimal configuration**: `SRT2Speech.WebAPI/appsettings.json` only contains logging settings - proxy configuration must come from external YAML files
- **Relative state file paths**: All state files (`proxy-state.yaml`, `ElevenlabKeyState.yaml`) use relative paths that resolve differently in development vs production
- **YAML naming convention auto-detection**: `YamlUtility.DeserializeAuto<T>()` tries 5 different naming conventions in sequence - this can mask configuration errors

## Service Architecture Patterns

- **Retry mechanism design**: `RetryWithJitterAndPolly` excludes user-requested cancellations - only catches TaskCanceledException when NOT user-initiated (line 24)
- **Proxy binding validation requirement**: All proxy endpoints MUST be validated with `BoundProxyParser.TryParseBoundEndpoint()` before assignment - invalid endpoints are silently rejected
- **Dead key logging architecture**: Exhausted API keys are automatically logged to `Configs/DeadKeys.txt` with timestamp and reason - this is mandatory, not optional
- **Rate limit header parsing**: ApiKeyManager automatically parses `X-RateLimit-Reset` headers and adjusts cooldown periods - this happens transparently

## Build Architecture Requirements

- **Single-file deployment flags**: All projects require `/p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true` for proper single-file builds
- **Enforced output structure**: `build-all.bat` creates specific directory structure under `../Production/` - deviating from this structure breaks deployment
- **No test framework integration**: Project has empty `Tests` folders but no configured test framework - all testing is manual through WPF interface
