# Exploration: ScreenSafe Area Manager — MVP Implementation

## Executive Summary

This exploration covers the technical feasibility and architectural decisions for building a Windows utility that reserves a configurable bottom strip of the primary display using .NET 8 and native Windows APIs. **Critical findings: (1) SPI_SETWORKAREA works on Windows 8.1 but is overridden by Explorer on Windows 10+, requiring a fallback strategy; (2) .NET 8 does NOT officially support Windows 8.1, creating a platform conflict with the project brief's target OS. Recommendations and tradeoffs for both scenarios are analyzed below.**

---

## 1. Windows WorkArea API: SPI_SETWORKAREA

### How It Works

`SystemParametersInfo(SPI_SETWORKAREA, 0, ref RECT, fWinIni)` sets the desktop work area — the region where maximized windows are constrained. The `RECT` defines the new bounds (left, top, right, bottom) in virtual screen coordinates. To reserve N pixels at the bottom:
- Leave `left = 0, top = 0`
- Set `right = screenWidth, bottom = screenHeight - reservedPixels`

The `fWinIni` flag controls persistence and notification:
- `SPIF_UPDATEINIFILE` (0x1) — writes to user profile (registry), persists across reboots
- `SPIF_SENDCHANGE` (0x2) — broadcasts `WM_SETTINGCHANGE` to all top-level windows
- Combine both with `0x1 | 0x2`

### Critical Finding — Windows 10+ Override

Microsoft KB 4014104 (archived) states:

> **In Windows 10, Windows Explorer overrides SPI_SETWORKAREA changes.** Explorer recalculates the work area when it receives `WM_SETTINGCHANGE`, using taskbar size and registered app bars only. Manual SPI_SETWORKAREA changes are **not** taken into account. This behavior is "by design."
>
> **This issue does not occur prior to Windows 10.**

This means:
- **Windows 8.1** — SPI_SETWORKAREA works correctly ✅
- **Windows 10/11** — SPI_SETWORKAREA changes are ephemeral and may be overridden by Explorer ❌

### Workarounds for Windows 10+

| Approach | Feasibility | Complexity |
|----------|-------------|------------|
| **SHAppBarMessage(ABM_SETPOS)** — register as an app bar that Explorer respects | High — the documented approach that aligns with Explorer's recalculation | Medium |
| **Temporarily kill/restart Explorer** after SPI_SETWORKAREA | Works but ugly — user loses shell for a moment | Low |
| **Periodically re-apply SPI_SETWORKAREA** in a loop | Not viable for a CLI-only MVP that should exit | Low |
| **Modify registry directly** (`HKEY_CURRENT_USER\Control Panel\Desktop\WorkArea`) and then trigger update | Fragile, undocumented | Medium |

### Edge Cases & Gotchas

- **Multi-monitor**: SPI_SETWORKAREA constrains to the monitor containing the specified rectangle — only affects that monitor
- **DPI scaling**: RECT coordinates must be in **physical pixels**, not virtual/dpi-scaled. Use `Screen.PrimaryScreen.Bounds` or P/Invoke `GetDeviceCaps` with `DESKTOPVERTRES`/`DESKTOPHORZRES`
- **Existing maximized windows**: Only respond to size change when work area is **reduced**. Restoring to full size may require manually sending `WM_SETTINGCHANGE` or using `SPIF_UPDATEINIFILE` alone (not combined with `SPIF_SENDCHANGE`)
- **Persistence**: SPI_SETWORKAREA with `SPIF_UPDATEINIFILE` writes to `HKEY_CURRENT_USER\Control Panel\Desktop\WorkArea` — survives reboot
- **RDP sessions**: Behavior depends on session type; console session is fine, remote desktop may exhibit different behavior

### Recommendation

✅ **Use SPI_SETWORKAREA as primary approach for Windows 8.1 target.**  
⚠️ **Add SHAppBarMessage fallback for Windows 10+ support.**  
The adapter should detect Windows version at runtime and choose the appropriate API. Given the MVP scope targets Windows 8.1 primarily, SPI_SETWORKAREA is acceptable, but the design MUST account for the Windows 10 override.

---

## 2. Clean Architecture in .NET 8 Console App

### Project Structure Options

#### Option A: Multi-Project Solution (Recommended)
```
screensafe/
├── ScreenSafe.sln
├── src/
│   ├── ScreenSafe.Domain/           # .NET 8 class library
│   │   ├── Models/
│   │   │   └── ScreenConfiguration.cs
│   │   ├── Services/
│   │   │   └── IWorkAreaService.cs
│   │   └── ScreenSafe.Domain.csproj
│   │
│   ├── ScreenSafe.Application/      # .NET 8 class library
│   │   ├── UseCases/
│   │   │   ├── ApplyWorkAreaUseCase.cs
│   │   │   ├── RestoreWorkAreaUseCase.cs
│   │   │   └── GetStatusUseCase.cs
│   │   └── ScreenSafe.Application.csproj
│   │
│   ├── ScreenSafe.Infrastructure/   # .NET 8 class library
│   │   ├── Windows/
│   │   │   ├── NativeMethods.cs        # P/Invoke declarations (partial class)
│   │   │   ├── WindowsWorkAreaAdapter.cs
│   │   │   └── IWindowsApi.cs          # Interface for testability
│   │   ├── Persistence/
│   │   │   ├── JsonSettingsRepository.cs
│   │   │   └── ISettingsRepository.cs
│   │   ├── Logging/
│   │   │   └── ConsoleLogger.cs
│   │   └── ScreenSafe.Infrastructure.csproj
│   │
│   ├── ScreenSafe.Console/          # .NET 8 console app
│   │   ├── Commands/
│   │   │   ├── ApplyCommand.cs
│   │   │   ├── RestoreCommand.cs
│   │   │   └── StatusCommand.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── ScreenSafe.Console.csproj
│   │
│   └── ScreenSafe.Tests/            # .NET 8 xUnit test project
│       ├── UnitTests/
│       │   ├── WorkAreaCalculationTests.cs
│       │   ├── ConfigurationValidationTests.cs
│       │   └── CommandParserTests.cs
│       ├── IntegrationTests/
│       │   ├── JsonPersistenceTests.cs
│       │   └── WorkAreaAdapterTests.cs
│       └── ScreenSafe.Tests.csproj
```

#### Option B: Single Project with Folders (Lighter)

```
screensafe/
└── ScreenSafe/
    ├── ScreenSafe.csproj
    ├── Domain/
    │   ├── ScreenConfiguration.cs
    │   └── IWorkAreaService.cs
    ├── Application/
    │   └── UseCases/
    ├── Infrastructure/
    │   ├── Windows/
    │   └── Persistence/
    ├── Commands/
    ├── Program.cs
    ├── appsettings.json
    └── Tests/
```

### Comparison

| Aspect | Multi-Project | Single Project |
|--------|--------------|----------------|
| Dependency enforcement | ✅ Compile-time (project refs) | ❌ Convention only |
| Testability | ✅ Clear boundaries | ✅ Still possible |
| Build complexity | Higher (need to restore all) | Lower |
| Overhead for MVP | Moderate | ✅ Lower |
| Future growth (GUI, tray icon) | ✅ Ready | ❌ Will need refactor |
| NuGet package count | 4 csproj files | 1 csproj file |

### Recommendation

For the MVP scope, **Option A (Multi-Project)** with a trimmed-down set of projects. The dependency rule is critical for testability and the brief explicitly asks for Clean Architecture. Use project references to enforce the dependency direction:

```
Domain ← Application ← Infrastructure
Console → Application (and DI wire-up to Infrastructure)
```

Do NOT reference Infrastructure from Domain or Application at compile time. Use dependency injection in the Console project's composition root.

---

## 3. P/Invoke Strategy

### DllImport vs LibraryImport (Source Generator)

| Aspect | DllImport (Legacy) | LibraryImport (.NET 7+) |
|--------|-------------------|------------------------|
| Marshalling | IL stub generated at runtime | Source-generated at compile time |
| AOT support | ❌ Not for NativeAOT | ✅ Works with NativeAOT |
| Performance | Runtime overhead | Better, can be inlined |
| `extern` required | Yes | Yes |
| `partial` method | No | ✅ Required |
| `SetLastError` | Supported | Supported |
| Struct marshalling | Automatic via marshaller | Only blittable structs (or custom marshaller) |
| `AllowUnsafeBlocks` | Not required | ✅ Required |
| Syntax | `[DllImport("user32.dll")] static extern ...` | `[LibraryImport("user32.dll")] static partial ...` |

### Recommendation

**Use `LibraryImport` for new .NET 8 code.** It's the modern, performant approach. The handful of Win32 APIs we need (`SystemParametersInfo`, `GetSystemMetrics`, `SHAppBarMessage`) all have blittable signatures, so no custom marshallers are needed. Set `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the Infrastructure csproj.

### Testability Pattern — Interface + Partial Class

```csharp
// Infrastructure/Windows/IWindowsApi.cs
public interface IWindowsApi
{
    bool GetWorkArea(out RECT rect);
    bool SetWorkArea(RECT rect);
    (int width, int height) GetScreenResolution();
}

// Infrastructure/Windows/NativeMethods.cs
internal static partial class NativeMethods
{
    internal const uint SPI_GETWORKAREA = 0x0030;
    internal const uint SPI_SETWORKAREA = 0x002F;
    internal const uint SPIF_UPDATEINIFILE = 0x0001;
    internal const uint SPIF_SENDCHANGE = 0x0002;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfoW(
        uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    [LibraryImport("gdi32.dll")]
    internal static partial int GetDeviceCaps(IntPtr hdc, int nIndex);
}

// Infrastructure/Windows/WindowsWorkAreaAdapter.cs
public sealed class WindowsWorkAreaAdapter : IWindowsApi
{
    public bool GetWorkArea(out RECT rect) { ... }
    public bool SetWorkArea(RECT rect) { ... }
}
```

The `IWindowsApi` interface is in `Domain` (or `Application`). The concrete impl is in `Infrastructure`. Tests mock the interface.

### CsWin32 Alternative

**Microsoft.Windows.CsWin32** is a NuGet source generator that auto-generates Win32 P/Invoke declarations from metadata. Pros: zero hand-written P/Invoke, always up to date. Cons: more complexity for < 10 function calls, adds dependency. For the MVP, **manual LibraryImport declarations** are simpler and sufficient.

---

## 4. Testing Strategy

### Unit Tests (xUnit)

| Test Subject | What to Test | How |
|-------------|-------------|-----|
| `ScreenConfiguration` | Validation (`reservedBottomPixels` range), equality | Pure logic, no mocks |
| `ApplyWorkAreaUseCase` | Correct RECT calculation, calls IWindowsApi correctly | Mock `IWindowsApi` |
| `RestoreWorkAreaUseCase` | Calls SetWorkArea with original RECT | Mock `IWindowsApi` |
| `GetStatusUseCase` | Returns correct status from IWindowsApi + settings | Mock both interfaces |
| `CommandParser` | Parses `apply`/`restore`/`status` from args | Direct input/output |
| `WorkAreaCalculator` | Computes RECT from screen size + reserved pixels | Pure function |

### Integration Tests

| Test Subject | What to Test | How |
|-------------|-------------|-----|
| `JsonSettingsRepository` | Read/write/update `appsettings.json` | Temp directory, real file I/O |
| `WindowsWorkAreaAdapter` | (Optional, needs Windows) | Conditional `[Fact(Skip="Requires Windows")]` |

### Test Project Setup

```xml
<!-- ScreenSafe.Tests.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  <PackageReference Include="xunit.v3" Version="*" />
  <PackageReference Include="Moq" Version="4.*" />
  <PackageReference Include="FluentAssertions" Version="6.*" />
</ItemGroup>
```

### Mock Strategy for P/Invoke

```csharp
// Domain layer defines the interface
public interface IWorkAreaService
{
    bool TryGetCurrentWorkArea(out ScreenRect rect);
    Result ApplyWorkArea(ScreenRect rect);
    ScreenResolution GetCurrentResolution();
}

// Tests mock the interface
var mock = new Mock<IWorkAreaService>();
mock.Setup(m => m.GetCurrentResolution())
    .Returns(new ScreenResolution(1920, 1080));
```

### xUnit Patterns

- **`[Fact]`** for pure unit tests
- **`[Theory]`** with `[InlineData]` for boundary tests (reservedBottomPixels = 0, negative, large)
- **`IDisposable`** in test classes for temp file cleanup in integration tests
- **`[Collection]`** for shared test contexts

---

## 5. Cross-Platform Concern (Linux Dev, Windows Target)

### Approaches

#### Approach A: Runtime Guard (Recommended)
```csharp
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("ScreenSafe requires Windows to run.");
    return ExitCode.PlatformNotSupported;
}
```
- **Pros**: Simple, single build, no conditional compilation
- **Cons**: Still need to handle graceful failure in `Main()`

#### Approach B: Conditional Compilation
```xml
<!-- In csproj: Auto-detect from MSBuild -->
<IsWindows Condition="$([MSBuild]::IsOSPlatform('Windows'))">true</IsWindows>
<DefineConstants Condition="'$(IsWindows)'=='true'">WINDOWS</DefineConstants>
```
```csharp
#if WINDOWS
    // SPI_SETWORKAREA calls
#endif
```
- **Pros**: Clean separation, no dead code on Linux
- **Cons**: Need two separate builds for testing vs deployment

#### Approach C: Target `net8.0-windows` (Windows-only TFM)
```xml
<TargetFramework>net8.0-windows</TargetFramework>
```
- **Pros**: Compile-time guard — won't even compile on Linux
- **Cons**: Cannot `dotnet build` on Linux at all

### Recommendation

Use **Approach A (Runtime Guard)** with `OperatingSystem.IsWindows()` in the console entry point. Keep the Infrastructure assembly buildable on Linux (for compilation and unit test purposes). The P/Invoke calls will never execute on Linux, but they compile just fine — the DllNotFoundException/LibraryImport will only throw if actually invoked.

For `ScreenSafe.Infrastructure.csproj`:
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

Add a `PlatformGuard.cs`:
```csharp
internal static class PlatformGuard
{
    internal static void AssertWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "ScreenSafe requires Windows. Running on: " +
                RuntimeInformation.OSDescription);
    }
}
```

---

## 6. JSON Persistence Strategy

### Format

```json
{
  "enabled": true,
  "reservedBottomPixels": 80
}
```

### File Location

| Strategy | Path | Pros | Cons |
|----------|------|------|------|
| **Side-by-side** | `appsettings.json` next to the exe | ✅ Simple, user can edit | ❌ Needs write access to install dir (maybe `Program Files`) |
| **AppData** | `%LOCALAPPDATA%\ScreenSafe\settings.json` | ✅ User-writable, per-user, clean | ❌ Harder for user to find/edit |
| **Current Directory** | `./appsettings.json` | ✅ Works for portable use | ❌ Location is ambiguous |

### Recommendation

**Use side-by-side (`appsettings.json` next to the .exe) for the MVP.** The brief specifies this path. If the tool is installed in `Program Files`, this will cause permission issues — but the MVP is CLI-only (no installer), so it will likely run from a user-writable directory.

### Implementation

```csharp
public sealed class JsonSettingsRepository : ISettingsRepository
{
    private readonly string _filePath;

    public JsonSettingsRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            AppContext.BaseDirectory, "appsettings.json");
    }

    public Settings Load()
    {
        if (!File.Exists(_filePath))
            return Settings.Default;

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<Settings>(json)
               ?? Settings.Default;
    }

    public void Save(Settings settings)
    {
        var json = JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
```

**No NuGet packages needed** — `System.Text.Json` is built into .NET 8. Only use `Microsoft.Extensions.Configuration` if you need the full configuration stack; for MVP, direct `JsonSerializer` is lighter.

---

## 7. .NET 8 + Windows 8.1 Compatibility (CRITICAL RISK)

**.NET 8 does NOT officially support Windows 8.1.** The supported Windows client list starts at Windows 10 (1607).

| OS | .NET 8 Support | SPI_SETWORKAREA |
|----|---------------|-----------------|
| Windows 8 (.NET 4.5.x) | ❌ | ✅ Works |
| Windows 8.1 | ❌ (officially) | ✅ Works |
| Windows 10 | ✅ | ❌ Overridden by Explorer |
| Windows 11 | ✅ | ❌ Overridden by Explorer |

### Options to Resolve

| Option | Pros | Cons |
|--------|------|------|
| **Use .NET Framework 4.8** (not .NET 8) | ✅ Supports Win 8.1 natively | ❌ No cross-platform build, older language features |
| **Use .NET 8 self-contained + ignore unsupported** | ✅ Single toolchain | ❌ Risky — may hit runtime bugs on Win 8.1 |
| **Target Win 10 only, drop Win 8** | ✅ Clean modern stack | ❌ Breaks the brief's use case |
| **Use .NET 8 with `net8.0-windows` TFM, test on Win 8.1** | ✅ May work in practice (many .NET 8 APIs don't require Win 10) | ❌ Unofficial, no guarantees |

### Recommendation

**Flag this for user decision.** Provide both paths in the proposal:
1. **Preferred**: Upgrade target to Windows 10 (spi_setworkarea + SHAppBarMessage fallback)
2. **Fallback**: Use .NET Framework 4.8 if Windows 8.1 is non-negotiable

The exploration assumes the user will decide. The architecture is the same either way — only the TFM changes.

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| **.NET 8 incompatible with Windows 8.1** | High — cannot run on target OS | Flag to user; propose .NET Framework 4.8 or Win 10 target |
| **SPI_SETWORKAREA overridden on Windows 10+** | High — work area disappears after apply | Design adapter with SHAppBarMessage fallback |
| **Cannot test on Windows during development** | Medium — only Linux dev environment | CI/CD builds produce Windows binary; validate in VM |
| **Existing maximized windows don't resize when restoring** | Medium — user might think it didn't work | Implement WM_SETTINGCHANGE broadcast or auto-minimize/restore |
| **DPI handling wrong** | Medium — incorrect RECT in high-DPI | Use proper DPI-aware RECT from GetDeviceCaps or Screen.Bounds |
| **No admin rights** | Medium — SPI_SETWORKAREA might fail | Document requirement; check error codes gracefully |

---

## Ready for Proposal

**Yes, with one caveat**: the .NET 8 + Windows 8.1 incompatibility must be resolved by a user decision before the proposal finalizes. The proposal should present both paths.

### Recommended Next Phase
`sdd-propose` — but only after the user clarifies the target OS decision.
