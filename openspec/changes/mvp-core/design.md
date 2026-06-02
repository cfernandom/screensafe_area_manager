# Design: ScreenSafe Area Manager — MVP Core

## Technical Approach

4-project Clean Architecture on .NET Framework 4.8. Strategy pattern for `IWorkAreaManager` with dual implementations — `SpSetWorkAreaStrategy` (primary, Win 8.1) and `ShAppBarMessageStrategy` (fallback, Win 10+). Console project is the composition root, wiring DI at startup, selecting strategy based on config + runtime OS version. All Win32 P/Invoke isolated in Infrastructure via `[DllImport]`. Linux dev via platform guard — compiles everywhere, runs only on Windows.

## Architecture Decisions

| Decision | Options | Choice | Rationale |
|---|---|---|---|
| Runtime | .NET 8 vs .NET Framework 4.8 | .NET Framework 4.8 | Win 8.1 compat is non-negotiable; .NET 8 doesn't support it |
| P/Invoke | LibraryImport vs DllImport | DllImport | LibraryImport is .NET 5+ only, doesn't exist on 4.8 |
| JSON | Newtonsoft.Json vs System.Text.Json | System.Text.Json | Modern API, no 3rd-party dep via NuGet |
| DI | Host.CreateDefaultBuilder vs ServiceCollection | ServiceCollection | Host.CreateDefaultBuilder doesn't exist in .NET Framework |
| Screen resolution | WinForms.Screen vs GetSystemMetrics | GetSystemMetrics P/Invoke | Avoids WinForms assembly dependency |
| OS detection | Environment.OSVersion vs RtlGetVersion | Environment.OSVersion + app.manifest | Sufficient with proper manifest for 8.1; no extra P/Invoke |
| Strategy selection | Config-only vs config+DI+capability | Config + DI + runtime platform check | Fallback to Win10+ strategy without config change |
| Original work area | In-memory only vs persisted | Persisted in appsettings.json | Survive app restarts; restore works after reboot |

## Domain Model

```csharp
// ScreenSafe.Domain/Models/ScreenRect.cs
public readonly struct ScreenRect
{
    public int Left { get; }
    public int Top { get; }
    public int Right { get; }
    public int Bottom { get; }
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

// ScreenSafe.Domain/Models/AppSettings.cs
public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public int ReservedBottomPixels { get; set; } = 80;
    public ScreenRect? OriginalWorkArea { get; set; }
    public string Strategy { get; set; } = "SpSetWorkArea";
}

// ScreenSafe.Domain/Interfaces/
public interface IWorkAreaManager
{
    bool Apply(int reservedBottomPixels);
    bool Restore();
    (int left, int top, int right, int bottom)? GetStatus();
}
public interface IPlatformInfoProvider
{
    Version OSVersion { get; }
    string Architecture { get; }
    bool CanSupportStrategy(string strategy);
}
public interface IScreenInfoProvider
{
    int GetScreenWidth();
    int GetScreenHeight();
}
```

## Data Flow

```
CLI args → CliDispatcher → UseCase → IWorkAreaManager → Strategy → NativeMethods (P/Invoke)
                                  ↘ AppSettings (read/write) ↗
```

**Apply flow:**

```
Dispatcher("apply") → ApplyUseCase
  → IScreenInfoProvider.GetScreenHeight()   // detect screen
  → IWorkAreaManager.Apply(reservedPixels)
    → strategy detects current full RECT
    → stores original in AppSettings.OriginalWorkArea
    → calls SPI_SETWORKAREA(new RECT with bottom trimmed)
    → persists modified AppSettings
```

**Restore flow:**

```
Dispatcher("restore") → RestoreUseCase
  → reads AppSettings.OriginalWorkArea
  → IWorkAreaManager.Restore()
    → calls SPI_SETWORKAREA(original RECT)
    → clears OriginalWorkArea in AppSettings
    → persists
```

**Status flow:**

```
Dispatcher("status") → StatusUseCase
  → reads AppSettings + IScreenInfoProvider
  → IWorkAreaManager.GetStatus() via SPI_GETWORKAREA
  → displays current vs reserved vs original
```

## File Structure

```
src/
├── ScreenSafe.sln
├── ScreenSafe.Domain/
│   ├── ScreenSafe.Domain.csproj
│   ├── ScreenRect.cs
│   ├── AppSettings.cs
│   ├── IWorkAreaManager.cs
│   ├── IPlatformInfoProvider.cs
│   └── IScreenInfoProvider.cs
├── ScreenSafe.Application/
│   ├── ScreenSafe.Application.csproj
│   ├── ApplyUseCase.cs
│   ├── RestoreUseCase.cs
│   └── StatusUseCase.cs
├── ScreenSafe.Infrastructure/
│   ├── ScreenSafe.Infrastructure.csproj
│   ├── NativeMethods/
│   │   ├── User32.cs
│   │   └── Shell32.cs
│   ├── SpSetWorkAreaStrategy.cs
│   ├── ShAppBarMessageStrategy.cs
│   ├── PlatformInfoProvider.cs
│   ├── ScreenInfoProvider.cs
│   ├── JsonSettingsRepository.cs
│   └── PlatformGuard.cs
├── ScreenSafe.Console/
│   ├── ScreenSafe.Console.csproj
│   ├── Program.cs
│   ├── CliDispatcher.cs
│   ├── app.manifest
│   └── appsettings.json
└── ScreenSafe.Tests/
    ├── ScreenSafe.Tests.csproj
    ├── Domain/
    ├── Application/
    └── Infrastructure/
```

Zero Win32 dependencies in Domain. All P/Invoke in `NativeMethods/` in Infrastructure.

## Testing Strategy

| Layer | What to Test | How |
|---|---|---|
| Unit | ScreenRect calculations (pure) | xUnit `[Fact]` |
| Unit | ApplyUseCase with mock IWorkAreaManager | xUnit + Moq |
| Unit | RestoreUseCase with mock IWorkAreaManager | xUnit + Moq |
| Unit | StatusUseCase with mock dependencies | xUnit + Moq |
| Unit | AppSettings default + validation | xUnit `[Theory]` boundary tests |
| Integration | JsonSettingsRepository read/write/update | xUnit, temp file, `IDisposable` |
| Integration | PlatformInfoProvider on Windows | xUnit, conditional `[Fact(Skip)]` |
| Integration | ScreenInfoProvider on Windows | xUnit, conditional `[Fact(Skip)]` |

## Migration / Rollout

Greenfield project — no migration needed. Rollout = build → copy .exe + `appsettings.json` to target machine → run `apply`.

## Technical Backlog

1. Solution + project scaffolding (4 csproj files, .sln)
2. Domain: `ScreenRect`, `AppSettings`, 3 interfaces
3. Infrastructure: `NativeMethods` (User32, Shell32)
4. Infrastructure: `PlatformInfoProvider`, `ScreenInfoProvider`
5. Infrastructure: `SpSetWorkAreaStrategy`
6. Infrastructure: `ShAppBarMessageStrategy`
7. Infrastructure: `JsonSettingsRepository`, `PlatformGuard`
8. Application: `ApplyUseCase`
9. Application: `RestoreUseCase`
10. Application: `StatusUseCase`
11. Console: `CliDispatcher`, `Program.cs`, DI wiring
12. Console: `app.manifest`, `appsettings.json`
13. Tests: Domain unit tests
14. Tests: Application use case tests (mocked)
15. Tests: Infrastructure integration tests
16. Build validation on Linux + Windows VM

## Open Questions

None — all decisions resolved by proposal and exploration findings.
