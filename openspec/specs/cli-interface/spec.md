# CLI Interface Specification

## Purpose

Console entry point with `apply`/`restore`/`status` commands, DI composition root, and platform guard.

## Requirements

### Requirement: Command Dispatch

The system MUST parse the first CLI argument and dispatch to `apply`, `restore`, or `status` handlers. Unknown commands MUST print usage text and return exit code 1.

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

#### Scenario: Unknown command

- GIVEN the application starts with args `["reboot"]`
- WHEN `Main` dispatches the command
- THEN it prints usage instructions to stderr
- AND returns exit code 1

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
