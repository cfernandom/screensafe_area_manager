# ScreenSafe Area Manager

A CLI utility for Windows that reserves a configurable strip at the bottom of the primary display — useful for compensating defective LCD areas or reserving screen space on kiosk/AIO systems.

Targets **Windows 8.1** (primary) and **Windows 10+** (compatible via dual-strategy fallback). Built with .NET Framework 4.8.

## Features

- **Reserve** — trim the desktop work area by N pixels from the bottom
- **Restore** — return the work area to its original full-screen bounds
- **Status** — display current work area, screen resolution, and reservation state
- **Persistent** — settings survive across CLI invocations via `appsettings.json`
- **Safe defaults** — 80px reserved, `auto` strategy selection

## Requirements

- **OS**: Windows 8.1 or later
- **Runtime**: .NET Framework 4.8 ([included with Windows](https://dotnet.microsoft.com/download/dotnet-framework/net48))
- **Build** (optional): .NET Framework 4.8 Developer Pack, .NET SDK 8.0+ for multi-target builds

## Quick Start

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

Exit codes:
  0   Success
  1   Error (apply failed, restore had nothing to restore, etc.)
```

## Configuration

`appsettings.json` sits next to the executable.

```json
{
  "Enabled": true,
  "ReservedBottomPixels": 80,
  "Strategy": "auto",
  "OriginalWorkArea": null
}
```

| Field | Default | Description |
|---|---|---|
| `Enabled` | `true` | Master switch. When `false`, `apply` exits with code 1. |
| `ReservedBottomPixels` | `80` | Number of pixels to trim from the bottom of the work area. |
| `Strategy` | `"auto"` | `"SpSetWorkArea"` (SystemParametersInfo), `"ShAppBarMessage"` (Shell32 fallback), or `"auto"` (pick by config). |
| `OriginalWorkArea` | `null` | Stored automatically after `apply`. Used by `restore`. |

## Architecture

Clean Architecture with 4 projects:

```
ScreenSafe.Domain       → Models (ScreenRect, AppSettings) and interfaces
ScreenSafe.Application  → Use cases (Apply, Restore, Status)
ScreenSafe.Infrastructure → P/Invoke, strategies, persistence, platform detection
ScreenSafe.Console      → CLI entry point, DI composition root
```

### Strategy Pattern

`IWorkAreaManager` has two implementations:

| Strategy | API | Target |
|---|---|---|
| `SpSetWorkAreaStrategy` | `SystemParametersInfoW(SPI_SETWORKAREA)` | Windows 8.1 (primary) |
| `ShAppBarMessageStrategy` | `SHAppBarMessage(ABM_SETPOS)` | Windows 10+ (fallback — survives Explorer override) |

Strategy selection is driven by the `Strategy` config field. `"auto"` defaults to `SpSetWorkAreaStrategy` for broad compatibility, and the user can switch to `"ShAppBarMessage"` when the primary path is overridden by Explorer.

### Data Flow

```
CLI args → CliDispatcher → UseCase → IWorkAreaManager → P/Invoke → Win32 API
                              ↘ ISettingsRepository ↗
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
│   └── IWorkAreaManager.cs, IPlatformInfoProvider.cs,
│       IScreenInfoProvider.cs, ISettingsRepository.cs
├── ScreenSafe.Application/      # Use cases
│   ├── ApplyUseCase.cs
│   ├── RestoreUseCase.cs
│   └── StatusUseCase.cs
├── ScreenSafe.Infrastructure/   # P/Invoke + strategies + persistence
│   ├── NativeMethods/
│   │   ├── User32.cs
│   │   └── Shell32.cs
│   ├── SpSetWorkAreaStrategy.cs
│   ├── ShAppBarMessageStrategy.cs
│   ├── PlatformInfoProvider.cs
│   ├── ScreenInfoProvider.cs
│   ├── JsonSettingsRepository.cs
│   └── PlatformGuard.cs
├── ScreenSafe.Console/          # CLI entry point
│   ├── Program.cs
│   ├── CliDispatcher.cs
│   ├── app.manifest
│   └── appsettings.json
└── ScreenSafe.Tests/            # xUnit + Moq
    ├── Domain/
    ├── Application/
    ├── Console/
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

