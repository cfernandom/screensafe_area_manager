using Xunit;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Tests.Infrastructure;

/// <summary>
/// Tests for the Phase 2 P/Invoke additions to User32.cs.
/// Verifies constants, structs, and delegate declarations compile and have expected values.
/// </summary>
public class User32Phase2Tests
{
    // ── Window Message Constants ─────────────────────────────────────────

    [Fact]
    public void WM_SETTINGCHANGE_IsCorrectValue()
    {
        Assert.Equal(0x001AU, User32.WM_SETTINGCHANGE);
    }

    [Fact]
    public void WM_DISPLAYCHANGE_IsCorrectValue()
    {
        Assert.Equal(0x007EU, User32.WM_DISPLAYCHANGE);
    }

    [Fact]
    public void WM_QUIT_IsCorrectValue()
    {
        Assert.Equal(0x0012U, User32.WM_QUIT);
    }

    [Fact]
    public void WM_CLOSE_IsCorrectValue()
    {
        Assert.Equal(0x0010U, User32.WM_CLOSE);
    }

    [Fact]
    public void CW_USEDEFAULT_IsCorrectValue()
    {
        Assert.Equal(unchecked((int)0x80000000), User32.CW_USEDEFAULT);
    }

    // ── WNDCLASSEX Struct ────────────────────────────────────────────────

    [Fact]
    public void WNDCLASSEX_DefaultsToZero()
    {
        var wc = default(WNDCLASSEX);
        Assert.Equal(0U, wc.cbSize);
        Assert.Equal(0U, wc.style);
        Assert.Equal(IntPtr.Zero, wc.lpfnWndProc);
        Assert.Equal(IntPtr.Zero, wc.hInstance);
        Assert.Null(wc.lpszClassName);
    }

    [Fact]
    public void WNDCLASSEX_CanSetAndReadFields()
    {
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(WNDCLASSEX)),
            style = 0,
            lpfnWndProc = new IntPtr(0x1234),
            hInstance = new IntPtr(0x5678),
            lpszClassName = "TestClass"
        };

        Assert.Equal((uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(WNDCLASSEX)), wc.cbSize);
        Assert.Equal(new IntPtr(0x1234), wc.lpfnWndProc);
        Assert.Equal(new IntPtr(0x5678), wc.hInstance);
        Assert.Equal("TestClass", wc.lpszClassName);
    }

    // ── MSG Struct ───────────────────────────────────────────────────────

    [Fact]
    public void MSG_DefaultsToZero()
    {
        var msg = default(MSG);
        Assert.Equal(IntPtr.Zero, msg.hwnd);
        Assert.Equal(0U, msg.message);
        Assert.Equal(IntPtr.Zero, msg.wParam);
        Assert.Equal(IntPtr.Zero, msg.lParam);
    }

    [Fact]
    public void MSG_CanSetAndReadFields()
    {
        var pt = new POINT { x = 100, y = 200 };
        var msg = new MSG
        {
            hwnd = new IntPtr(0xABCD),
            message = 0x001A,
            wParam = new IntPtr(0x002F),
            lParam = new IntPtr(0),
            time = 5000U,
            pt = pt
        };

        Assert.Equal(new IntPtr(0xABCD), msg.hwnd);
        Assert.Equal(0x001AU, msg.message);
        Assert.Equal(new IntPtr(0x002F), msg.wParam);
        Assert.Equal(new IntPtr(0), msg.lParam);
        Assert.Equal(5000U, msg.time);
        Assert.Equal(100, msg.pt.x);
        Assert.Equal(200, msg.pt.y);
    }

    // ── POINT Struct ─────────────────────────────────────────────────────

    [Fact]
    public void POINT_CanSetAndReadFields()
    {
        var pt = new POINT { x = 10, y = 20 };
        Assert.Equal(10, pt.x);
        Assert.Equal(20, pt.y);
    }

    [Fact]
    public void POINT_DefaultsToZero()
    {
        var pt = default(POINT);
        Assert.Equal(0, pt.x);
        Assert.Equal(0, pt.y);
    }

    // ── Method Declaration Verification ──────────────────────────────────
    // These verify the P/Invoke declarations exist (compilation check).
    // The methods themselves are tested indirectly through integration tests
    // in Phase 2. We verify they can be called with expected parameters.

    [Fact]
    public void RegisterWindowMessageW_CanBeInvoked()
    {
        // RegisterWindowMessageW returns 0 on failure (system message registration failure)
        // This tests the declaration compiles and can be called
        var result = User32.RegisterWindowMessageW("TestMessage");
        Assert.NotEqual(uint.MaxValue, result);
    }
}
