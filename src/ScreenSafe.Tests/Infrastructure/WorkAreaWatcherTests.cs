using Xunit;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Tests.Infrastructure;

/// <summary>
/// Integration tests for WorkAreaWatcher hidden window and Win32 message pump.
/// Uses PostMessageW to send synthetic messages and ManualResetEvent to wait for async events.
/// All tests skipped by default — run on Windows VM only.
/// </summary>
public class WorkAreaWatcherTests : IDisposable
{
    private readonly WorkAreaWatcher _watcher = new();

    public void Dispose()
    {
        try
        {
            _watcher.Stop();
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    [Fact(Skip = "Requires Windows — run on VM only")]
    public void Start_CreatesHiddenWindow_WithNonZeroHwnd()
    {
        _watcher.Start();
        try
        {
            Assert.NotEqual(IntPtr.Zero, _watcher.Hwnd);
        }
        finally
        {
            _watcher.Stop();
        }
    }

    [Fact(Skip = "Requires Windows — run on VM only")]
    public void PostMessage_WM_SETTINGCHANGE_FiresWorkAreaChanged()
    {
        var fired = new ManualResetEvent(false);
        _watcher.WorkAreaChanged += (s, e) => fired.Set();
        _watcher.Start();

        try
        {
            User32.PostMessageW(
                _watcher.Hwnd,
                User32.WM_SETTINGCHANGE,
                (IntPtr)0x002F, // SPI_SETWORKAREA
                IntPtr.Zero);

            Assert.True(fired.WaitOne(5000), "WorkAreaChanged event did not fire");
        }
        finally
        {
            _watcher.Stop();
        }
    }

    [Fact(Skip = "Requires Windows — run on VM only")]
    public void PostMessage_WM_DISPLAYCHANGE_FiresDisplayChanged()
    {
        var fired = new ManualResetEvent(false);
        _watcher.DisplayChanged += (s, e) => fired.Set();
        _watcher.Start();

        try
        {
            User32.PostMessageW(
                _watcher.Hwnd,
                User32.WM_DISPLAYCHANGE,
                IntPtr.Zero,
                IntPtr.Zero);

            Assert.True(fired.WaitOne(5000), "DisplayChanged event did not fire");
        }
        finally
        {
            _watcher.Stop();
        }
    }

    [Fact(Skip = "Requires Windows — run on VM only")]
    public void PostMessage_TaskbarCreated_FiresExplorerRestarted()
    {
        var fired = new ManualResetEvent(false);
        _watcher.ExplorerRestarted += (s, e) => fired.Set();
        _watcher.Start();

        try
        {
            // Register the same message the watcher uses internally
            uint taskbarCreatedMsg = User32.RegisterWindowMessageW("TaskbarCreated");
            Assert.NotEqual(0u, taskbarCreatedMsg);

            User32.PostMessageW(
                _watcher.Hwnd,
                taskbarCreatedMsg,
                IntPtr.Zero,
                IntPtr.Zero);

            Assert.True(fired.WaitOne(5000), "ExplorerRestarted event did not fire");
        }
        finally
        {
            _watcher.Stop();
        }
    }

    [Fact(Skip = "Requires Windows — run on VM only")]
    public void StartStop_Lifecycle_WorksWithoutError()
    {
        // Start and stop multiple times should not throw
        var exception = Record.Exception(() =>
        {
            _watcher.Start();
            _watcher.Stop();
            _watcher.Start();
            _watcher.Stop();
        });

        Assert.Null(exception);
    }
}
