using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Application;

namespace ScreenSafe.Tests.Application;

/// <summary>
/// Tests for <see cref="AutoApplyService"/> — orchestrates watcher events,
/// debounce, evaluation, and reapply with circuit breaker.
/// </summary>
public class AutoApplyServiceTests
{
    private sealed class Fixture
    {
        public Mock<IWorkAreaWatcher> Watcher { get; } = new();
        public Mock<IEventDebouncer> Debouncer { get; } = new();
        public Mock<IWorkAreaManager> WorkAreaManager { get; } = new();
        public Mock<ISettingsRepository> SettingsRepo { get; } = new();
        public Mock<IScreenInfoProvider> ScreenInfo { get; } = new();
        public Mock<ILogger> Logger { get; } = new();

        public Fixture()
        {
            // Default setup: area matches (no reapply needed)
            ScreenInfo.Setup(s => s.GetScreenWidth()).Returns(1920);
            ScreenInfo.Setup(s => s.GetScreenHeight()).Returns(1080);
            WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1000)); // 80px reserved
            SettingsRepo.Setup(r => r.Load()).Returns(new AppSettings
            {
                Enabled = true,
                ReservedBottomPixels = 80,
                OriginalWorkArea = new ScreenRect(0, 0, 1920, 1080)
            });
        }

        public AutoApplyService CreateService(
            int circuitBreakerMaxReapplies = 10,
            int circuitBreakerWindowSeconds = 60,
            int circuitBreakerSuspendSeconds = 300)
        {
            return new AutoApplyService(
                Watcher.Object,
                Debouncer.Object,
                WorkAreaManager.Object,
                SettingsRepo.Object,
                ScreenInfo.Object,
                Logger.Object,
                circuitBreakerMaxReapplies,
                circuitBreakerWindowSeconds,
                circuitBreakerSuspendSeconds);
        }

        public void RaiseWorkAreaChanged()
        {
            Watcher.Raise(w => w.WorkAreaChanged += null, EventArgs.Empty);
        }

        public void RaiseDisplayChanged()
        {
            Watcher.Raise(w => w.DisplayChanged += null, EventArgs.Empty);
        }

        public void RaiseExplorerRestarted()
        {
            Watcher.Raise(w => w.ExplorerRestarted += null, EventArgs.Empty);
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Test: areas match even when taskbar is at top (top != 0)
    // Regression test for the daemon loop bug: Evaluate() was checking
    // top == 0, which fails when taskbar is at top/side of screen.
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void OnWorkAreaChanged_WhenTaskbarAtTop_AndBottomCorrect_MatchesCorrectly()
    {
        // Arrange: taskbar at top (40px), work area correctly reserved (bottom = 1000)
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 40, 1920, 1000)); // top=40, bottom=1000 (correct)
        // desiredBottom = 1080 - 80 = 1000 → matches

        var service = fx.CreateService();
        Action? capturedCallback = null;
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => capturedCallback = cb);

        // Act
        service.Start();
        fx.RaiseWorkAreaChanged();
        capturedCallback!();

        // Assert: Apply should NOT be called — bottom matches, top is irrelevant
        fx.WorkAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        fx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("areas match, no reapply needed"))), Times.AtLeastOnce);

        service.Stop();
    }

    // ────────────────────────────────────────────────────────────────
    // Test: taskbar at left side (left != 0) also matches correctly
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void OnWorkAreaChanged_WhenTaskbarAtLeft_AndBottomCorrect_MatchesCorrectly()
    {
        // Arrange: taskbar at left (60px), work area correctly reserved
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((60, 0, 1920, 1000)); // left=60, bottom=1000 (correct)

        var service = fx.CreateService();
        Action? capturedCallback = null;
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => capturedCallback = cb);

        // Act
        service.Start();
        fx.RaiseWorkAreaChanged();
        capturedCallback!();

        // Assert: Apply should NOT be called
        fx.WorkAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);

        service.Stop();
    }

    // ────────────────────────────────────────────────────────────────
    // Test: self-triggered event after Apply is suppressed
    // Regression test: without suppression, a WorkAreaChanged triggered
    // by our own Apply call would loop through Evaluate → Apply →
    // WorkAreaChanged → ... indefinitely.
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void OnWorkAreaChanged_AfterApply_SuppressesNextImmediateEvent()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 900)); // mismatched
        fx.WorkAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);

        var service = fx.CreateService();

        var onNextCalls = new List<Action>();
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => onNextCalls.Add(cb));

        service.Start();

        // First event: fires normally, captured by debouncer
        fx.RaiseWorkAreaChanged();
        Assert.Single(onNextCalls);

        // Execute Evaluate → Apply() → sets _suppressNextWorkAreaChanged = true
        onNextCalls[0]();
        fx.WorkAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Once);

        // Second event: should be suppressed — NOT reach debouncer
        fx.RaiseWorkAreaChanged();

        // Assert: still only 1 OnNext call (second event was suppressed)
        Assert.Single(onNextCalls);

        service.Stop();
    }

    [Fact]
    public void OnWorkAreaChanged_WhenAreaMismatch_AppliesWorkArea()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 900)); // mismatched! should be 1000
        fx.WorkAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);

        var service = fx.CreateService();
        Action? capturedCallback = null;
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => capturedCallback = cb);

        // Act
        service.Start();

        // Simulate watcher firing → debouncer.OnNext called
        fx.RaiseWorkAreaChanged();

        // The debouncer's callback should have been captured as Evaluate
        Assert.NotNull(capturedCallback);

        // Act: invoke the captured Evaluate callback
        capturedCallback!();

        // Assert
        fx.WorkAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.AtLeastOnce);
        fx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Reapplying"))), Times.AtLeastOnce);

        service.Stop();
    }

    // ────────────────────────────────────────────────────────────────
    // Test: event received → debounce → evaluate → NO apply when areas match
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void OnWorkAreaChanged_WhenAreasMatch_DoesNotApply()
    {
        // Arrange
        var fx = new Fixture();
        // Areas already match (GetStatus returns desired area based on settings)
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 1000)); // matches desired (1080-80)

        var service = fx.CreateService();
        Action? capturedCallback = null;
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => capturedCallback = cb);

        // Act
        service.Start();
        fx.RaiseWorkAreaChanged();
        capturedCallback!();

        // Assert — Apply should NOT be called
        fx.WorkAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Never);
        fx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("areas match, no reapply needed"))), Times.AtLeastOnce);

        service.Stop();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: circuit breaker: >10 reapplies in 60s → suspend 5 minutes
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CircuitBreaker_AfterMaxReapplies_SuspendsReapply()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 900)); // always mismatched
        fx.WorkAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);

        var service = fx.CreateService(
            circuitBreakerMaxReapplies: 5,
            circuitBreakerWindowSeconds: 60,
            circuitBreakerSuspendSeconds: 1); // short suspend for test speed

        Action? capturedCallback = null;
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => capturedCallback = cb);

        // Act
        service.Start();

        // Fire an event to populate capturedCallback via debouncer mock
        fx.RaiseWorkAreaChanged();
        Assert.NotNull(capturedCallback);

        // Trigger 5 reapplies (should be OK)
        for (int i = 0; i < 5; i++)
        {
            capturedCallback!();
        }

        // The 6th call should be suspended
        capturedCallback!();

        // Assert: After suspension, Apply should still be called 5 times (not 6)
        // The circuit breaker should log about suspension
        fx.Logger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Circuit breaker"))), Times.AtLeastOnce);
        fx.WorkAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.Exactly(5));

        service.Stop();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: circuit breaker: after suspension, resumes normal operation
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CircuitBreaker_AfterSuspendExpires_ResumesNormalOperation()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 900)); // always mismatched
        fx.WorkAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);

        var service = fx.CreateService(
            circuitBreakerMaxReapplies: 3,
            circuitBreakerWindowSeconds: 60,
            circuitBreakerSuspendSeconds: 1); // short suspend

        Action? capturedCallback = null;
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => capturedCallback = cb);

        service.Start();

        // Fire an event to populate capturedCallback via debouncer mock
        fx.RaiseWorkAreaChanged();
        Assert.NotNull(capturedCallback);

        // Call 3 times to fill the reapply window (1st call applies, 2nd applies, 3rd applies)
        // The 4th call triggers the circuit breaker (count >= max)
        for (int i = 0; i < 4; i++)
        {
            capturedCallback!();
        }

        // Verify circuit breaker was triggered
        fx.Logger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Circuit breaker"))), Times.AtLeastOnce);

        // Wait for suspension to expire
        Thread.Sleep(1100);

        // Clear the verify counts for the next reapply
        fx.WorkAreaManager.Invocations.Clear();

        // After resume, another reapply should work
        capturedCallback!();

        // Assert: Apply should have been called again (resumed)
        fx.WorkAreaManager.Verify(m => m.Apply(It.IsAny<int>()), Times.AtLeastOnce);
        fx.Logger.Verify(l => l.Info(It.Is<string>(s => s.Contains("resumed"))), Times.AtLeastOnce);

        service.Stop();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: OriginalWorkArea is NOT modified during auto-reapply
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_DoesNotModifyOriginalWorkArea()
    {
        // Arrange
        var fx = new Fixture();
        fx.WorkAreaManager.Setup(m => m.GetStatus()).Returns((0, 0, 1920, 900)); // mismatched
        fx.WorkAreaManager.Setup(m => m.Apply(It.IsAny<int>())).Returns(true);

        var savedSettings = new List<AppSettings>();
        fx.SettingsRepo.Setup(r => r.Save(It.IsAny<AppSettings>()))
            .Callback<AppSettings>(s => savedSettings.Add(s));

        var service = fx.CreateService();
        Action? capturedCallback = null;
        fx.Debouncer.Setup(d => d.OnNext(It.IsAny<Action>()))
            .Callback<Action>(cb => capturedCallback = cb);

        var initialOwa = new ScreenRect(0, 0, 1920, 1080);

        // Act
        service.Start();
        fx.RaiseWorkAreaChanged();
        capturedCallback!();

        // Assert: Save should NOT have been called (we don't persist during auto-reapply)
        fx.SettingsRepo.Verify(r => r.Save(It.IsAny<AppSettings>()), Times.Never);

        // Assert: OriginalWorkArea in settings should still be the initial value
        var loadedSettings = fx.SettingsRepo.Object.Load();
        Assert.Equal(initialOwa.Left, loadedSettings.OriginalWorkArea?.Left);
        Assert.Equal(initialOwa.Top, loadedSettings.OriginalWorkArea?.Top);
        Assert.Equal(initialOwa.Right, loadedSettings.OriginalWorkArea?.Right);
        Assert.Equal(initialOwa.Bottom, loadedSettings.OriginalWorkArea?.Bottom);

        service.Stop();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Test: Start/Stop lifecycle
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StartStop_Lifecycle_SubscribesAndUnsubscribes()
    {
        // Arrange
        var fx = new Fixture();
        var service = fx.CreateService();

        // Act: Start
        service.Start();

        // Assert: Watcher.Start should have been called
        fx.Watcher.Verify(w => w.Start(), Times.Once);
        fx.Debouncer.Verify(d => d.Start(), Times.Once);

        // Act: Stop
        service.Stop();

        // Assert: Watcher.Stop and Debouncer.Stop should have been called
        fx.Watcher.Verify(w => w.Stop(), Times.Once);
        fx.Debouncer.Verify(d => d.Stop(), Times.Once);
    }
}
