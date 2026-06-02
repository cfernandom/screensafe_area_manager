using Xunit;
using ScreenSafe.Infrastructure;

namespace ScreenSafe.Tests.Infrastructure;

public class ScreenInfoProviderTests
{
    /// <summary>
    /// GetScreenWidth calls GetSystemMetrics(SM_CXSCREEN) via P/Invoke.
    /// On Windows, this returns a positive value equal to the screen width.
    /// </summary>
    [Fact]
    public void GetScreenWidth_ReturnsPositiveValue()
    {
        var provider = new ScreenInfoProvider();
        var width = provider.GetScreenWidth();
        Assert.True(width > 0, "Screen width should be a positive number of pixels.");
    }

    /// <summary>
    /// GetScreenHeight calls GetSystemMetrics(SM_CYSCREEN) via P/Invoke.
    /// On Windows, this returns a positive value equal to the screen height.
    /// </summary>
    [Fact]
    public void GetScreenHeight_ReturnsPositiveValue()
    {
        var provider = new ScreenInfoProvider();
        var height = provider.GetScreenHeight();
        Assert.True(height > 0, "Screen height should be a positive number of pixels.");
    }

    /// <summary>
    /// Verifies both methods return values that are consistent with a known
    /// display resolution (e.g., at least 800x600 minimum).
    /// </summary>
    [Fact]
    public void GetScreenWidth_IsAtLeastMinimumResolution()
    {
        var provider = new ScreenInfoProvider();
        var width = provider.GetScreenWidth();
        Assert.True(width >= 800, "Screen width should be at least 800px.");
    }

    [Fact]
    public void GetScreenHeight_IsAtLeastMinimumResolution()
    {
        var provider = new ScreenInfoProvider();
        var height = provider.GetScreenHeight();
        Assert.True(height >= 600, "Screen height should be at least 600px.");
    }
}
