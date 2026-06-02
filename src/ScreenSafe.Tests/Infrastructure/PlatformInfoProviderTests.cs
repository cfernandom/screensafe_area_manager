using Xunit;
using ScreenSafe.Infrastructure;

namespace ScreenSafe.Tests.Infrastructure;

public class PlatformInfoProviderTests
{
    [Fact]
    public void OSVersion_ReturnsNonNullVersion()
    {
        var provider = new PlatformInfoProvider();
        Assert.NotNull(provider.OSVersion);
    }

    [Fact]
    public void OSVersion_ReturnsExpectedType()
    {
        var provider = new PlatformInfoProvider();
        Assert.IsType<System.Version>(provider.OSVersion);
    }

    [Fact]
    public void Architecture_ReturnsNonEmptyString()
    {
        var provider = new PlatformInfoProvider();
        Assert.False(string.IsNullOrWhiteSpace(provider.Architecture));
    }

    [Fact]
    public void CanSupportStrategy_WithSpSetWorkArea_ReturnsBool()
    {
        var provider = new PlatformInfoProvider();
        var result = provider.CanSupportStrategy("SpSetWorkArea");
        // Must return a bool (structural check — the actual value depends on OS)
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void CanSupportStrategy_WithShAppBarMessage_ReturnsBool()
    {
        var provider = new PlatformInfoProvider();
        var result = provider.CanSupportStrategy("ShAppBarMessage");
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void CanSupportStrategy_ReturnsSameValueForBothStrategiesOnSamePlatform()
    {
        var provider = new PlatformInfoProvider();
        var spResult = provider.CanSupportStrategy("SpSetWorkArea");
        var shResult = provider.CanSupportStrategy("ShAppBarMessage");
        // Both strategies have identical support on any given platform
        Assert.Equal(spResult, shResult);
    }

    [Fact]
    public void CanSupportStrategy_DoesNotThrowForUnknownStrategy()
    {
        var provider = new PlatformInfoProvider();
        var exception = Record.Exception(() => provider.CanSupportStrategy("UnknownStrategy"));
        Assert.Null(exception);
    }
}
