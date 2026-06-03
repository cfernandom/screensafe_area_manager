using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Application;
using System.IO;

namespace ScreenSafe.Tests.Application;

/// <summary>
/// Tests for <see cref="StatusUseCase"/> — displaying current work area status to the console.
/// </summary>
public class StatusUseCaseTests
{
    [Fact]
    public void Execute_DisplaysCurrentWorkArea()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings
        {
            Enabled = true,
            ReservedBottomPixels = 80,
            Strategy = "SpSetWorkArea",
            OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080)
        });

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1080));

        var screenInfo = new Mock<IScreenInfoProvider>();
        screenInfo.Setup(s => s.GetScreenWidth()).Returns(1920);
        screenInfo.Setup(s => s.GetScreenHeight()).Returns(1080);

        var useCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var output = new StringWriter();
        var originalOut = System.Console.Out;
        System.Console.SetOut(output);

        try
        {
            var result = useCase.Execute();

            Assert.Equal(0, result);
            var consoleText = output.ToString();
            Assert.Contains("Enabled: True", consoleText);
            Assert.Contains("Reserved bottom pixels: 80", consoleText);
            Assert.Contains("Strategy: SpSetWorkArea", consoleText);
            Assert.Contains("1920", consoleText);
            Assert.Contains("1080", consoleText);
            Assert.Contains("(0, 0)", consoleText);
            Assert.Contains("(1920, 1080)", consoleText);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Execute_WhenGetStatusFails_ShowsError()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        settingsRepo.Setup(r => r.Load()).Returns(new AppSettings
        {
            Enabled = true,
            ReservedBottomPixels = 80,
            Strategy = "auto",
            OriginalWorkArea = null
        });

        var workAreaManager = new Mock<IWorkAreaManager>();
        workAreaManager.Setup(m => m.GetStatus()).Returns(((int, int, int, int)?)null);

        var screenInfo = new Mock<IScreenInfoProvider>();
        screenInfo.Setup(s => s.GetScreenWidth()).Returns(1920);
        screenInfo.Setup(s => s.GetScreenHeight()).Returns(1080);

        var useCase = new StatusUseCase(settingsRepo.Object, workAreaManager.Object, screenInfo.Object);

        var output = new StringWriter();
        var originalOut = System.Console.Out;
        System.Console.SetOut(output);

        try
        {
            var result = useCase.Execute();

            Assert.Equal(0, result);
            var consoleText = output.ToString();
            Assert.Contains("(unknown)", consoleText);
            Assert.Contains("Original work area: (not set)", consoleText);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }
}
