# Proposal: ScreenSafe Area Manager — MVP Core

## Intent

CLI-only Windows utility that reserves a configurable bottom screen strip,
compensating for defective LCD areas on a Windows 8.1 all-in-one machine.
Must survive Explorer override on Windows 10+ via dual P/Invoke strategies.

## Scope

### In Scope
- 4-project solution: `Domain → Application → Infrastructure → Console`
- `IWorkAreaManager` interface + 2 strategies (SpSetWorkArea primary, ShAppBarMessage fallback)
- Strategy selection via config + DI (`Microsoft.Extensions.DependencyInjection`)
- CLI: `apply` (set work area), `restore` (reset to full), `status` (current state)
- JSON config persistence (`appsettings.json` side-by-side with exe)
- Platform guards for Linux dev (compile on Linux, fail gracefully at runtime)
- xUnit + Moq unit tests + integration tests for persistence

### Out of Scope
- GUI / tray icon
- Multi-monitor support
- Windows Service, auto-updater, installer
- Advanced logging (Serilog)
- .NET 8 migration (deferred)

## Capabilities

### New Capabilities
- `work-area-management`: Reserve, restore, and query the Windows desktop work area via dual strategy pattern
- `config-persistence`: JSON-based settings read/write (`appsettings.json`)
- `cli-interface`: Console entry point with `apply`/`restore`/`status` commands, DI composition root, platform guard

### Modified Capabilities
None — greenfield project.

## Approach

Multi-project Clean Architecture on .NET Framework 4.8. Domain defines 3
interfaces: `IWorkAreaManager`, `IPlatformInfoProvider`, `IScreenInfoProvider`.

**IWorkAreaManager** — two strategies in Infrastructure:
1. `SpSetWorkAreaStrategy` (primary): `SystemParametersInfo(SPI_SETWORKAREA)`
2. `ShAppBarMessageStrategy` (fallback): `SHAppBarMessage(ABM_SETPOS)`

Strategy selection via config (`appsettings.json: strategy`) + DI + runtime
platform check (`IPlatformInfoProvider.Capability`). Console project is
the composition root. P/Invoke via `[DllImport]` (required for .NET Framework 4.8;
`LibraryImport` is .NET 5+).

**IPlatformInfoProvider** — wraps `RuntimeInformation` + `Environment.OSVersion`
(with app.manifest for reliable Win 8.1 detection). Returns OS version,
architecture, and strategy capability.

**IScreenInfoProvider** — wraps `GetSystemMetrics(SM_CXSCREEN / SM_CYSCREEN)`
via P/Invoke. Avoids coupling to `System.Windows.Forms.Screen`.

**Original Work Area persistence** — `apply` stores the original RECT in
`appsettings.json` before modifying. `restore` reads that exact RECT and calls
`SPI_SETWORKAREA` with it — no calculation, no reconstruction.

No unnecessary patterns beyond Clean Architecture + Strategy. Toda llamada a
User32/Shell32 queda en Infrastructure. Platform guard at `Main()` prevents
Win32 calls on non-Windows OS.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ScreenSafe.sln` | New | Solution file, 4 projects |
| `src/ScreenSafe.Domain/` | New | Models (`ScreenRect`, `AppSettings`), interfaces (`IWorkAreaManager`, `IPlatformInfoProvider`, `IScreenInfoProvider`) |
| `src/ScreenSafe.Application/` | New | Use cases: `ApplyUseCase`, `RestoreUseCase`, `StatusUseCase` |
| `src/ScreenSafe.Infrastructure/` | New | `NativeMethods` (P/Invoke), 2 strategy impls, `PlatformInfoProvider`, `ScreenInfoProvider`, `JsonSettingsRepository` |
| `src/ScreenSafe.Console/` | New | `Program.cs`, CLI dispatch, DI composition, `app.manifest`, `appsettings.json` |
| `src/ScreenSafe.Tests/` | New | Unit tests (use cases, config, strategies mock), integration tests (persistence) |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| SPI_SETWORKAREA overridden by Explorer (Win 10+) | Medium | SHAppBarMessage fallback strategy |
| Cannot test Win32 APIs on Linux dev machine | High | Platform guards + VM validation |
| .NET Framework 4.8 missing modern APIs | Low | NuGet backports (DI, System.Text.Json) |

## Rollback Plan

Run `restore` — calls SPI_SETWORKAREA with original full-screen RECT. Delete
`appsettings.json` to reset config. If SPI fails on Win 10+, switch strategy
strategy via config. Reboot clears any stale work area state.

## Dependencies

- .NET Framework 4.8 Developer Pack
- NuGet: `Microsoft.Extensions.DependencyInjection`, `System.Text.Json`
- NuGet (test): `xUnit`, `Moq`

## Success Criteria

- [ ] `apply` reserves N pixels at screen bottom on Win 8.1
- [ ] `restore` returns work area to full screen
- [ ] `status` shows current work area + reserved pixels
- [ ] Settings persist across CLI invocations
- [ ] Linux build compiles, shows platform error at runtime
- [ ] xUnit tests pass on both Linux (unit) and Windows (integration)
