using Xunit;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;

namespace ScreenSafe.Tests.Infrastructure;

/// <summary>
/// Tests for EventDebouncer: timer reset, callback invocation, multi-event collapse,
/// and Start/Stop lifecycle.
/// </summary>
public class EventDebouncerTests : IDisposable
{
    [Fact]
    public void OnNext_FiresCallback_AfterInterval()
    {
        using var debouncer = new EventDebouncer(10);
        var fired = false;
        var reset = new ManualResetEvent(false);

        debouncer.Start();
        debouncer.OnNext(() => { fired = true; reset.Set(); });

        Assert.True(reset.WaitOne(3000), "Callback did not fire within timeout");
        Assert.True(fired);
    }

    [Fact]
    public void OnNext_MultipleCalls_ResetsTimer_OnlyOneCallbackFires()
    {
        using var debouncer = new EventDebouncer(100);
        var callCount = 0;
        var reset = new ManualResetEvent(false);

        debouncer.Start();
        // Fire rapid OnNext calls — only the last callback should fire
        debouncer.OnNext(() => Interlocked.Increment(ref callCount));
        Thread.Sleep(10);
        debouncer.OnNext(() => Interlocked.Increment(ref callCount));
        Thread.Sleep(10);
        debouncer.OnNext(() => { Interlocked.Increment(ref callCount); reset.Set(); });

        Assert.True(reset.WaitOne(3000), "Final callback did not fire within timeout");
        // Give any stale timer callbacks time to fire, then verify single fire
        Thread.Sleep(300);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Stop_PreventsCallback_FromFiring()
    {
        using var debouncer = new EventDebouncer(50);
        var fired = false;

        debouncer.Start();
        debouncer.OnNext(() => { fired = true; });
        debouncer.Stop();

        Thread.Sleep(300);
        Assert.False(fired);
    }

    [Fact]
    public void StartStop_Lifecycle_AllowsRestart()
    {
        using var debouncer = new EventDebouncer(10);
        var fired = false;
        var reset = new ManualResetEvent(false);

        // First cycle
        debouncer.Start();
        debouncer.OnNext(() => { fired = true; reset.Set(); });
        Assert.True(reset.WaitOne(3000), "First callback did not fire");
        Assert.True(fired);

        // Stop
        fired = false;
        reset.Reset();
        debouncer.Stop();
        Thread.Sleep(100);
        Assert.False(fired);

        // Restart
        debouncer.Start();
        debouncer.OnNext(() => { fired = true; reset.Set(); });
        Assert.True(reset.WaitOne(3000), "Restarted callback did not fire");
        Assert.True(fired);
    }

    [Fact]
    public void Constructor_UsesDefaultInterval_Of400Ms()
    {
        using var debouncer = new EventDebouncer();
        var fired = false;
        var reset = new ManualResetEvent(false);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        debouncer.Start();
        debouncer.OnNext(() => { fired = true; reset.Set(); });
        Assert.True(reset.WaitOne(3000), "Callback did not fire within timeout");

        stopwatch.Stop();
        Assert.True(fired);
        // Should be at least ~400ms (allow some tolerance for timer scheduling)
        Assert.True(stopwatch.ElapsedMilliseconds >= 300,
            $"Callback fired too early: {stopwatch.ElapsedMilliseconds}ms (expected ~400ms)");
    }

    public void Dispose()
    {
        // Cleanup is handled by using() in each test
    }
}
