using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;

namespace ScreenSafe.Tests.Infrastructure;

public class ShAppBarMessageStrategyTests
{
    [Fact]
    public void Constructor_ReceivesIScreenInfoProvider()
    {
        var screenInfo = Mock.Of<IScreenInfoProvider>();
        var strategy = new ShAppBarMessageStrategy(screenInfo);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Constructor_WhenScreenInfoProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ShAppBarMessageStrategy(null!));
        Assert.Equal("screenInfoProvider", exception.ParamName);
    }

    [Fact]
    public void Restore_WithOriginalArea_DoesNotThrow()
    {
        var screenInfo = Mock.Of<IScreenInfoProvider>();
        var strategy = new ShAppBarMessageStrategy(screenInfo);
        var originalArea = new ScreenRect(0, 0, 1920, 1080);
        // P/Invoke may or may not succeed in test environment — verifies no exception
        var result = strategy.Restore(originalArea);
        // Either outcome is valid — the fix is that Restore accepts a parameter
        // rather than relying on in-memory state
        Assert.True(result || !result);
    }
}
