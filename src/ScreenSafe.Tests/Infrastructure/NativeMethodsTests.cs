using Xunit;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Tests.Infrastructure;

public class NativeMethodsTests
{
    // ── User32 Constants ────────────────────────────────────────────────

    [Fact]
    public void SPI_GETWORKAREA_IsCorrectValue()
    {
        Assert.Equal(0x0030U, User32.SPI_GETWORKAREA);
    }

    [Fact]
    public void SPI_SETWORKAREA_IsCorrectValue()
    {
        Assert.Equal(0x002FU, User32.SPI_SETWORKAREA);
    }

    [Fact]
    public void SPIF_UPDATEINIFILE_IsCorrectValue()
    {
        Assert.Equal(0x0001U, User32.SPIF_UPDATEINIFILE);
    }

    [Fact]
    public void SPIF_SENDCHANGE_IsCorrectValue()
    {
        Assert.Equal(0x0002U, User32.SPIF_SENDCHANGE);
    }

    [Fact]
    public void CombinedUpdateIniFileAndSendChange_IsCorrectValue()
    {
        // Verify they OR correctly to 0x0003
        var combined = User32.SPIF_UPDATEINIFILE | User32.SPIF_SENDCHANGE;
        Assert.Equal(0x0003U, combined);
    }

    // ── RECT Struct ─────────────────────────────────────────────────────

    [Fact]
    public void RECT_CanStoreAndReadFieldValues()
    {
        var rect = new RECT { Left = 10, Top = 20, Right = 1920, Bottom = 1080 };
        Assert.Equal(10, rect.Left);
        Assert.Equal(20, rect.Top);
        Assert.Equal(1920, rect.Right);
        Assert.Equal(1080, rect.Bottom);
    }

    [Fact]
    public void RECT_DefaultsToZero()
    {
        var rect = default(RECT);
        Assert.Equal(0, rect.Left);
        Assert.Equal(0, rect.Top);
        Assert.Equal(0, rect.Right);
        Assert.Equal(0, rect.Bottom);
    }

    // ── Shell32 Constants ───────────────────────────────────────────────

    [Fact]
    public void ABM_NEW_IsCorrectValue()
    {
        Assert.Equal(0x0000U, Shell32.ABM_NEW);
    }

    [Fact]
    public void ABM_REMOVE_IsCorrectValue()
    {
        Assert.Equal(0x0001U, Shell32.ABM_REMOVE);
    }

    [Fact]
    public void ABM_QUERYPOS_IsCorrectValue()
    {
        Assert.Equal(0x0002U, Shell32.ABM_QUERYPOS);
    }

    [Fact]
    public void ABM_SETPOS_IsCorrectValue()
    {
        Assert.Equal(0x0003U, Shell32.ABM_SETPOS);
    }

    [Fact]
    public void ABE_BOTTOM_IsCorrectValue()
    {
        Assert.Equal(3U, Shell32.ABE_BOTTOM);
    }

    // ── APPBARDATA Struct ───────────────────────────────────────────────

    [Fact]
    public void APPBARDATA_CanStoreAndReadCbSize()
    {
        var data = new APPBARDATA { cbSize = 100 };
        Assert.Equal(100U, data.cbSize);
    }

    [Fact]
    public void APPBARDATA_CanStoreAndReadHWnd()
    {
        var expected = new IntPtr(0x12345);
        var data = new APPBARDATA { hWnd = expected };
        Assert.Equal(expected, data.hWnd);
    }

    [Fact]
    public void APPBARDATA_CanStoreAndReadEdge()
    {
        var data = new APPBARDATA { uEdge = Shell32.ABE_BOTTOM };
        Assert.Equal(3U, data.uEdge);
    }

    [Fact]
    public void APPBARDATA_CanStoreAndReadRect()
    {
        var rc = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var data = new APPBARDATA { rc = rc };
        Assert.Equal(0, data.rc.Left);
        Assert.Equal(0, data.rc.Top);
        Assert.Equal(1920, data.rc.Right);
        Assert.Equal(1080, data.rc.Bottom);
    }
}
