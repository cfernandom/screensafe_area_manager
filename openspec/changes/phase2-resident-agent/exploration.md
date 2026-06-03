## Exploration: Phase 2 — Resident Protection Agent

### Current State
The MVP implements an ephemeral CLI tool that applies/reserves/restores the Windows Work Area and exits immediately. When Windows recalculates the Work Area (taskbar move, Explorer restart, resolution change), the reservation is lost with no recovery mechanism. The codebase uses Clean Architecture with 4 projects (Domain, Application, Infrastructure, Console), a strategy pattern for `IWorkAreaManager` (SpSetWorkAreaStrategy + ShAppBarMessageStrategy), and DI via `ServiceCollection` in `Program.cs`.

### Affected Areas
- `src/ScreenSafe.Console/Program.cs` — DI composition root needs restructuring to support dual-mode (CLI + daemon)
- `src/ScreenSafe.Console/CliDispatcher.cs` — needs new `start`, `stop`, `--install`, `--uninstall` commands
- `src/ScreenSafe.Console/ScreenSafe.Console.csproj` — may need additional references (System.Windows.Forms or System.Drawing)
- `src/ScreenSafe.Infrastructure/` — new files: WorkAreaWatcher (hidden window + message pump), WindowsStartupManager, EventDebouncer
- `src/ScreenSafe.Infrastructure/NativeMethods/User32.cs` — extended P/Invoke for CreateWindowEx, GetMessage, DefWindowProc, RegisterWindowMessage
- `src/ScreenSafe.Domain/` — new interfaces: IWorkAreaWatcher, IWindowsStartupManager (optional, could stay in Infrastructure)
- `src/ScreenSafe.Application/` — new use case: MonitorUseCase or AutoApplyService
- `src/ScreenSafe.Tests/` — new test classes for the watcher, debouncer, and auto-apply logic
- `docs/specifications/phase2-resident-protection-agent.md` — specification document

### Approaches

#### 1. Background Execution Model

1. **Hidden Console App with manual Win32 message pump** (RECOMMENDED)
   - Keep as Console App (same project), add `--daemon` flag
   - Create hidden window via `CreateWindowExW` (P/Invoke) or `NativeWindow` (WinForms)
   - Run `GetMessage`/`TranslateMessage`/`DispatchMessage` loop
   - Hide console window via `FreeConsole()` or `ShowWindow(FindWindow("ConsoleWindowClass",...), SW_HIDE)`
   - Pros: Zero new assembly dependencies (pure P/Invoke path), ~28 MB working set, full message reception, single exe deployment
   - Cons: Requires manual window procedure dispatch, must root callback delegates to prevent GC collection
   - Effort: Medium

2. **SystemTray App with Application.Run()**
   - Reference System.Windows.Forms, create hidden Form + NotifyIcon
   - `Application.Run()` provides built-in message pump
   - Pros: Familiar WinForms pattern, built-in message loop, tray icon for status
   - Cons: Requires WinForms + Drawing references (~15-20 MB added to working set, pushing toward 50 MB limit), user sees tray icon (may not want), desktop composition coupling
   - Effort: Low-Medium

3. **Windows Service**
   - Install as a service via SCM, run as LocalSystem
   - Pros: True background, starts before user logon, auto-restart on crash
   - Cons: Session 0 isolation means can't receive user-session messages (WM_SETTINGCHANGE fires in session 1+), requires complex session-aware IPC, overkill for a user-space utility
   - Effort: High

4. **Polling timer in background (no message pump)**
   - Keep CLI app, add `System.Threading.Timer` that polls `SPI_GETWORKAREA` every 1-2 seconds
   - Pros: Simplest implementation, no Win32 message pump needed
   - Cons: Violates RNF-101 (<1% CPU) — polling at 1s interval keeps CPU awake, misses events between polls, less responsive than event-driven
   - Effort: Low

**Recommendation**: Approach 1 (Hidden Console + manual message pump). Best power/resource profile, no new assembly dependencies, directly receives Win32 messages, single-file deploy.

#### 2. Win32 Event Monitoring Strategy

**Events to capture and their Win32 registration:**

| Event | Message ID / Registration | wParam | lParam | What triggers it |
|-------|--------------------------|--------|--------|-----------------|
| Work Area change | `WM_SETTINGCHANGE` (0x001A) | `SPI_SETWORKAREA` (0x002F) | Section name string or 0 | Taskbar resize/move, Explorer recalculation |
| Resolution change | `WM_DISPLAYCHANGE` (0x007E) | Bits-per-pixel | `MAKELPARAM(width, height)` | Display config change, monitor unplug/dock |
| Explorer restart | `RegisterWindowMessage("TaskbarCreated")` | 0 | 0 | Explorer.exe restart, shell restart |

**Critical findings from research:**

a) **WM_SETTINGCHANGE after resolution change**: Spy++ output confirms Windows sends this sequence after display change: `WM_WINDOWPOSCHANGED` → `WM_MOVE` → `WM_SETTINGCHANGE(SPI_ICONVERTICALSPACING)` → `WM_DISPLAYCHANGE` → `WM_SETTINGCHANGE(SPI_SETWORKAREA)`. This means you receive BOTH `WM_DISPLAYCHANGE` AND `WM_SETTINGCHANGE(SPI_SETWORKAREA)` for a single resolution change.

b) **TaskbarCreated is a registered message**: Must call `RegisterWindowMessage("TaskbarCreated")` once at startup. The message ID varies per boot session. The RetroBar project (open-source Explorer replacement) demonstrates the pattern: `NativeWindow` subclass, register in static init, handle in `WndProc`.

c) **WM_SETTINGCHANGE with SPI_SETWORKAREA IS sent**: When Windows recalculates Work Area (due to taskbar action), it broadcasts `WM_SETTINGCHANGE(wParam=SPI_SETWORKAREA)`. This is well-documented and confirmed by the Spy++ log and WSLg issue reports.

d) **Pitfall — false positives**: WM_SETTINGCHANGE fires for MANY system settings changes (wallpaper, fonts, theme, etc.). MUST filter on `wParam == SPI_SETWORKAREA (0x002F)`. For Taskbar-specific changes, check `lParam` for "TraySettings" section.

e) **Pitfall — message ordering during resolution change**: Multiple events fire in sequence. An apply on the first event may be overwritten by subsequent events. Solution: **debounce** all events with a 300-500ms timer.

**Recommended debounce strategy:**
- Single `System.Threading.Timer` with 400ms interval
- On ANY relevant message: restart timer (`timer.Change(400, Timeout.Infinite)`)
- When timer fires: check `SPI_GETWORKAREA`, compare with desired config, reapply if different
- This collapses multiple rapid events into one apply

#### 3. Auto-Start with Windows

| Approach | Mechanism | User Visibility | Reliability | Complexity |
|----------|-----------|----------------|-------------|------------|
| **Registry Run key** (RECOMMENDED) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Task Manager Startup tab | High — standard pattern | Low |
| Task Scheduler | `schtasks.exe` / COM API | Task Scheduler library | High — can auto-restart | Medium |
| Startup Folder | Shortcut in `%APPDATA%\...\Startup` | User sees it in Explorer | Medium — user may delete | Low |

**Recommendation**: Registry Run key with `HKCU`. Simplest, most appropriate for a user-space utility. Add CLI commands:
- `screensafe install` — writes Run key
- `screensafe uninstall` — removes Run key
- Path: `"C:\...\ScreenSafe.Console.exe --daemon"` (hidden mode)

Task Scheduler is overkill. Startup Folder is user-visible and deletable.

#### 4. Architecture Impact

**What needs to change in each layer:**

**Domain layer** (minimal):
- New interface likely needed: `IWorkAreaWatcher` — if we want testable abstraction over the message pump
  - Events: `event EventHandler<WorkAreaChangedEventArgs> WorkAreaChanged;`
  - Events: `event EventHandler<DisplayChangedEventArgs> DisplayChanged;`
  - Events: `event EventHandler ExplorerRestarted;`
  - Method: `void Start()` / `void Stop()`
- Or keep it simpler: just `IAutoApplyService` with `Task RunAsync(CancellationToken)`

**Application layer** (new use case or service):
- `AutoApplyService` — orchestration logic:
  - Receives events from watcher
  - Debounces
  - Compares current work area vs. desired (from settings)
  - Calls `IWorkAreaManager.Apply()` if different
  - Logs events
- `StartupService` — manages auto-start registration

**Infrastructure layer** (new files):
- `WorkAreaWatcher.cs` — hidden window + message pump (the heart of Phase 2)
- `EventDebouncer.cs` — reusable debounce mechanism
- `WindowsStartupManager.cs` — Registry Run key management
- Extended `User32.cs` — add `CreateWindowExW`, `DefWindowProcW`, `RegisterClassExW`, `GetMessageW`, `DispatchMessageW`, `RegisterWindowMessage`, `PostQuitMessage`

**Console layer** (composition root changes):
- `Program.cs` — dual mode: if args contain `daemon` or no args, run as resident; else CLI
- Extract `ConfigureServices()` to shared method used by both modes
- CLI dispatcher gets new commands: `start`, `stop`, `install`, `uninstall`, `status`

**DI registration additions in daemon mode:**
```csharp
services.AddSingleton<IWorkAreaWatcher, WorkAreaWatcher>();
services.AddSingleton<EventDebouncer>();
services.AddSingleton<AutoApplyService>();
services.AddTransient<IWindowsStartupManager, WindowsStartupManager>();
```

**What stays the same:**
- `IWorkAreaManager` + strategy implementations — NO changes needed
- `ISettingsRepository` + `JsonSettingsRepository` — NO changes needed
- `IScreenInfoProvider` + `ScreenInfoProvider` — NO changes needed
- `IPlatformInfoProvider` + `PlatformInfoProvider` — NO changes needed

**Key architectural decision**: Keep the agent in the Console project, not a new project. Rationale:
- Single exe deployment
- Shared DI container
- Same config file
- Can still run CLI commands on the agent
- Avoids project overhead

#### 5. Testing Strategy

**Unit testable layers:**

| Component | What to test | How |
|-----------|-------------|-----|
| `EventDebouncer` | Timer restart, callback invocation, multiple event collapse | Pure C# with `ManualResetEvent` sync |
| `AutoApplyService` | Work area comparison logic, apply decision | Mock `IWorkAreaManager`, `ISettingsRepository`, `IWorkAreaWatcher` |
| `WindowsStartupManager` | Registry key read/write/delete | Mock `RegistryKey` or test with temp key path |
| `WorkAreaWatcher` | Event routing (NOT message pump itself) | Create hidden window, send `PostMessage`, verify event fires |

**What NOT to unit test:**
- Actual Win32 message pump loop (integration test only)
- `CreateWindowExW` success (trust P/Invoke contract)
- Specific message ordering from Windows (test debouncing behavior generically)

**Integration testing pattern for message pump:**
```csharp
// Create a WorkAreaWatcher in test
var watcher = new WorkAreaWatcher(handle);
// Use NativeMethods.PostMessage to send synthetic messages
NativeMethods.PostMessage(watcher.Hwnd, WM_SETTINGCHANGE, SPI_SETWORKAREA, 0);
// Assert watcher.WorkAreaChanged event fired
```

**Integration test conditional skip:**
```csharp
[Fact(Skip = "Requires Windows — run on CI only")]
public void WorkAreaWatcher_ReceivesWmSettingChange() { ... }
```

**Mocking pattern for AutoApplyService:**
```csharp
// Arrange
var watcherMock = new Mock<IWorkAreaWatcher>();
var managerMock = new Mock<IWorkAreaManager>();
var settingsMock = new Mock<ISettingsRepository>();

var service = new AutoApplyService(watcherMock.Object, managerMock.Object, settingsMock.Object, ...);
service.Start();

// Act — simulate event
watcherMock.Raise(w => w.WorkAreaChanged += null, EventArgs.Empty);
await Task.Delay(600); // debounce window

// Assert
managerMock.Verify(m => m.Apply(It.IsAny<int>()), Times.Once);
```

### Recommendation

**Background model**: Hidden Console App (same project, `--daemon` flag) with manual Win32 message pump via pure P/Invoke. This is the best fit for <50 MB and <1% CPU constraints.

**Event monitoring**: Create a `WorkAreaWatcher` class that creates a hidden window via `CreateWindowExW`, registers for `WM_SETTINGCHANGE`, `WM_DISPLAYCHANGE`, and `TaskbarCreated`. Apply a 400ms debounce timer to coalesce rapid events.

**Auto-start**: Registry Run key (`HKCU\...\Run`). Simple, standard, user-manageable via Task Manager.

**Architecture**: Keep everything in the existing 5 projects. Add ~5 new files across the solution. The Console project becomes dual-mode (CLI + daemon). The `IWorkAreaManager` interface remains untouched — the agent reuses existing strategies.

**Testing**: Extract `EventDebouncer` as a pure testable class. Use `IWorkAreaWatcher` interface for mocking. Integration tests send synthetic `PostMessage` calls to the hidden window.

### Risks
- **Message pump thread management**: The message pump blocks the thread. Must run on a dedicated background thread to allow the rest of the application (or CLI response) to function. This adds threading complexity — need proper `SynchronizationContext` handling.
- **Delegate GC collection**: The window procedure callback passed to `RegisterClassExW` via a delegate must be manually rooted (stored in a static field or pinned). If GC collects it, the application crashes on the next message. This is a classic C#/Win32 interop gotcha.
- **Windows 10 SPI_SETWORKAREA override**: The existing research (MVP exploration) confirms Explorer overrides SPI_SETWORKAREA on Windows 10+. The agent's reapply loop mitigates this — it will reapply after Explorer recalculates. But rapid reapply loops could cause flicker or high CPU. The debounce is critical here.
- **32-bit vs 64-bit**: P/Invoke struct layout (RECT, WNDCLASSEX) must be correct for both x86 and x64. The `RECT` struct is blittable and should work, but `WNDCLASSEX` has different packing on x86 (4-byte) vs x64 (8-byte).
- **Console window flash on startup**: Even with `--daemon`, the console window appears briefly before being hidden. Mitigation: use `wmain` or set `Subsystem:Windows` in the .exe and allocate console only when CLI args are present (requires changing OutputType or using `AllocConsole`/`FreeConsole`).

### Ready for Proposal
Yes. All technical approaches are researched, tradeoffs documented. The recommended approach (Hidden Console App + manual Win32 message pump + Registry Run key) is well-understood and low-risk for .NET Framework 4.8. The orchestrator should communicate to the user that:
1. The solution stays as one exe (no new project)
2. No new assembly dependencies needed (pure P/Invoke for the message pump)
3. The agent reuses all existing `IWorkAreaManager` strategies untouched
4. A 400ms debounce is required to handle rapid event sequences from display changes
5. Testing will use `IWorkAreaWatcher` interface mocking + `PostMessage` integration tests
