using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Application;
using ScreenSafe.Console;

namespace ScreenSafe.Tests.Console;

/// <summary>
/// Tests for <see cref="CliDispatcher"/> — command argument parsing and dispatch routing.
/// </summary>
public class CliDispatcherTests
{
    /// <summary>
    /// Creates a CliDispatcher with mocked dependencies for testing.
    /// </summary>
    private static CliDispatcher CreateDispatcher(
        ApplyUseCase? applyUseCase = null,
        RestoreUseCase? restoreUseCase = null,
        StatusUseCase? statusUseCase = null,
        HealthUseCase? healthUseCase = null,
        IWindowsStartupManager? startupManager = null)
    {
        applyUseCase ??= new Mock<ApplyUseCase>(
            Mock.Of<ISettingsRepository>(), Mock.Of<IWorkAreaManager>(), Mock.Of<IScreenInfoProvider>()).Object;
        restoreUseCase ??= new Mock<RestoreUseCase>(
            Mock.Of<ISettingsRepository>(), Mock.Of<IWorkAreaManager>()).Object;
        statusUseCase ??= new Mock<StatusUseCase>(
            Mock.Of<ISettingsRepository>(), Mock.Of<IWorkAreaManager>(), Mock.Of<IScreenInfoProvider>()).Object;
        healthUseCase ??= new Mock<HealthUseCase>(
            Mock.Of<IScreenInfoProvider>(), Mock.Of<IWorkAreaManager>(),
            Mock.Of<ISettingsRepository>(), Mock.Of<IWindowsStartupManager>(),
            Mock.Of<ILogger>(), Mock.Of<IDaemonStatusProvider>()).Object;
        startupManager ??= Mock.Of<IWindowsStartupManager>();

        return new CliDispatcher(applyUseCase, restoreUseCase, statusUseCase, healthUseCase, startupManager);
    }

    [Fact]
    public void Execute_WithApplyArg_DispatchesToApplyUseCase()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings { Enabled = true });

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();

        var applyUseCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);
        var restoreUseCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);
        var statusUseCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var dispatcher = CreateDispatcher(applyUseCase: applyUseCase, restoreUseCase: restoreUseCase, statusUseCase: statusUseCase);

        var result = dispatcher.Execute(["apply"]);

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.Apply(80), Times.Once);
        workAreaManager.Verify(m => m.Restore(It.IsAny<ScreenRect>()), Times.Never);
    }

    [Fact]
    public void Execute_WithRestoreArg_DispatchesToRestoreUseCase()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings
        {
            OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080)
        });

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Restore(It.IsAny<ScreenRect>())).Returns(true);
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();

        var applyUseCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);
        var restoreUseCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);
        var statusUseCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var dispatcher = CreateDispatcher(applyUseCase: applyUseCase, restoreUseCase: restoreUseCase, statusUseCase: statusUseCase);

        var result = dispatcher.Execute(["restore"]);

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.Restore(It.IsAny<ScreenRect>()), Times.Once);
        workAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Execute_WithStatusArg_DispatchesToStatusUseCase()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings());

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();
        screenInfo.Setup(s => s.GetScreenWidth()).Returns(1920);
        screenInfo.Setup(s => s.GetScreenHeight()).Returns(1080);

        var applyUseCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);
        var restoreUseCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);
        var statusUseCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var dispatcher = CreateDispatcher(applyUseCase: applyUseCase, restoreUseCase: restoreUseCase, statusUseCase: statusUseCase);

        var result = dispatcher.Execute(["status"]);

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.GetStatus(), Times.AtLeastOnce);
        workAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        workAreaManager.Verify(m => m.Restore(It.IsAny<ScreenRect>()), Times.Never);
    }

    [Fact]
    public void Execute_WithEmptyArgs_ReturnsOneAndCallsNoUseCase()
    {
        var workAreaManager = new Mock<IWorkAreaManager>(MockBehavior.Loose);

        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings { Enabled = true });

        var screenInfo = new Mock<IScreenInfoProvider>();

        var applyUseCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);
        var restoreUseCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);
        var statusUseCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var dispatcher = CreateDispatcher(applyUseCase: applyUseCase, restoreUseCase: restoreUseCase, statusUseCase: statusUseCase);

        var result = dispatcher.Execute([]);

        Assert.Equal(1, result);
        workAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        workAreaManager.Verify(m => m.Restore(It.IsAny<ScreenRect>()), Times.Never);
        workAreaManager.Verify(m => m.GetStatus(), Times.Never);
    }

    [Fact]
    public void Execute_WithUnknownCommand_ReturnsOneAndCallsNoUseCase()
    {
        var workAreaManager = new Mock<IWorkAreaManager>(MockBehavior.Loose);

        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings { Enabled = true });

        var screenInfo = new Mock<IScreenInfoProvider>();

        var applyUseCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);
        var restoreUseCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);
        var statusUseCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var dispatcher = CreateDispatcher(applyUseCase: applyUseCase, restoreUseCase: restoreUseCase, statusUseCase: statusUseCase);

        var result = dispatcher.Execute(["xyz"]);

        Assert.Equal(1, result);
        workAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        workAreaManager.Verify(m => m.Restore(It.IsAny<ScreenRect>()), Times.Never);
        workAreaManager.Verify(m => m.GetStatus(), Times.Never);
    }

    // ── Install command ─────────────────────────────────────────────────

    [Fact]
    public void Execute_WithInstallArg_CallsWindowsStartupManagerInstall()
    {
        var startupManager = new Mock<IWindowsStartupManager>();
        var dispatcher = CreateDispatcher(startupManager: startupManager.Object);

        var result = dispatcher.Execute(["install"]);

        Assert.Equal(0, result);
        startupManager.Verify(m => m.Install(), Times.Once);
    }

    // ── Uninstall command ───────────────────────────────────────────────

    [Fact]
    public void Execute_WithUninstallArg_CallsWindowsStartupManagerUninstall()
    {
        var startupManager = new Mock<IWindowsStartupManager>();
        var dispatcher = CreateDispatcher(startupManager: startupManager.Object);

        var result = dispatcher.Execute(["uninstall"]);

        Assert.Equal(0, result);
        startupManager.Verify(m => m.Uninstall(), Times.Once);
    }

    // ── Health command ─────────────────────────────────────────────────

    [Fact]
    public void Execute_WithHealthArg_ReturnsZeroAndDispatches()
    {
        var screenInfo = new Mock<IScreenInfoProvider>();
        screenInfo.Setup(s => s.GetScreenWidth()).Returns(1920);
        screenInfo.Setup(s => s.GetScreenHeight()).Returns(1080);

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings());

        var startupManager = new Mock<IWindowsStartupManager>();
        startupManager.Setup(m => m.IsInstalled()).Returns(true);

        var daemonStatus = new Mock<IDaemonStatusProvider>();
        daemonStatus.Setup(d => d.IsDaemonRunning()).Returns(true);

        var healthUseCase = new HealthUseCase(
            screenInfo.Object, workAreaManager.Object,
            settingsRepo.Object, startupManager.Object,
            Mock.Of<ILogger>(), daemonStatus.Object);

        var dispatcher = CreateDispatcher(healthUseCase: healthUseCase);

        var result = dispatcher.Execute(["health"]);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Execute_InstallWhenStartupFails_ReturnsOne()
    {
        var startupManager = new Mock<IWindowsStartupManager>();
        startupManager.Setup(m => m.Install()).Throws<UnauthorizedAccessException>();
        var dispatcher = CreateDispatcher(startupManager: startupManager.Object);

        var result = dispatcher.Execute(["install"]);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_HealthReturnsTwo_WhenDaemonNotRunning()
    {
        var screenInfo = new Mock<IScreenInfoProvider>();
        screenInfo.Setup(s => s.GetScreenWidth()).Returns(1920);
        screenInfo.Setup(s => s.GetScreenHeight()).Returns(1080);

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings());

        var daemonStatus = new Mock<IDaemonStatusProvider>();
        daemonStatus.Setup(d => d.IsDaemonRunning()).Returns(false);

        var healthUseCase = new HealthUseCase(
            screenInfo.Object, workAreaManager.Object,
            settingsRepo.Object, Mock.Of<IWindowsStartupManager>(),
            Mock.Of<ILogger>(), daemonStatus.Object);

        var dispatcher = CreateDispatcher(healthUseCase: healthUseCase);

        var result = dispatcher.Execute(["health"]);

        Assert.Equal(1, result);
    }
}
