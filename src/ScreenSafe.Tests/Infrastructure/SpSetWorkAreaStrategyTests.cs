using Xunit;
using Moq;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Tests.Infrastructure;

public class SpSetWorkAreaStrategyTests
{
    [Fact]
    public void Constructor_ReceivesIScreenInfoProvider()
    {
        var screenInfo = Mock.Of<IScreenInfoProvider>();
        var strategy = new SpSetWorkAreaStrategy(screenInfo);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Constructor_WhenScreenInfoProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SpSetWorkAreaStrategy(null!));
        Assert.Equal("screenInfoProvider", exception.ParamName);
    }

    [Fact]
    public void Restore_WhenNoStoredRect_ReturnsFalse()
    {
        var screenInfo = Mock.Of<IScreenInfoProvider>();
        var strategy = new SpSetWorkAreaStrategy(screenInfo);
        var result = strategy.Restore();
        Assert.False(result);
    }

    [Fact]
    public void CalculateNewWorkArea_SubtractsReservedPixelsFromBottom()
    {
        var original = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var result = SpSetWorkAreaStrategy.CalculateNewWorkArea(original, 1080, 80);
        Assert.Equal(0, result.Left);
        Assert.Equal(0, result.Top);
        Assert.Equal(1920, result.Right);
        Assert.Equal(1000, result.Bottom);
    }

    [Fact]
    public void CalculateNewWorkArea_ZeroReserved_ReturnsFullHeight()
    {
        var original = new RECT { Left = 100, Top = 50, Right = 1900, Bottom = 1080 };
        var result = SpSetWorkAreaStrategy.CalculateNewWorkArea(original, 1080, 0);
        Assert.Equal(100, result.Left);
        Assert.Equal(50, result.Top);
        Assert.Equal(1900, result.Right);
        Assert.Equal(1080, result.Bottom);
    }
}
