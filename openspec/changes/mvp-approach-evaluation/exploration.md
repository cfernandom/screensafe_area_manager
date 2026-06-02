# Exploration: PowerShell vs AutoHotkey vs C# for ScreenSafe MVP

## Current State

The ScreenSafe Area Manager MVP is specified as a C# .NET 8 Console Application that uses `SystemParametersInfo(SPI_SETWORKAREA)` to reserve a configurable bottom strip of the primary display. An existing exploration (`sdd/mvp-core/explore`) already identified two critical risks:

1. **Windows 10+ Explorer override**: SPI_SETWORKAREA changes are overridden by Explorer on Windows 10+. The recommended fallback is `SHAppBarMessage(ABM_SETPOS)` — registering as an invisible app bar that Explorer respects.
2. **.NET 8 + Windows 8.1 incompatibility**: .NET 8 does NOT officially support Windows 8.1, creating a platform conflict with the project brief's target OS.

The project is entirely greenfield — no `.csproj`, `.sln`, or source code exists. Only the MVP brief, SDD init artifacts, and the `mvp-core` exploration exist.

This evaluation compares three alternative implementation approaches for the same functional requirements:

| # | Requirement | RF |
|---|-------------|----|
| 1 | Detect current screen resolution | RF-001 |
| 2 | Reserve configurable bottom pixels via Windows API | RF-002, RF-003 |
| 3 | Restore original work area | RF-004 |
| 4 | Read/write JSON config (`appsettings.json`) | RF-005 |
| 5 | CLI commands: `apply`, `restore`, `status` | RF-006 |
| 6 | Handle Win10+ Explorer override (SHAppBarMessage fallback) | RF-003 |
| 7 | Basic event logging | (implied) |
| 8 | SDD/TDD workflow | (brief §3) |

---

## Approach Comparison

### PowerShell (.ps1)

**Feasibility**: ⚠️ — technically possible, but with significant caveats

#### SystemParametersInfo via Add-Type

PowerShell can call native APIs via `Add-Type` with embedded C#:

```powershell
$MemberDefinition = @'
[StructLayout(LayoutKind.Sequential)]
public struct RECT {
    public int left;
    public int top;
    public int right;
    public int bottom;
}

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool SystemParametersInfoW(
    uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);
'@

$Win32 = Add-Type -MemberDefinition $MemberDefinition `
    -Name 'Win32' -Namespace 'ScreenSafe' -PassThru
```

**Problem**: `Add-Type` compiles the C# code at runtime. Each invocation takes **300–800ms** of JIT compilation before any API call runs. The struct and method definitions cannot be conditionally included — all code is compiled at once.

#### SHAppBarMessage Fallback

```powershell
$AppBarMemberDef = @'
[StructLayout(LayoutKind.Sequential)]
public struct APPBARDATA {
    public int cbSize;
    public IntPtr hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public IntPtr lParam;
}

[DllImport("shell32.dll")]
public static extern IntPtr SHAppBarMessage(
    uint dwMessage, ref APPBARDATA pData);
'@
```

**Critical issue**: SHAppBarMessage requires a window handle (`hWnd`) with a message pump to receive callback notifications. In PowerShell:
- Can create a hidden `System.Windows.Forms.Form` via `Add-Type` to get an `hWnd`
- BUT: requires pumping messages (`Application.DoEvents()`), which conflicts with the CLI-exit model
- The script must stay alive briefly to let Explorer process the appbar registration
- Much harder to manage lifecycle (ABM_NEW → ABM_QUERYPOS → ABM_SETPOS → ABM_REMOVE)

#### JSON Persistence ✅

```powershell
# Read
$config = Get-Content "appsettings.json" | ConvertFrom-Json
$reserved = $config.reservedBottomPixels

# Write
$config | ConvertTo-Json | Set-Content "appsettings.json"
```

`ConvertFrom-Json` / `ConvertTo-Json` are built-in and work well. No external dependencies.

#### CLI Argument Parsing ✅

```powershell
param(
    [ValidateSet('apply','restore','status')]
    [string]$Command
)

switch ($Command) {
    'apply'   { Apply-WorkArea }
    'restore' { Restore-WorkArea }
    'status'  { Get-Status }
}
```

`param()` block works fine. Validation with `ValidateSet` is built-in.

#### Logging ✅

`Write-Host`, `Start-Transcript`, or custom logging functions. Simple.

#### Testing (Pester)

Pester v5 (built into PowerShell 7+) can unit test PowerShell functions. **BUT**: the core P/Invoke wrappers created by `Add-Type` cannot be mocked or tested — they are compiled C# types injected at runtime. You would need to wrap every API call in a PowerShell function and test the wrapper, not the actual P/Invoke.

```powershell
Describe "WorkArea" {
    It "Calculates correct RECT" {
        $result = Get-NewWorkArea -ScreenHeight 1080 -ReservedPixels 80
        $result.bottom | Should -Be 1000
    }
}
```

- Pure logic functions: testable ✅
- P/Invoke wrappers (Add-Type): NOT testable in isolation ❌
- No dependency injection at the PowerShell script level

#### Distribution

| Factor | Issue |
|--------|-------|
| **Execution Policy** | Default `Restricted` on Windows clients blocks ALL .ps1 files. Requires `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser` or running via `powershell.exe -ExecutionPolicy Bypass -File .\script.ps1` |
| **PowerShell Version** | PowerShell 7+ needed for .NET Core compat (but only ships with Win10+). Windows PowerShell 5.1 is pre-installed but is .NET Framework-based |
| **Startup time** | 200–500ms for pwsh.exe startup + 300–800ms for Add-Type JIT = **~1 second overhead** before any work |
| **Single file** | Can be single `.ps1` but `Add-Type` generates temporary `.cs` files on disk |
| **No .exe** | Cannot produce a standalone executable. Requires PowerShell installed |

#### Key Limitations
1. **Execution policy** is the #1 distribution barrier — enterprise machines lock this down via GPO
2. **Add-Type compilation** adds startup latency and temporary file clutter
3. **SHAppBarMessage message pump** is extremely awkward in a non-interactive PowerShell script
4. **Cannot produce self-contained .exe** — user must have PowerShell
5. **No Clean Architecture** — PowerShell scripts encourage procedural code, not layered architecture
6. **SDD/TDD mismatch**: Pester is good for testing PowerShell functions, but the core Win32 interop layer is untestable; no Clean Architecture pattern exists for PowerShell

#### Effort Estimate
- MVP implementation: **Medium** (3–4 days for a skilled PowerShell dev)
- Testing: **Low** coverage for P/Invoke layer
- SDD workflow: **Poor fit** — SDD assumes a compile-time type system and layered architecture

---

### AutoHotkey v2 (.ahk)

**Feasibility**: ⚠️ — technically capable, strong for system-level APIs, but weak for SDD/TDD

#### SystemParametersInfo via DllCall

AutoHotkey v2 has first-class `DllCall` support with `Buffer` for structs:

```ahk
; RECT struct: 4 x Int32 = 16 bytes
Rect := Buffer(16)

; SPI_GETWORKAREA = 0x0030
DllCall("SystemParametersInfo", "UInt", 0x0030, "UInt", 0,
    "Ptr", Rect, "UInt", 0)

; Read the RECT values back
left   := NumGet(Rect, 0, "Int")
top    := NumGet(Rect, 4, "Int")
right  := NumGet(Rect, 8, "Int")
bottom := NumGet(Rect, 12, "Int")

; SPI_SETWORKAREA = 0x002F
; Modify bottom to reserve N pixels
NumPut("Int", 0,    "Int", 0,
       "Int", right, "Int", ScreenHeight - ReservedPixels, Rect)
DllCall("SystemParametersInfo", "UInt", 0x002F, "UInt", 0,
    "Ptr", Rect, "UInt", 0x0003)  ; SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
```

This is **cleaner than PowerShell** because:
- No Add-Type compilation needed — DllCall is native
- Buffer() allocates and manages memory automatically
- NumPut/NumGet handle struct member access

#### SHAppBarMessage Fallback ✅ (AHK's secret weapon)

```ahk
; APPBARDATA structure layout:
; cbSize (4), hWnd (8/4), uCallbackMessage (4), uEdge (4),
; rc (RECT = 16), lParam (8/4)
; Total: 40 bytes on 32-bit, 48 on 64-bit
cbSize := A_PtrSize = 8 ? 48 : 40
abd := Buffer(cbSize)

NumPut("UInt", cbSize, abd, 0)          ; cbSize
NumPut("Ptr",  A_ScriptHwnd, abd, 4)    ; hWnd — AHK's own hidden window!
NumPut("UInt", 0x8000, abd, 4 + A_PtrSize) ; uCallbackMessage (WM_APP)

; ABM_NEW = 0x00000000
DllCall("SHAppBarMessage", "UInt", 0x00000000, "Ptr", abd)

; ABM_QUERYPOS = 0x00000002, ABM_SETPOS = 0x00000003
NumPut("UInt", 3, abd, 4 + A_PtrSize + 4)  ; uEdge = ABE_BOTTOM
DllCall("SHAppBarMessage", "UInt", 0x00000002, "Ptr", abd)  ; ABM_QUERYPOS
DllCall("SHAppBarMessage", "UInt", 0x00000003, "Ptr", abd)  ; ABM_SETPOS

; ABM_REMOVE = 0x00000001
DllCall("SHAppBarMessage", "UInt", 0x00000001, "Ptr", abd)
```

**Key advantage over C# and PowerShell**: AHK has a built-in window message pump. `A_ScriptHwnd` provides an hWnd for free. `OnMessage()` handles callbacks naturally. The SHAppBarMessage lifecycle (register → query → set → remove) integrates naturally into AHK's event model.

#### JSON ❌ — No built-in support

AutoHotkey v2 has **zero built-in JSON parsing**. You must use a third-party library:
- **jsongo** (pure AHK, ~600 lines)
- **cJson** (C-based DLL wrapper)
- **thqby/JSON.ahk** (pure AHK, ~200 lines)

```ahk
#Include "JSON.ahk"

; Read
config := FileRead("appsettings.json")
parsed := JSON.parse(config)
enabled := parsed["enabled"]
reserved := parsed["reservedBottomPixels"]

; Write
config := {enabled: true, reservedBottomPixels: 80}
json := JSON.stringify(config, , "  ")
FileOpen("appsettings.json", "w").Write(json)
```

This adds a file dependency. The file must be distributed alongside the script/exe.

#### CLI Argument Parsing ✅

```ahk
; A_Args is a built-in array of CLI arguments
if A_Args.Length == 0 {
    MsgBox "Usage: screensafe apply|restore|status"
    ExitApp
}

switch A_Args[1] {
    case "apply":   ApplyWorkArea()
    case "restore": RestoreWorkArea()
    case "status":  ShowStatus()
    default:        MsgBox "Unknown command"
}
```

Simple and functional. No validation framework beyond manual checks.

#### Testing (unit testing frameworks)

Several frameworks exist for AHK v2, but none are mature/standard:

| Framework | Type | Verdict |
|-----------|------|---------|
| **Yunit** | Simple test runner | ✅ works but minimal (no assertions, just try/catch patterns) |
| **AutoHotUnit** | Modern framework with fixtures | ⚠️ v2 support, assertion library, CI-compatible |
| **expect.ahk** | TAP-compliant assertions | ⚠️ minimalist, 9 assertion methods |
| **ahk-testlib** | test discovery + runners | ⚠️ VS Code test explorer integration |
| **AHKUnit** | VS Code extension | ⚠️ IDE integration, coverage reports |

Key limitation: **No dependency injection** — AHK has no interface system, no constructor injection, no mocking. Testability requires passing function references or using global state.

#### Distribution: Ahk2Exe ✅

This is AHK's strongest distribution advantage:

```bash
Ahk2Exe.exe /in screensafe.ahk /out screensafe.exe /base AutoHotkey64.exe
```

- Produces a **standalone .exe** (~1–3 MB)
- No runtime dependency (AutoHotkey interpreter is bundled)
- User runs a single .exe file
- Supports icon customization, version info, compression

The compiled EXE includes the AHK interpreter + script. The user does NOT need AutoHotkey installed.

#### Key Limitations
1. **No built-in JSON** — requires a third-party library file
2. **Limited test ecosystem** — no mocking, no dependency injection, no Clean Architecture
3. **SDD/TDD mismatch**: AHK has no concept of interfaces, project references, or compile-time dependency enforcement — the foundation of Clean Architecture
4. **Native DllCall is unsafe** — wrong struct offset = silent crash. No type safety whatsoever
5. **Less mainstream** — fewer devs, less community support, harder to hire for
6. **Ahk2Exe distribution** requires Windows tooling (can't cross-compile from Linux)

#### Effort Estimate
- MVP implementation: **Low–Medium** (2–3 days) — DllCall is fast to write, message pump is native
- Testing: **Low** coverage for API layer
- SDD workflow: **Poor fit** — no architectural enforcement, no structured testing pattern

---

### C# .NET 8 (Baseline)

**Feasibility**: ✅ — Well-established approach, full SDD/TDD support, but with the .NET 8 + Windows 8.1 risk

#### Key Advantages
- **Clean Architecture**: project references enforce dependency direction (Domain ← Application ← Infrastructure ← Console)
- **xUnit + Moq**: full testability at every layer. `IWindowsApi` interface → mock → unit test
- **System.Text.Json**: built-in, zero-effort JSON
- **LibraryImport (source generator)**: modern P/Invoke with compile-time safety
- **Self-contained publish**: `dotnet publish -r win-x64 --self-contained` produces a single folder with everything needed
- **SDD/TDD**: designed for this workflow from day one

#### Key Disadvantages
- **.NET 8 + Win 8.1 incompatibility** (critical): .NET 8 does not officially support Windows 8.1. Can use .NET Framework 4.8 (on Windows only) or target Win 10+
- **SHAppBarMessage message pump**: Console Application does not have a built-in message loop. Need `Application.Run()` or a custom message pump
- **Linux dev → Windows target**: cannot test API calls during development
- **Startup time**: .NET 8 self-contained ~50ms, framework-dependent faster

#### Effort Estimate
- MVP implementation: **Medium** (4–5 days including project scaffolding, Clean Architecture, tests)
- Testing: ✅ **Full** — xUnit + Moq for every layer
- SDD workflow: ✅ **Excellent fit**

---

## Detailed Comparison Matrix

| Criterion | PowerShell | AutoHotkey v2 | C# .NET 8 |
|-----------|-----------|---------------|-----------|
| **SystemParametersInfo** | ⚠️ Add-Type + C# embed | ✅ Native DllCall + Buffer | ✅ LibraryImport |
| **SHAppBarMessage fallback** | ❌ No message pump; very awkward | ✅ Native message loop + A_ScriptHwnd | ⚠️ Needs manual message pump |
| **JSON persistence** | ✅ ConvertFrom/To-Json | ❌ Third-party library required | ✅ System.Text.Json |
| **CLI argument parsing** | ✅ param() block | ⚠️ Manual (A_Args, no validation) | ✅ Many options (System.CommandLine, etc.) |
| **Testing framework** | ⚠️ Pester (P/Invoke untestable) | ❌ Fragmented, no mocking | ✅ xUnit, Moq, FluentAssertions |
| **Dependency injection** | ❌ Not possible | ❌ Not possible | ✅ Microsoft.Extensions.DI |
| **Clean Architecture** | ❌ No | ❌ No | ✅ Project refs enforce layering |
| **Standalone .exe** | ❌ No | ✅ Ahk2Exe (~1–3 MB) | ✅ dotnet publish (~15–30 MB) |
| **Runtime dependency** | Requires PowerShell | None (compiled .exe) | Requires .NET Runtime OR self-contained |
| **Execution policy** | ❌ Major blocker on enterprise | N/A (compiled .exe) | N/A (.exe) |
| **Startup time** | ⚠️ ~1s (pwsh + Add-Type JIT) | ✅ Near-instant | ✅ ~50ms |
| **Windows 8.1 compat** | ⚠️ PW7 no, WPS5.1 yes | ✅ Native Win7+ | ❌ .NET 8 unsupported |
| **Memory footprint** | ~30–50 MB | ~3–10 MB | ~10–30 MB |
| **Dev on Linux** | ✅ pwsh available | ❌ No runtime | ✅ dotnet build works |
| **SDD/TDD fit** | ❌ Poor | ❌ Poor | ✅ Excellent |
| **Maintainability** | ❌ Low (procedural, no DI) | ⚠️ Low-Med (struct offsets fragile) | ✅ High (layered, tested) |
| **Team scalability** | ❌ Low | ❌ Very low (niche language) | ✅ High |

---

## Recommendation

**Stick with C# .NET 8 as specified in the brief.** Here's why:

### 1. The brief explicitly requires C#
Section 4 (Plataforma Tecnológica) specifies **C# as the language**, **.NET 8 as the runtime**, **Console Application as the type**. Deviating from this without a compelling technical reason would violate the project specification.

### 2. SDD/TDD demands Clean Architecture — scripts cannot deliver it
The brief requires Specification Driven Development and Test Driven Development (§3). This is **not optional process noise** — it's a core requirement. Clean Architecture with dependency inversion, interface-based mocking, and layered project references is the foundation of testable design.

- PowerShell: Pester can test functions but cannot test `Add-Type` P/Invoke wrappers. No DI. No interface enforcement.
- AutoHotkey: Testing frameworks exist but are fragmented. No DI. No interface system. No compile-time safety.
- **C#: xUnit + Moq directly support the TDD workflow.** `IWorkAreaService` → mock → verify calls → done.

### 3. SHAppBarMessage complexity is a C# strength, not weakness
The message pump concern is solved by creating a hidden `System.Windows.Forms.Form` or using `Windows.Win32` P/Invoke with a custom message loop. This is **well-documented terrain** with thousands of examples. In PowerShell, the same task is near-impossible. In AutoHotkey it's easier, but at the cost of everything else.

### 4. .NET 8 + Windows 8.1 — this is a decision for the user, not a justification to switch
The incompatibility risk exists regardless of language. PowerShell 7 has the same problem (doesn't support Win 8.1). AutoHotkey v2 runs on Win 7+ natively, but the SDD/TDD loss outweighs this gain.

### 5. Distribution and maintainability
- C# compiles to a .exe that any Windows user runs. No execution policy, no runtime dependency (with self-contained publish).
- PowerShell scripts require execution policy changes — a real blocker on enterprise-managed machines.
- AutoHotkey compiled .exes are tiny but the language is niche — harder to maintain long-term.

### The Only Scenario Where Switching Makes Sense
If **Windows 8.1 support is non-negotiable AND the team cannot mitigate the .NET 8 incompatibility**:
- **Use .NET Framework 4.8** (same C# code, different TFM) instead of switching languages
- AutoHotkey would be a distant third choice, only if neither .NET 8 nor .NET Framework 4.8 is viable

---

## Risks of Switching to a Script Approach

| Risk | Impact | Details |
|------|--------|---------|
| **SDD workflow breaks** | High | The entire SDD toolchain (spec → design → tasks → apply → verify → archive) assumes compile-time type checking, DI, and project references. Scripts cannot participate in this pipeline. |
| **TDD becomes ceremonial** | High | Without testable interfaces and DI, "TDD" reduces to testing pure math functions. The critical Win32 interop layer remains untested. |
| **Execution policy blocks distribution** | High (PowerShell) | If the target is a locked-down enterprise Win 8.1 machine, the default `Restricted` policy blocks the .ps1 entirely. The user must know to run `powershell -ExecutionPolicy Bypass`. |
| **SHAppBarMessage on PowerShell** | Medium | The Win10+ fallback requires a message pump — PowerShell is a terrible fit for this. Without it, the tool silently fails on Win10+. |
| **JSON dependency for AHK** | Low | jsongo/JSON.ahk is stable, but adds a file to ship and maintain. Minor but real overhead. |
| **AHK is niche** | Low-Medium | Fewer developers know it. Documentation is thinner. Community is smaller. Long-term maintenance risk. |
| **Cannot cross-compile AHK from Linux** | Medium | Ahk2Exe only runs on Windows. The dev team is on Linux/Ubuntu. Would need a Windows VM just for packaging. |
| **No Clean Architecture evolution path** | Medium | The brief mentions GUI and tray icon as future scope. A script-based MVP cannot evolve into a well-architected GUI app — it would need a complete rewrite. The C# version can add Windows Forms/WPF natively. |

---

## Ready for Proposal

**Yes.** The recommendation is clear: stick with C# .NET 8 as specified. Present this comparison to the user alongside the existing `mvp-core` exploration, and let them confirm before proceeding to the proposal phase.

### What the orchestrator should tell the user

"The exploration is complete. Three approaches were evaluated:

1. **PowerShell** — technically feasible but crippled by execution policy restrictions, Add-Type compilation overhead, and inability to handle SHAppBarMessage message pumping. **Not recommended.**

2. **AutoHotkey v2** — technically capable, excellent for system-level API calls thanks to native DllCall and built-in message loop. BUT: no built-in JSON, fragmented testing ecosystem, no DI/mocking, no Clean Architecture support. **Not recommended for an SDD/TDD project.**

3. **C# .NET 8** — remains the best choice despite the .NET 8 + Windows 8.1 risk. Full Clean Architecture, xUnit + Moq testing, System.Text.Json, and proper dependency injection. The SHAppBarMessage complexity is well-documented and solvable.

The existing `.NET 8 + Win 8.1 incompatibility` risk from the `mvp-core` exploration still needs a user decision before proposal work begins. Options: (a) target Windows 10+ and drop Win 8.1, (b) use .NET Framework 4.8 for Win 8.1 compat, or (c) test .NET 8 on Win 8.1 in a VM and accept the risk."

### Next Phase
`sdd-propose` on a re-scoped `mvp-core` change (or a new `mvp-command-line` change), after the user resolves the .NET 8 + Win 8.1 target decision.
