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
    public void Restore_WhenNoStoredRect_ReturnsFalse()
    {
        var screenInfo = Mock.Of<IScreenInfoProvider>();
        var strategy = new ShAppBarMessageStrategy(screenInfo);
        var result = strategy.Restore();
        Assert.False(result);
    }
}
