# Resident Agent Specification

## Purpose

Daemon mode that monitors Win32 desktop events and automatically reapplies the desired work area when Windows or Explorer overrides it.

## Requirements

### Requirement: Hidden Window Boot

The daemon MUST start without a visible console window. It MUST load config from `appsettings.json` on startup and apply the desired WorkArea (no-op if already correct). The Win32 message pump MUST start AFTER the initial WorkArea application.

#### Scenario: Startup applies then monitors

- GIVEN the daemon starts with `--daemon`
- WHEN it initializes
- THEN it loads `appsettings.json`, applies desired WorkArea, and starts the message pump
- AND no visible window is shown

#### Scenario: No-op if area already correct

- GIVEN the current WorkArea already matches the desired config
- WHEN the daemon initializes
- THEN it skips apply and starts monitoring directly

### Requirement: Event Filtering and Debounce

The daemon MUST filter `WM_SETTINGCHANGE` (wParam == SPI_SETWORKAREA), `WM_DISPLAYCHANGE`, and `TaskbarCreated` messages. Events MUST be debounced using configurable `eventDebounceMs` (default 400ms).

#### Scenario: Debounce collapses rapid events

- GIVEN 5 WM_SETTINGCHANGE events arrive within 200ms
- WHEN the debounce timer fires after 400ms
- THEN only one evaluation cycle executes

#### Scenario: Irrelevant WM_SETTINGCHANGE ignored

- GIVEN a WM_SETTINGCHANGE event with wParam != SPI_SETWORKAREA
- WHEN the event is received
- THEN the debounce timer is NOT reset

### Requirement: Stateful Reapply

After debounce, the daemon MUST read `SPI_GETWORKAREA`, compare against the desired area, and reapply ONLY if they differ. It MUST NOT rewrite `OriginalWorkArea`.

#### Scenario: No reapply on matching areas

- GIVEN `SPI_GETWORKAREA` returns the desired RECT
- WHEN the debounce timer fires
- THEN no `SPI_SETWORKAREA` call is made

#### Scenario: Reapply on mismatch

- GIVEN `SPI_GETWORKAREA` returns a different RECT than desired
- WHEN the debounce timer fires
- THEN `SPI_SETWORKAREA` is called with the desired RECT
- AND the event is logged

### Requirement: Circuit Breaker

The daemon MUST track reapplies in a sliding 60s window. If the count exceeds 10, it MUST suspend automatic reapplication for 5 minutes and log a critical error. After suspension, it MUST resume normally.

#### Scenario: Breaker activates and recovers

- GIVEN 10 reapplies occurred within 60 seconds
- WHEN the 11th reapply is triggered
- THEN the daemon suspends monitoring for 5 minutes and logs a critical error
- AND resumes normal operation after the suspension expires

### Requirement: Clean Shutdown

The daemon MUST stop cleanly on Ctrl+C or WM_CLOSE, disposing all resources and stopping the message pump.

#### Scenario: Ctrl+C stops daemon

- GIVEN the daemon is running
- WHEN Ctrl+C is received
- THEN the message pump exits, resources are disposed, and the process terminates
