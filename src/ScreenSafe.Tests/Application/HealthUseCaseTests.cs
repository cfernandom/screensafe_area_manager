using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Application;

namespace ScreenSafe.Tests.Application;

/// <summary>
/// Tests for <see cref="HealthUseCase"/> — aggregates screen info, work area
/// status, settings, daemon status, and auto-start into a HealthReport.
/// </summary>
public class HealthUseCaseTests
{
    private sealed class Fixture
    {
        public Mock<IScreenInfoProvider> ScreenInfo { get; } = new();
        public Mock<IWorkAreaManager> WorkAreaManager { get; } = new();
        public Mock<ISettingsRepository> SettingsRepo { get; } = new();
        public Mock<IWindowsStartupManager> StartupManager { get; } = new();
        public Mock<ILogger> Logger { get; } = new();
        public Mock<IDaemonStatusProvider> DaemonStatus { get; } = new();

        public Fixture()
        {
            ScreenInfo.Setup(s => s.GetScreenWidth()).Returns(1920);
            ScreenInfo.Setup(s => s.GetScreenHeight()).Returns(1080);
            WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1000));
            SettingsRepo.Setup(r => r.Load()).Returns(new AppSettings
            {
                Enabled = true,
                ReservedBottomPixels = 80,
                Strategy = "auto",
                OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080)
            });
            StartupManager.Setup(m => m.IsInstalled()).Returns(true);
            DaemonStatus.Setup(d => d.IsDaemonRunning()).Returns(true);
        }

        public HealthUseCase CreateUseCase()
        {
            return new HealthUseCase(
                ScreenInfo.Object,
                WorkAreaManager.Object,
                SettingsRepo.Object,
                StartupManager.Object,
                Logger.Object,
                DaemonStatus.Object);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: all data sources aggregated correctly
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_AggregatesAllDataSources()
    {
        // Arrange
        var fx = new Fixture();
        var useCase = fx.CreateUseCase();

        // Act
        var report = useCase.Execute();

        // Assert
        Assert.Equal(1920, report.ScreenWidth);
        Assert.Equal(1080, report.ScreenHeight);
        Assert.Equal(0, report.DesiredWorkArea.left);
        Assert.Equal(0, report.DesiredWorkArea.top);
        Assert.Equal(1920, report.DesiredWorkArea.right);
        Assert.Equal(1000, report.DesiredWorkArea.bottom); // 1080 - 80
        Assert.Equal(0, report.CurrentWorkArea?.left);
        Assert.Equal(0, report.CurrentWorkArea?.top);
        Assert.Equal(1920, report.CurrentWorkArea?.right);
        Assert.Equal(1000, report.CurrentWorkArea?.bottom);
        Assert.Equal("auto", report.Strategy);
        Assert.True(report.DaemonRunning);
        Assert.True(report.AutoStartEnabled);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: daemon running → DaemonRunning = true
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_WhenDaemonRunning_SetsDaemonRunningTrue()
    {
        // Arrange
        var fx = new Fixture();
        fx.DaemonStatus.Setup(d => d.IsDaemonRunning()).Returns(true);
        var useCase = fx.CreateUseCase();

        // Act
        var report = useCase.Execute();

        // Assert
        Assert.True(report.DaemonRunning);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: daemon not running → DaemonRunning = false
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_WhenDaemonNotRunning_SetsDaemonRunningFalse()
    {
        // Arrange
        var fx = new Fixture();
        fx.DaemonStatus.Setup(d => d.IsDaemonRunning()).Returns(false);
        var useCase = fx.CreateUseCase();

        // Act
        var report = useCase.Execute();

        // Assert
        Assert.False(report.DaemonRunning);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: areas match → Status = "OK", ExitCode = 0
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_WhenAreasMatch_ReturnsStatusOk()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1000)); // matches desired
        var useCase = fx.CreateUseCase();

        // Act
        var report = useCase.Execute();

        // Assert
        Assert.Equal("OK", report.Status);
        Assert.Equal(0, report.ExitCode);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: areas mismatch → Status = "Mismatch Detected", ExitCode = 0
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_WhenAreasMismatch_ReturnsMismatchDetected()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 900)); // doesn't match desired (1000)
        var useCase = fx.CreateUseCase();

        // Act
        var report = useCase.Execute();

        // Assert
        Assert.Equal("Mismatch Detected", report.Status);
        Assert.Equal(0, report.ExitCode); // diagnostic success, even if mismatch
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: error reading state → Status = "Error Reading State", ExitCode = 2
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_WhenErrorReadingState_ReturnsErrorStatus()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns(default((int, int, int, int)?)); // error
        var useCase = fx.CreateUseCase();

        // Act
        var report = useCase.Execute();

        // Assert
        Assert.Equal("Error Reading State", report.Status);
        Assert.Equal(2, report.ExitCode);
    }
}
