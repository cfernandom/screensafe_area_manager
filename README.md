# ScreenSafe Area Manager

A CLI utility for Windows that reserves a configurable strip at the bottom of the primary display — useful for compensating defective LCD areas or reserving screen space on kiosk/AIO systems.

Targets **Windows 8.1** (primary) and **Windows 10+** (compatible via dual-strategy fallback). Built with .NET Framework 4.8.

## Demo

![ScreenSafe demo v0.2.0](assets/demo-v0.2.0.gif)

## Features

- **Reserve** — trim the desktop work area by N pixels from the bottom
- **Restore** — return the work area to its original full-screen bounds
- **Status** — display current work area, screen resolution, and reservation state
- **Daemon** — background resident agent that monitors work area changes and reapplies automatically
- **Auto-start** — register/unregister via `install` / `uninstall` commands (HKCU Run key)
- **Health** — full diagnostic output (resolution, work area, daemon status, auto-start, last reapply)
- **Logging** — rotating file logs at `%LOCALAPPDATA%\ScreenSafe\Logs\`
- **Circuit breaker** — max 10 reapplies in 60s window, 5 min suspension if exceeded
- **Persistent** — settings survive across CLI invocations via `appsettings.json`
- **Safe defaults** — 80px reserved, 400ms debounce, `auto` strategy

## Requirements

- **OS**: Windows 8.1 or later
- **Runtime**: .NET Framework 4.8 ([download](https://dotnet.microsoft.com/download/dotnet-framework/net48))
  - **Windows 10 v1903+**: comes pre-installed
  - **Windows 8 / 8.1**: requires manual install — [offline installer](https://support.microsoft.com/es-es/topic/instalador-sin-conexi%C3%B3n-de-microsoft-net-framework-4-8-para-windows-9d23f658-3b97-68ab-d013-aa3c3e7495e0)
- **Build** (optional): .NET Framework 4.8 Developer Pack, .NET SDK 8.0+ for multi-target builds

## Download

Grab the latest release from GitHub — no login required.

1. Go to the [releases page](https://github.com/cfernandom/screensafe_area_manager/releases)
2. Download the `ScreenSafe-v*.*.*-win81.zip` asset from the latest release
3. Extract the ZIP anywhere on the target machine
4. Open a terminal in the extracted folder and run:

```console
ScreenSafe.Console.exe status
```

See [Usage](#usage) below for all available commands.

> **Note**: .NET Framework 4.8 comes pre-installed on Windows 10 v1903+. On Windows 8/8.1, install it first ([download page](https://dotnet.microsoft.com/download/dotnet-framework/net48) or [offline installer](https://support.microsoft.com/es-es/topic/instalador-sin-conexi%C3%B3n-de-microsoft-net-framework-4-8-para-windows-9d23f658-3b97-68ab-d013-aa3c3e7495e0)).

### SmartScreen Notice

When you run the downloaded `ScreenSafe.Console.exe`, Windows SmartScreen may show
**"Windows protected your PC"** and block execution. This happens because the
executable is not code-signed with an Authenticode certificate
(certificates cost $300–500/year).

**How to unblock:**

1. Right-click `ScreenSafe.Console.exe` → **Properties** → check **Unblock** →
   **Apply** → **OK**. Then run the command again.

2. Or run PowerShell in the extracted folder:

   ```powershell
   Unblock-File -Path .\ScreenSafe.Console.exe
   ```

3. For a permanent fix: extract the ZIP to `C:\Program Files\ScreenSafe\` instead
   of Downloads or Desktop. SmartScreen only flags files downloaded to user-profile
   directories — `Program Files` is treated as a trusted location.

> **Why no code signing?** ScreenSafe is a small open-source utility. An Extended
> Validation (EV) code signing certificate from a Certificate Authority is the
> industry standard for removing SmartScreen warnings, but it is cost-prohibitive
> for a project at this stage. If the project grows, a certificate can be added
> and the EXE signed with `signtool.exe`.


## Quick Start

> Build from source. If you just want to run the app, see [Download](#download) above.

```console
# Clone and build
git clone <repo>
dotnet build -f net48 src/ScreenSafe.slnx

# Show current status
src\ScreenSafe.Console\bin\Debug\net48\ScreenSafe.Console.exe status

# Reserve 80 pixels at the bottom (default)
src\ScreenSafe.Console\bin\Debug\net48\ScreenSafe.Console.exe apply

# Restore the original full-screen area
src\ScreenSafe.Console\bin\Debug\net48\ScreenSafe.Console.exe restore
```

## Usage

```
ScreenSafe.Console.exe <command>

Commands:
  apply     Reserve the configured bottom pixels. Saves the original
            work area for later restoration.
  restore   Restore the original full-screen work area. Clears the
            stored original area.
  status    Display current work area, screen resolution, config,
            and whether a reservation is active.
  install   Register ScreenSafe to start automatically with Windows
            (HKCU\...\Run).
  uninstall Remove the auto-start registration.
  health    Display full diagnostic: resolution, work area,
            strategy, daemon status, auto-start, last reapply time.
  --daemon  Start in background monitor mode. Detects work area
            changes and reapplies automatically. Used by auto-start.

Exit codes:
  0   Success
  1   Error (apply failed, daemon already running, error reading state, etc.)
  2   Health check error (failed to read diagnostic data)
```

## Configuration

`appsettings.json` sits next to the executable.

```json
{
  "Enabled": true,
  "ReservedBottomPixels": 80,
  "Strategy": "auto",
  "OriginalWorkArea": null,
  "EventDebounceMs": 400,
  "LogPath": ""
}
```

| Field | Default | Description |
|---|---|---|
| `Enabled` | `true` | Master switch. When `false`, `apply` exits with code 1. |
| `ReservedBottomPixels` | `80` | Number of pixels to trim from the bottom of the work area. |
| `Strategy` | `"auto"` | `"SpSetWorkArea"` (SystemParametersInfo), `"ShAppBarMessage"` (Shell32 fallback), or `"auto"` (pick by config). |
| `OriginalWorkArea` | `null` | Stored automatically after `apply`. Used by `restore`. |
| `EventDebounceMs` | `400` | Debounce interval (ms) for work area change events in daemon mode. |
| `LogPath` | `""` | Log directory path. Empty = `%LOCALAPPDATA%\ScreenSafe\Logs\`. |

## Architecture

Clean Architecture with 5 projects:

```
ScreenSafe.Domain       → Models (ScreenRect, AppSettings) and interfaces
ScreenSafe.Application  → Use cases (Apply, Restore, Status) + AutoApplyService,
                            HealthUseCase
ScreenSafe.Infrastructure → P/Invoke, strategies, persistence, platform detection,
                            WorkAreaWatcher, EventDebouncer, WindowsStartupManager,
                            LogRotator, DaemonStatusProvider
ScreenSafe.Console      → CLI entry point, DI composition root, daemon mode (--daemon)
ScreenSafe.Tests        → xUnit + Moq tests
```

### Strategy Pattern

`IWorkAreaManager` has two implementations:

| Strategy | API | Target |
|---|---|---|
| `SpSetWorkAreaStrategy` | `SystemParametersInfoW(SPI_SETWORKAREA)` | Windows 8.1 (primary) |
| `ShAppBarMessageStrategy` | `SHAppBarMessage(ABM_SETPOS)` | Windows 10+ (fallback — survives Explorer override) |

Strategy selection is driven by the `Strategy` config field. `"auto"` defaults to `SpSetWorkAreaStrategy` for broad compatibility, and the user can switch to `"ShAppBarMessage"` when the primary path is overridden by Explorer.

### Data Flow

**CLI mode** (commands: apply, restore, status, health, install, uninstall):

```
CLI args → CliDispatcher → UseCase → IWorkAreaManager → P/Invoke → Win32 API
                              ↘ ISettingsRepository ↗
```

**Daemon mode** (`--daemon`):

```
Named mutex (singleton guard)
      ↓
WorkAreaWatcher (hidden Win32 window, message pump)
      ↓
EventDebouncer (400ms coalesce)
      ↓
AutoApplyService → SPI_GETWORKAREA → compare → reapply if different
      ↓
Circuit breaker (10/60s → 5 min suspend if exceeded)
```

## Development

### Build

```console
dotnet build -f net48 src/ScreenSafe.slnx
```

Multi-target libraries also compile for `net8.0`:

```console
dotnet build -f net8.0 src/ScreenSafe.slnx
```

### Test

```console
dotnet test -f net48 src/ScreenSafe.Tests/ScreenSafe.Tests.csproj
```

xUnit + Moq. Strict TDD — tests are written before production code.

### Linux

The project compiles on Linux (`dotnet build -f net48`), but running any command throws `PlatformNotSupportedException` — the P/Invoke calls require Windows.

### Project Structure

```
src/
├── ScreenSafe.Domain/           # Models + interfaces (zero dependencies)
│   ├── ScreenRect.cs
│   ├── AppSettings.cs
│   ├── HealthReport.cs
│   ├── IWorkAreaManager.cs, IPlatformInfoProvider.cs,
│   │   IScreenInfoProvider.cs, ISettingsRepository.cs
│   ├── IWorkAreaWatcher.cs, IEventDebouncer.cs, ILogger.cs
│   ├── IWindowsStartupManager.cs
│   └── IDaemonStatusProvider.cs
├── ScreenSafe.Application/      # Use cases
│   ├── ApplyUseCase.cs
│   ├── RestoreUseCase.cs
│   ├── StatusUseCase.cs
│   ├── AutoApplyService.cs      # Resident agent orchestrator
│   └── HealthUseCase.cs         # Diagnostic aggregation
├── ScreenSafe.Infrastructure/   # P/Invoke + strategies + persistence
│   ├── NativeMethods/
│   │   ├── User32.cs
│   │   └── Shell32.cs
│   ├── SpSetWorkAreaStrategy.cs
│   ├── ShAppBarMessageStrategy.cs
│   ├── PlatformInfoProvider.cs
│   ├── ScreenInfoProvider.cs
│   ├── JsonSettingsRepository.cs
│   ├── PlatformGuard.cs
│   ├── WorkAreaWatcher.cs       # Hidden Win32 window + message pump
│   ├── EventDebouncer.cs        # Timer-based coalescing
│   ├── LogRotator.cs            # Size-based rotation (1 MB × 3)
│   ├── Logging/
│   │   ├── ConsoleLogger.cs
│   │   └── FileLogger.cs
│   ├── WindowsStartupManager.cs # Registry Run key CRUD
│   └── DaemonStatusProvider.cs  # Named mutex daemon detection
├── ScreenSafe.Console/          # CLI entry point
│   ├── Program.cs               # Dual mode: --daemon or CLI
│   ├── CliDispatcher.cs         # +install, +uninstall, +health
│   ├── app.manifest
│   └── appsettings.json
└── ScreenSafe.Tests/            # xUnit + Moq
    ├── Domain/
    ├── Application/
    │   ├── AutoApplyServiceTests.cs
    │   └── HealthUseCaseTests.cs
    ├── Console/
    │   ├── CliDispatcherTests.cs
    │   └── ProgramTests.cs
    └── Infrastructure/
```

### Technical Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Runtime | .NET Framework 4.8 | Required for Windows 8.1 support |
| P/Invoke | `[DllImport]` | `LibraryImport` is .NET 5+ |
| JSON | System.Text.Json | Modern API, no 3rd-party dependency |
| DI | `ServiceCollection` | `Host.CreateDefaultBuilder` doesn't exist on .NET Framework |
| Screen resolution | `GetSystemMetrics` P/Invoke | Avoids WinForms assembly dependency |
| OS detection | `Environment.OSVersion` + app.manifest | No extra P/Invoke needed with proper manifest |
| Daemon detection | Named mutex `Global\ScreenSafeDaemon` | Cross-process, atomic create, no extra deps |
| Debounce | Timer-based, single-fire, 400ms default | Avoids redundant reapplies during rapid events |
| Log rotation | 1 MB per file, 3 file retention | Prevents unbounded disk usage |
| Message pump | `CreateWindowExW` + manual `WndProc` | No WinForms/WPF dependency, lightweight |
| Circuit breaker | 10 reapplies / 60s sliding → 5 min suspend | Mitigates Win10+ Explorer override loops |
| OriginalWorkArea | Captured once, never rewritten by daemon | Prevents daemon from corrupting the reference |
| Window creation | Class atom via `MAKEINTATOM` + `WS_OVERLAPPED` | Avoids `ERROR_CANNOT_FIND_WND_CLASS` (1407) on Win8.1+. Overlapped windows receive system broadcasts on all Windows versions |
| Restore state | Stateless — `Restore(ScreenRect)` | Caller provides pre-reservation area from persistent JSON. No in-memory state → no bugs across restarts |
| Event convergence | Comparison-based (no suppression flag) | Comparing right+bottom edges converges naturally — a matching comparison produces no reapply. Self-suppression was blocking legitimate reapplies after external taskbar changes |
| Watcher diagnostics | `ILogger` via DI factory | Diagnostic logs for window creation, message pump lifecycle, and event reception — Win32 integration failures become debuggable |

