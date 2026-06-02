using Xunit;
using ScreenSafe.Domain;

namespace ScreenSafe.Tests.Domain;

public class ScreenRectTests
{
    [Fact]
    public void Width_WhenCreatedWith1920x1080_Returns1920()
    {
        var rect = new ScreenRect(0, 0, 1920, 1080);
        Assert.Equal(1920, rect.Width);
    }

    [Fact]
    public void Height_WhenCreatedWith1920x1080_Returns1080()
    {
        var rect = new ScreenRect(0, 0, 1920, 1080);
        Assert.Equal(1080, rect.Height);
    }

    [Fact]
    public void Width_WhenNegativeCoords_ReturnsCorrectWidth()
    {
        var rect = new ScreenRect(-1920, 0, 0, 1080);
        Assert.Equal(1920, rect.Width);
    }

    [Fact]
    public void Height_WhenNegativeCoords_ReturnsCorrectHeight()
    {
        var rect = new ScreenRect(0, -1080, 1920, 0);
        Assert.Equal(1080, rect.Height);
    }

    [Fact]
    public void Equals_TwoIdenticalRects_ReturnsTrue()
    {
        var rect1 = new ScreenRect(0, 0, 1920, 1080);
        var rect2 = new ScreenRect(0, 0, 1920, 1080);
        Assert.Equal(rect1, rect2);
    }

    [Fact]
    public void Equals_TwoDifferentRects_ReturnsFalse()
    {
        var rect1 = new ScreenRect(0, 0, 1920, 1080);
        var rect2 = new ScreenRect(0, 0, 1920, 1000);
        Assert.NotEqual(rect1, rect2);
    }

    [Fact]
    public void GetHashCode_TwoIdenticalRects_ReturnsSameHash()
    {
        var rect1 = new ScreenRect(0, 0, 1920, 1080);
        var rect2 = new ScreenRect(0, 0, 1920, 1080);
        Assert.Equal(rect1.GetHashCode(), rect2.GetHashCode());
    }

    [Fact]
    public void ToString_WithValidRect_ReturnsFormattedString()
    {
        var rect = new ScreenRect(0, 0, 1920, 1080);
        var str = rect.ToString();
        Assert.Contains("1920", str);
        Assert.Contains("1080", str);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080, 1920, 1080)]
    [InlineData(100, 200, 800, 600, 700, 400)]
    [InlineData(-100, -200, 100, 200, 200, 400)]
    public void WidthAndHeight_VariousInputs_ReturnsCorrectValues(
        int left, int top, int right, int bottom,
        int expectedWidth, int expectedHeight)
    {
        var rect = new ScreenRect(left, top, right, bottom);
        Assert.Equal(expectedWidth, rect.Width);
        Assert.Equal(expectedHeight, rect.Height);
    }
}
