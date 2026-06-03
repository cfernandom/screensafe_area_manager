# Design: Phase 2 — Resident Protection Agent

## Technical Approach

Add a `--daemon` flag to the existing `.exe` that launches a hidden Win32 message pump on a dedicated thread. A `WorkAreaWatcher` creates a hidden window via `CreateWindowExW` and listens for `WM_SETTINGCHANGE(SPI_SETWORKAREA)`, `WM_DISPLAYCHANGE`, and `TaskbarCreated`. Events flow through an `EventDebouncer` (400ms default) into `AutoApplyService`, which compares `SPI_GETWORKAREA` against desired config and calls `IWorkAreaManager.Apply()` only on mismatch. Circuit breaker suspends after 10 reapplies in 60s. All new code lives in existing 5 projects — same DI container, same `appsettings.json`, same strategy implementations.

## Architecture Decisions

| Decision | Options | Choice | Rationale |
|---|---|---|---|
| Background model | Hidden console + manual pump, WinForms, Windows Service, Polling | Hidden console + manual pump | Zero new assembly deps, full Win32 message reception, single exe |
| Pump location | Dedicated thread vs Application.Run() | Dedicated thread with `GetMessage` loop | No WinForms dependency; `FreeConsole()` hides window after start |
| Delegate rooting | Static field vs GC.KeepAlive | Static `WndProcDelegate` field | Classic gotcha: GC collects `WndProc` — static root prevents crash |
| Debounce mechanism | System.Threading.Timer vs Channel+Delay | Timer with single-fire pattern (`Timeout.Infinite`) | Existing in .NET 4.8, no extra deps, proven pattern |
| Circuit breaker | Sliding counter + timestamp array | ConcurrentQueue<DateTime> + trim | Memory-efficient for 60s window, no external libs needed |
| Auto-start | Registry Run, Task Scheduler, Startup Folder | Registry Run `HKCU\...\Run` | Simplest, user-visible in Task Manager, idempotent |

## Data Flow

```
Daemon startup:
  Program.cs --daemon
    → ConfigureServices() (shared)
    → AutoApplyService.Start()
      → WorkAreaWatcher.Start()
        → CreateWindowExW (hidden)
        → RegisterClassExW (static WndProc)
        → GetMessage loop (dedicated thread)

  Message received:
    WM_SETTINGCHANGE(wParam=SPI_SETWORKAREA)
    WM_DISPLAYCHANGE
    TaskbarCreated (registered message)
      → WorkAreaWatcher fires C# event
      → EventDebouncer.OnNext()
        → timer.Change(400ms, Infinite)
        → timer fires → callback
          → AutoApplyService.Evaluate()
            → SPI_GETWORKAREA vs AppSettings.DesiredWorkArea
            → if mismatch: IWorkAreaManager.Apply()
            → if >10/60s: circuit breaker suspends 5min
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/IWorkAreaWatcher.cs` | Create | Interface: Start/Stop + events WorkAreaChanged, DisplayChanged, ExplorerRestarted |
| `Domain/IWindowsStartupManager.cs` | Create | Interface: Install, Uninstall, IsInstalled, GetRegisteredCommand |
| `Domain/IEventDebouncer.cs` | Create | Interface: OnNext(action), Start, Stop |
| `Domain/HealthReport.cs` | Create | Record/class for structured health data (resolution, desired/current work area, strategy, daemon status, auto-start, last reapply) |
| `Application/AutoApplyService.cs` | Create | Orchestrator: subscribe to watcher events → debounce → evaluate → apply (with circuit breaker) |
| `Infrastructure/WorkAreaWatcher.cs` | Create | Hidden window via CreateWindowExW, message pump thread, Win32 event routing to C# events |
| `Infrastructure/EventDebouncer.cs` | Create | Timer-based debounce with configurable interval, single-fire restart pattern, CancellationTokenSource lifecycle |
| `Infrastructure/WindowsStartupManager.cs` | Create | Registry Run key CRUD via `Microsoft.Win32.Registry` |
| `Infrastructure/LogRotator.cs` | Create | Size-based rotation (1MB), retention 3 files, path `%LOCALAPPDATA%\ScreenSafe\Logs\` |
| `Infrastructure/ILogger.cs` | Create | Interface for logger abstraction (LogInfo, LogWarning, LogError for daemon use) |
| `Infrastructure/ConsoleLogger.cs` | Create | Console logger for CLI commands; stdout for health, stderr for errors |
| `Infrastructure/FileLogger.cs` | Create | File logger for daemon mode with LogRotator integration |
| `Infrastructure/NativeMethods/User32.cs` | Modify | Add CreateWindowExW, DefWindowProcW, RegisterClassExW, GetMessageW, TranslateMessage, DispatchMessageW, RegisterWindowMessageW, PostQuitMessage, FreeConsole, WNDCLASSEX, MSG structs |
| `Console/Program.cs` | Modify | Dual-mode routing: `--daemon` → daemon mode; else → CLI. Extract `ConfigureServices()` shared method. Add daemon DI registrations |
| `Console/CliDispatcher.cs` | Modify | Add `install`, `uninstall`, `health` commands |
| `Console/HealthUseCase.cs` | Create | Aggregate data from IScreenInfoProvider, ISettingsRepository, IWorkAreaManager, daemon stats, auto-start status |
| `Tests/` | Modify | New test files for EventDebouncer, AutoApplyService, WorkAreaWatcher (integration), WindowsStartupManager, LogRotator, HealthUseCase |

## Interfaces / Contracts

```csharp
// New Domain interfaces
public interface IWorkAreaWatcher
{
    event EventHandler WorkAreaChanged;
    event EventHandler DisplayChanged;
    event EventHandler ExplorerRestarted;
    void Start();
    void Stop();
}

public interface IWindowsStartupManager
{
    void Install(string executablePath);    // writes Run key
    void Uninstall();                        // deletes Run key
    bool IsInstalled();                      // checks key exists + matches
    string? GetRegisteredCommand();          // for health display
}

public interface IEventDebouncer
{
    void OnNext();                           // resets timer
    void Start(Action callback, int intervalMs);
    void Stop();
}

// New P/Invoke in User32.cs (partial list)
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern IntPtr CreateWindowExW(
    uint dwExStyle, string lpClassName, string lpWindowName,
    uint dwStyle, int x, int y, int nWidth, int nHeight,
    IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

[DllImport("user32.dll")]
public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

[DllImport("user32.dll")]
public static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

[DllImport("user32.dll")]
public static extern bool TranslateMessage(ref MSG lpMsg);

[DllImport("user32.dll")]
public static extern IntPtr DispatchMessageW(ref MSG lpMsg);

[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern uint RegisterWindowMessageW(string lpString);

[DllImport("user32.dll")]
public static extern void PostQuitMessage(int nExitCode);

[DllImport("kernel32.dll")]
public static extern bool FreeConsole();
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | EventDebouncer — timer reset, callback invocation, multi-event collapse | `ManualResetEvent` sync, verify single callback per rapid burst |
| Unit | AutoApplyService — debounce → evaluate → apply flow, circuit breaker threshold/suspension/resume | Mock `IWorkAreaManager`, `ISettingsRepository`, `IWorkAreaWatcher`, `IEventDebouncer` |
| Unit | HealthUseCase — aggregation of all data sources | Mock all 4+ dependencies |
| Unit | WindowsStartupManager — registry CRUD | Test via `Microsoft.Win32.RegistryKey` with `Registry.CurrentUser.CreateSubKey` in a test path |
| Unit | LogRotator — rotation at threshold, retention count, file naming | Temp directory, File.WriteAllText to simulate size, verify rollover |
| Integration | WorkAreaWatcher — create hidden window, PostMessage events, verify C# events fire | `[Fact(Skip = "Windows only")]`, use `PostMessage` to send synthetic `WM_SETTINGCHANGE` |
| What NOT to test | Actual `GetMessage` loop convergence, `CreateWindowExW` success, Win32 message ordering | Trust P/Invoke contract, test debounce behavior generically |

## Migration / Rollout

No migration required for existing `appsettings.json` — new `eventDebounceMs` field defaults to 400ms via `AppSettings` default. Existing CLI commands (`apply`, `restore`, `status`) unchanged. Daemon is opt-in via `--daemon` flag.

## Resolved Decisions

### Last Reapply Timestamp: In-Memory Only

**Decision**: Keep in memory only. Not persisted to `appsettings.json`.

**Rationale**: Operational data, not configuration. Does not affect functional behavior. Not needed for state restoration. Avoids frequent disk writes. Avoids mixing config with telemetry.

**Behavior**: On daemon start, `Last Reapply` shows `N/A` until the first reapplication occurs. If historical metrics are needed in a future Phase 3, they can be derived from the log files.

### Daemon Detection: Named Mutex

**Decision**: Use a named mutex (`Global\ScreenSafeDaemon`).

**Rationale**: Simplest approach, classic Windows pattern. No sockets, no temp files, no PID file cleanup issues. Additionally prevents multiple daemon instances — `Create Mutex` on start; if `ERROR_ALREADY_EXISTS`, abort.

**Flow**:
- Daemon start: `CreateMutexW("Global\\ScreenSafeDaemon")` → if already exists, exit with error.
- Health CLI: `OpenMutexW("Global\\ScreenSafeDaemon")` → if handle acquired, daemon is NOT running (no owner); if `ERROR_FILE_NOT_FOUND`, daemon is not running. (Note: `OpenMutex` succeeds even when owned by another process — presence of the named mutex indicates daemon existence.)

### Health Command Output Contract

The `screensafe health` command MUST produce deterministic exit codes and output for scripting and remote support:

| Condition | Exit Code | Status Field |
|-----------|-----------|-------------|
| Daemon running, area correct | 0 | `Status: OK` |
| Daemon running, area mismatch | 0 | `Status: Mismatch Detected` |
| Daemon not running | 1 | `Status: Daemon Not Running` |
| Error reading state | 2 | `Status: Error Reading State` |

Exit code 0 = diagnostic success (information provided, even if mismatch). Exit code 1+ = operational issue (cannot determine state, or critical infrastructure missing).

Output format (stdout):
```text
ScreenSafe Health

Current Resolution: {width}x{height}
Desired WorkArea:   {left},{top},{right},{bottom}
Current WorkArea:   {left},{top},{right},{bottom}
Strategy:           {strategy name}
Daemon:             {Running|Stopped}
AutoStart:          {Enabled|Disabled}
Last Reapply:       {ISO 8601 timestamp or N/A}
Status:             {OK|Mismatch Detected|Daemon Not Running|Error Reading State}
```
