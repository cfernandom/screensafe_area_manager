# CLI Interface Specification

## Purpose

Console entry point with `apply`/`restore`/`status`/`install`/`uninstall`/`health` commands, DI composition root, and platform guard.

## Requirements

### Requirement: Command Dispatch

The system MUST parse the first CLI argument and dispatch to `apply`, `restore`, `status`, `install`, `uninstall`, or `health` handlers. If `--daemon` flag is present, the system MUST switch to resident mode instead. Unknown commands MUST print usage text and return exit code 1.

#### Scenario: Apply command

- GIVEN the application starts with args `["apply"]`
- WHEN `Main` dispatches the command
- THEN it invokes `ApplyUseCase` with settings from `JsonSettingsRepository`

#### Scenario: Restore command

- GIVEN the application starts with args `["restore"]`
- WHEN `Main` dispatches the command
- THEN it invokes `RestoreUseCase` which reads stored original RECT and calls `SPI_SETWORKAREA`

#### Scenario: Status command

- GIVEN the application starts with args `["status"]`
- WHEN `Main` dispatches the command
- THEN it prints current work area bounds and reserved pixels count

#### Scenario: Install command

- GIVEN the application starts with args `["install"]`
- WHEN `Main` dispatches the command
- THEN it invokes `WindowsStartupManager.Install()`

#### Scenario: Uninstall command

- GIVEN the application starts with args `["uninstall"]`
- WHEN `Main` dispatches the command
- THEN it invokes `WindowsStartupManager.Uninstall()`

#### Scenario: Health command

- GIVEN the application starts with args `["health"]`
- WHEN `Main` dispatches the command
- THEN it invokes `HealthUseCase` and prints diagnostic output

#### Scenario: Daemon flag

- GIVEN the application starts with args `["--daemon"]`
- WHEN `Main` dispatches the command
- THEN it enters resident mode with message pump and Win32 monitoring

#### Scenario: Unknown command

- GIVEN the application starts with args `["reboot"]`
- WHEN `Main` dispatches the command
- THEN it prints usage instructions to stderr
- AND returns exit code 1

### Requirement: Health Output Format

The `health` command MUST print structured diagnostic output to stdout with the exact format:

```
ScreenSafe Health

Current Resolution: {width}x{height}
Desired WorkArea:   {left},{top},{right},{bottom}
Current WorkArea:   {left},{top},{right},{bottom}
Strategy:           {strategy name}
Daemon:             {Running|Stopped}
AutoStart:          {Enabled|Disabled}
Last Reapply:       {ISO timestamp or N/A}
Status:             {OK|Mismatch Detected}
```

#### Scenario: Health output all fields present

- GIVEN the daemon is running and auto-start is enabled
- WHEN `health` is executed
- THEN all 8 fields are printed with correct values

#### Scenario: Health when daemon not running

- GIVEN the daemon is not running
- WHEN `health` is executed
- THEN Daemon field shows "Stopped" and Last Reapply shows "N/A"

### Requirement: Platform Guard on Non-Windows

On non-Windows OS, all commands MUST print an error message and exit immediately without invoking any Win32 calls.

#### Scenario: Linux shows platform error

- GIVEN the process runs on Linux
- WHEN any command is dispatched
- THEN it prints "ScreenSafe requires Windows" to stderr
- AND returns exit code 2

### Requirement: DI Composition Root

The Console project MUST be the composition root. All dependencies (IWorkAreaManager, ISettingsRepository, IPlatformInfoProvider, IScreenInfoProvider) MUST be registered in `Microsoft.Extensions.DependencyInjection` and resolved before command dispatch.

#### Scenario: All services resolve

- GIVEN the DI container is configured with all registrations
- WHEN `ServiceProvider` is built
- THEN `IWorkAreaManager`, `ISettingsRepository`, `IPlatformInfoProvider`, and `IScreenInfoProvider` resolve without throwing
