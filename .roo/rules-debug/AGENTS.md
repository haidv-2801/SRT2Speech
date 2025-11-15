# AGENTS.md - Debug Mode

This file provides non-obvious debugging guidance for AI assistants working in this repository.

## Hidden Debug Information

- **Console logging is primary**: Most services use `Console.WriteLine()` for debugging, not configurable logging frameworks
- **Persistence logging format**: Look for `[PERSISTENCE]`, `[PERSISTENCE_ERROR]`, `[BINDING_INVALID]` prefixes in console output
- **Retry mechanism logging**: Retry attempts logged with `[RETRY]` and `[RETRY_ERROR]`/`[RETRY_INVALID]` prefixes
- **Proxy selection logging**: Proxy selection logged with strategy name and proxy details

## Critical Debug Points

- **ApiKeyManager state issues**: Check `Configs/DeadKeys.txt` for exhausted keys with timestamps and reasons
- **Proxy binding validation**: Invalid proxy endpoints are silently rejected - check for `[BINDING_INVALID]` console messages
- **YAML deserialization failures**: `YamlUtility.DeserializeAuto<T>()` tries 5 conventions - check console for which succeeded
- **Rate limit parsing**: X-RateLimit-Reset header parsing adjusts cooldowns - verify in ApiKeyManager logs

## Configuration Debug Paths

- **Dual proxy config paths**: ProxyManager checks BOTH workspace (`SRT2Speech.AppWindow/Configs/proxies.yaml`) AND runtime paths
- **State file locations**: Check `Configs/proxy-state.yaml` and `Configs/ElevenlabKeyState.yaml` for persistence issues
- **WebAPI config minimal**: `SRT2Speech.WebAPI/appsettings.json` only contains logging - proxy config comes from external sources

## Common Silent Failures

- **Proxy state persistence disabled**: Lines 331 and 335 in ProxyManager have LoadState() and timer commented out
- **Invalid proxy endpoints**: BoundProxyParser silently rejects invalid endpoints without throwing exceptions
- **Task cancellation handling**: Retry mechanism excludes user-requested cancellations - check cancellation token usage
- **AES encryption failures**: Hardcoded keys in AESEncryption class - failures indicate code issues, not config
