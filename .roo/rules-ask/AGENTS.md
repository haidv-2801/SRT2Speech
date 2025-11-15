# AGENTS.md - Ask Mode

This file provides non-obvious documentation context for AI assistants working in this repository.

## Hidden Documentation Context

- **Console logging is primary debug source**: Most services use `Console.WriteLine()` for debugging, not configurable logging frameworks - check console output first
- **Persistence logging format**: Look for `[PERSISTENCE]`, `[PERSISTENCE_ERROR]`, `[BINDING_INVALID]` prefixes in console for state-related issues
- **Rate limit parsing happens automatically**: ApiKeyManager transparently parses `X-RateLimit-Reset` headers and adjusts cooldowns - no manual intervention needed
- **Proxy binding validation is silent**: Invalid proxy endpoints are rejected without exceptions - check console for `[BINDING_INVALID]` messages

## Counterintuitive Code Organization

- **ServiceLocator uses static pattern**: Unlike typical DI, ServiceLocator has static ServiceProvider property - services are accessed statically, not injected
- **ProxyManager vs ApiKeyManager persistence inconsistency**: ProxyManager disables auto-save while ApiKeyManager uses 30-second timer - this inconsistency is intentional
- **Dual configuration path priority**: ProxyManager checks workspace path first, then runtime path - workspace configs override runtime configs
- **WebAPI has minimal configuration**: `SRT2Speech.WebAPI/appsettings.json` only contains logging - all proxy config comes from external YAML files

## Configuration Discovery Patterns

- **State file locations**: Check `Configs/proxy-state.yaml` and `Configs/ElevenlabKeyState.yaml` for persistence issues
- **Dead key tracking**: Exhausted API keys automatically logged to `Configs/DeadKeys.txt` with timestamps and reasons
- **YAML naming convention detection**: `YamlUtility.DeserializeAuto<T>()` tries 5 conventions automatically - configuration errors may be masked
- **Fixed health check endpoint**: Default proxy health check uses `https://httpbin.org/ip` - this is hardcoded, not configurable

## Silent Failure Points

- **Task cancellation exclusion**: Retry mechanism excludes user-requested cancellations - check cancellation token usage in retry logic
- **AES encryption key immutability**: Static key/IV values in AESEncryption cannot be changed - encryption failures indicate code issues
- **Proxy state persistence disabled**: Despite EnableStatePersistence setting, ProxyManager timer is commented out - state only saves on Dispose
- **Invalid endpoint rejection**: BoundProxyParser silently rejects invalid proxy endpoints without throwing exceptions

## Build and Deployment Context

- **Single-file deployment requirements**: All projects need specific MSBuild flags for proper single-file executables
- **Enforced output directory structure**: `build-all.bat` creates `../Production/` structure - deviating breaks deployment
- **No test framework configuration**: Project has empty Tests folders but no xUnit/NUnit/MSTest setup - testing is manual through WPF interface
- **Relative path resolution differences**: State files use relative paths that resolve differently in development vs production environments
