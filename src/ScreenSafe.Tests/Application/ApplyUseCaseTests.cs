using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Application;

namespace ScreenSafe.Tests.Application;

/// <summary>
/// Tests for <see cref="ApplyUseCase"/> — reserving bottom pixels via IWorkAreaManager.
/// </summary>
public class ApplyUseCaseTests
{
    [Fact]
    public void Execute_CallsWorkAreaManagerApply()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings());

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();
        var useCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var result = useCase.Execute();

        Assert.Equal(0, result);
        workAreaManager.Verify(m => m.Apply(80), Times.Once);
        settingsRepo.Verify(r => r.Save(It.IsAny<AppSettings>()), Times.Once);
    }

    [Fact]
    public void Execute_WhenApplyFails_ReturnsErrorCode()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings());

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(false);
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();
        var useCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var result = useCase.Execute();

        Assert.NotEqual(0, result);
    }

    [Fact]
    public void Execute_StoresOriginalRectAndSaves()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings());

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();
        var useCase = new ApplyUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        useCase.Execute();

        settingsRepo.Verify(r => r.Save(It.Is<AppSettings>(s =>
            s.OriginalWorkArea.HasValue &&
            s.OriginalWorkArea.Value.Left == 0 &&
            s.OriginalWorkArea.Value.Top == 0 &&
            s.OriginalWorkArea.Value.Right == 1920 &&
            s.OriginalWorkArea.Value.Bottom == 1080
        )), Times.Once);
    }
}
