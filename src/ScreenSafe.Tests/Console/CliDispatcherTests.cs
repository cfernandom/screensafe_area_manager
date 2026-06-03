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

        var dispatcher = new CliDispatcher(applyUseCase, restoreUseCase, statusUseCase);

        var result = dispatcher.Execute(["apply"]);

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.Apply(80), Times.Once);
        workAreaManager.Verify(m => m.Restore(), Times.Never);
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
        workAreaManager.Setup(m => m.Restore()).Returns(true);
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();

        var applyUseCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);
        var restoreUseCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);
        var statusUseCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var dispatcher = new CliDispatcher(applyUseCase, restoreUseCase, statusUseCase);

        var result = dispatcher.Execute(["restore"]);

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.Restore(), Times.Once);
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

        var dispatcher = new CliDispatcher(applyUseCase, restoreUseCase, statusUseCase);

        var result = dispatcher.Execute(["status"]);

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.GetStatus(), Times.AtLeastOnce);
        workAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        workAreaManager.Verify(m => m.Restore(), Times.Never);
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

        var dispatcher = new CliDispatcher(applyUseCase, restoreUseCase, statusUseCase);

        var result = dispatcher.Execute([]);

        Assert.Equal(1, result);
        workAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        workAreaManager.Verify(m => m.Restore(), Times.Never);
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

        var dispatcher = new CliDispatcher(applyUseCase, restoreUseCase, statusUseCase);

        var result = dispatcher.Execute(["xyz"]);

        Assert.Equal(1, result);
        workAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        workAreaManager.Verify(m => m.Restore(), Times.Never);
        workAreaManager.Verify(m => m.GetStatus(), Times.Never);
    }
}
