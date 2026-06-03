using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Application;

namespace ScreenSafe.Tests.Application;

/// <summary>
/// Tests for <see cref="RestoreUseCase"/> — restoring the original full-screen work area.
/// </summary>
public class RestoreUseCaseTests
{
    [Fact]
    public void Execute_CallsWorkAreaManagerRestore()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings
        {
            OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080)
        });

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Restore(It.IsAny<ScreenRect>())).Returns(true);

        var useCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);

        var result = useCase.Execute();

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.Restore(It.Is<ScreenRect>(r => r.Left == 0 && r.Top == 0 && r.Right == 1920 && r.Bottom == 1080)), Times.Once);
    }

    [Fact]
    public void Execute_WhenNoOriginalWorkArea_ReturnsErrorCode()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings()); // OriginalWorkArea defaults to null

        var workAreaManager = new Mock<IWorkAreaManager>();
        var useCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);

        var result = useCase.Execute();

        Assert.NotEqual(0, result);
        workAreaManager.Verify(m => m.Restore(It.IsAny<ScreenRect>()), Times.Never);
    }

    [Fact]
    public void Execute_WhenRestoreFails_ReturnsErrorCode()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings
        {
            OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080)
        });

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Restore(It.IsAny<ScreenRect>())).Returns(false);

        var useCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);

        var result = useCase.Execute();

        Assert.NotEqual(0, result);
    }

    [Fact]
    public void Execute_ClearsOriginalWorkAreaAfterRestore()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings
        {
            OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080)
        });

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Restore(It.IsAny<ScreenRect>())).Returns(true);

        var useCase = new RestoreUseCase(settingsRepo.Object, workAreaManager.Object);

        var result = useCase.Execute();

        Assert.Equal(0, result);
        settingsRepo.Verify(r => r.Save(It.Is<AppSettings>(s =>
            !s.OriginalWorkArea.HasValue
        )), Times.Once);
    }
}
