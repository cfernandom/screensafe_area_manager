using Xunit;
using ScreenSafe.Console;

namespace ScreenSafe.Tests.Console;

/// <summary>
/// Tests for <see cref="Program"/> — entry point routing logic.
/// These tests verify the argument parsing decision, not the full daemon
/// lifecycle (which requires Windows desktop).
/// </summary>
public class ProgramTests
{
    [Theory]
    [InlineData(new[] { "--daemon" }, true)]
    [InlineData(new[] { "--daemon", "extra" }, true)]
    [InlineData(new[] { "apply" }, false)]
    [InlineData(new[] { "restore" }, false)]
    [InlineData(new[] { "health" }, false)]
    [InlineData(new[] { "install" }, false)]
    [InlineData(new[] { "uninstall" }, false)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "unknown" }, false)]
    [InlineData(new[] { "--DaEmOn" }, true)]
    public void IsDaemonMode_ReturnsExpected(string[] args, bool expected)
    {
        var result = Program.IsDaemonMode(args);
        Assert.Equal(expected, result);
    }
}
