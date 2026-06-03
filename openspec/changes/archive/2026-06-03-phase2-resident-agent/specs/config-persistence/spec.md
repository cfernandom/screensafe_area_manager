# Delta for Config Persistence

## MODIFIED Requirements

### Requirement: Read Configuration on Startup

The system MUST read `appsettings.json` from the executable directory on startup. If the file does not exist, the system MUST return default settings (`reservedBottomPixels: 80`, `strategy: auto`, `eventDebounceMs: 400`, `logPath: %LOCALAPPDATA%\ScreenSafe\Logs\`).
(Previously: only `reservedBottomPixels: 80` and `strategy: auto` defaults)

#### Scenario: File exists with valid JSON (updated)

- GIVEN `appsettings.json` exists with `{"reservedBottomPixels": 120, "strategy": "spisetworkarea", "eventDebounceMs": 600}`
- WHEN `Load()` is called
- THEN it returns a `Settings` object with `ReservedBottomPixels = 120`, `Strategy = "spisetworkarea"`, and `EventDebounceMs = 600`

#### Scenario: File exists without new fields (backward compat)

- GIVEN `appsettings.json` has no `eventDebounceMs` or `logPath` fields
- WHEN `Load()` is called
- THEN `EventDebounceMs` defaults to 400 and `LogPath` defaults to `%LOCALAPPDATA%\ScreenSafe\Logs\`

#### Scenario: File does not exist

- GIVEN no `appsettings.json` exists in the executable directory
- WHEN `Load()` is called
- THEN it returns default settings including `eventDebounceMs: 400`
