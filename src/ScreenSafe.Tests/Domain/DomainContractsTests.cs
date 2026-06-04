using Xunit;
using Moq;
using ScreenSafe.Domain;

namespace ScreenSafe.Tests.Domain;

/// <summary>
/// Verifies that Phase 2 domain interfaces and models exist with expected contracts.
/// These are structural verification tests — they prove the types compile and
/// have the expected members.
/// </summary>
public class DomainContractsTests
{
    // ── IWorkAreaWatcher ─────────────────────────────────────────────────

    [Fact]
    public void IWorkAreaWatcher_MockCanCreateAndVerifyStart()
    {
        var mock = new Mock<IWorkAreaWatcher>();
        mock.Object.Start();
        mock.Verify(m => m.Start(), Times.Once);
    }

    [Fact]
    public void IWorkAreaWatcher_MockCanCreateAndVerifyStop()
    {
        var mock = new Mock<IWorkAreaWatcher>();
        mock.Object.Stop();
        mock.Verify(m => m.Stop(), Times.Once);
    }

    [Fact]
    public void IWorkAreaWatcher_WorkAreaChangedEvent_CanBeRaised()
    {
        var mock = new Mock<IWorkAreaWatcher>();
        var raised = false;
        mock.Object.WorkAreaChanged += (_, _) => raised = true;
        mock.Raise(w => w.WorkAreaChanged += null, EventArgs.Empty);
        Assert.True(raised);
    }

    [Fact]
    public void IWorkAreaWatcher_DisplayChangedEvent_CanBeRaised()
    {
        var mock = new Mock<IWorkAreaWatcher>();
        var raised = false;
        mock.Object.DisplayChanged += (_, _) => raised = true;
        mock.Raise(w => w.DisplayChanged += null, EventArgs.Empty);
        Assert.True(raised);
    }

    [Fact]
    public void IWorkAreaWatcher_ExplorerRestartedEvent_CanBeRaised()
    {
        var mock = new Mock<IWorkAreaWatcher>();
        var raised = false;
        mock.Object.ExplorerRestarted += (_, _) => raised = true;
        mock.Raise(w => w.ExplorerRestarted += null, EventArgs.Empty);
        Assert.True(raised);
    }

    // ── IWindowsStartupManager ───────────────────────────────────────────

    [Fact]
    public void IWindowsStartupManager_MockCanInstall()
    {
        var mock = new Mock<IWindowsStartupManager>();
        mock.Object.Install();
        mock.Verify(m => m.Install(), Times.Once);
    }

    [Fact]
    public void IWindowsStartupManager_MockCanUninstall()
    {
        var mock = new Mock<IWindowsStartupManager>();
        mock.Object.Uninstall();
        mock.Verify(m => m.Uninstall(), Times.Once);
    }

    [Fact]
    public void IWindowsStartupManager_MockCanCheckIsInstalled()
    {
        var mock = new Mock<IWindowsStartupManager>();
        mock.Setup(m => m.IsInstalled()).Returns(true);
        Assert.True(mock.Object.IsInstalled());
    }

    [Fact]
    public void IWindowsStartupManager_MockCanGetRegisteredCommand()
    {
        var mock = new Mock<IWindowsStartupManager>();
        mock.Setup(m => m.GetRegisteredCommand()).Returns("test.exe --daemon");
        Assert.Equal("test.exe --daemon", mock.Object.GetRegisteredCommand());
    }

    // ── IEventDebouncer ──────────────────────────────────────────────────

    [Fact]
    public void IEventDebouncer_MockCanInvokeOnNext()
    {
        var mock = new Mock<IEventDebouncer>();
        mock.Object.OnNext(() => { });
        mock.Verify(m => m.OnNext(It.IsAny<Action>()), Times.Once);
    }

    [Fact]
    public void IEventDebouncer_MockCanStart()
    {
        var mock = new Mock<IEventDebouncer>();
        mock.Object.Start();
        mock.Verify(m => m.Start(), Times.Once);
    }

    [Fact]
    public void IEventDebouncer_MockCanStop()
    {
        var mock = new Mock<IEventDebouncer>();
        mock.Object.Stop();
        mock.Verify(m => m.Stop(), Times.Once);
    }

    // ── HealthReport ─────────────────────────────────────────────────────

    [Fact]
    public void HealthReport_DefaultsAreSet()
    {
        var report = new HealthReport();
        Assert.Equal("N/A", report.LastReapply);
        Assert.Equal(0, report.ExitCode);
    }

    [Fact]
    public void HealthReport_CanSetAndReadScreenWidth()
    {
        var report = new HealthReport { ScreenWidth = 1920 };
        Assert.Equal(1920, report.ScreenWidth);
    }

    [Fact]
    public void HealthReport_CanSetAndReadScreenHeight()
    {
        var report = new HealthReport { ScreenHeight = 1080 };
        Assert.Equal(1080, report.ScreenHeight);
    }

    [Fact]
    public void HealthReport_CanSetAndReadDesiredWorkArea()
    {
        var report = new HealthReport { DesiredWorkArea = (0, 0, 1920, 1040) };
        Assert.Equal(0, report.DesiredWorkArea.left);
        Assert.Equal(1040, report.DesiredWorkArea.bottom);
    }

    [Fact]
    public void HealthReport_CanSetAndReadCurrentWorkArea()
    {
        var report = new HealthReport { CurrentWorkArea = (0, 0, 1920, 1080) };
        Assert.True(report.CurrentWorkArea.HasValue);
        Assert.Equal(1920, report.CurrentWorkArea.Value.right);
    }

    [Fact]
    public void HealthReport_CurrentWorkArea_DefaultsToNull()
    {
        var report = new HealthReport();
        Assert.Null(report.CurrentWorkArea);
    }

    [Fact]
    public void HealthReport_CanSetAndReadStrategy()
    {
        var report = new HealthReport { Strategy = "spisetworkarea" };
        Assert.Equal("spisetworkarea", report.Strategy);
    }

    [Fact]
    public void HealthReport_CanSetAndReadDaemonRunning()
    {
        var report = new HealthReport { DaemonRunning = true };
        Assert.True(report.DaemonRunning);
    }

    [Fact]
    public void HealthReport_CanSetAndReadAutoStartEnabled()
    {
        var report = new HealthReport { AutoStartEnabled = false };
        Assert.False(report.AutoStartEnabled);
    }

    [Fact]
    public void HealthReport_CanSetAndReadLastReapply()
    {
        var timestamp = "2026-06-02T10:00:00Z";
        var report = new HealthReport { LastReapply = timestamp };
        Assert.Equal(timestamp, report.LastReapply);
    }

    [Fact]
    public void HealthReport_CanSetAndReadStatus()
    {
        var report = new HealthReport { Status = "OK" };
        Assert.Equal("OK", report.Status);
    }

    [Fact]
    public void HealthReport_CanSetAndReadExitCode()
    {
        var report = new HealthReport { ExitCode = 1 };
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void HealthReport_AllPropertiesRoundTrip()
    {
        var report = new HealthReport
        {
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            DesiredWorkArea = (0, 0, 1920, 1040),
            CurrentWorkArea = (0, 0, 1920, 1080),
            Strategy = "auto",
            DaemonRunning = true,
            AutoStartEnabled = false,
            LastReapply = "2026-06-02T12:00:00Z",
            Status = "Mismatch Detected",
            ExitCode = 0
        };

        Assert.Equal(1920, report.ScreenWidth);
        Assert.Equal(1080, report.ScreenHeight);
        Assert.Equal((0, 0, 1920, 1040), report.DesiredWorkArea);
        Assert.Equal((0, 0, 1920, 1080), report.CurrentWorkArea);
        Assert.Equal("auto", report.Strategy);
        Assert.True(report.DaemonRunning);
        Assert.False(report.AutoStartEnabled);
        Assert.Equal("2026-06-02T12:00:00Z", report.LastReapply);
        Assert.Equal("Mismatch Detected", report.Status);
        Assert.Equal(0, report.ExitCode);
    }
}
