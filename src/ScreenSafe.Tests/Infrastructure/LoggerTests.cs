using Xunit;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure.Logging;

namespace ScreenSafe.Tests.Infrastructure;

/// <summary>
/// Tests for the logger infrastructure: ILogger interface, ConsoleLogger,
/// and FileLogger implementations.
/// </summary>
public class LoggerTests
{
    // ── LogLevel Enum ────────────────────────────────────────────────────

    [Fact]
    public void LogLevel_Info_IsZero()
    {
        Assert.Equal(0, (int)LogLevel.Info);
    }

    [Fact]
    public void LogLevel_Warning_IsOne()
    {
        Assert.Equal(1, (int)LogLevel.Warning);
    }

    [Fact]
    public void LogLevel_Error_IsTwo()
    {
        Assert.Equal(2, (int)LogLevel.Error);
    }

    // ── ILogger Interface ────────────────────────────────────────────────

    [Fact]
    public void ILogger_InfoMethod_CanBeCalled()
    {
        ILogger logger = new ConsoleLogger();
        logger.Info("test info");
        // No exception means test passes — interface is implemented
    }

    [Fact]
    public void ILogger_WarningMethod_CanBeCalled()
    {
        ILogger logger = new ConsoleLogger();
        logger.Warning("test warning");
    }

    [Fact]
    public void ILogger_ErrorMethod_CanBeCalled()
    {
        ILogger logger = new ConsoleLogger();
        logger.Error("test error");
    }

    [Fact]
    public void ILogger_LogMethod_CanBeCalled()
    {
        ILogger logger = new ConsoleLogger();
        logger.Log(LogLevel.Info, "test log");
        logger.Log(LogLevel.Warning, "test warning");
        logger.Log(LogLevel.Error, "test error");
    }

    // ── ConsoleLogger ────────────────────────────────────────────────────

    [Fact]
    public void ConsoleLogger_Info_WritesFormattedOutput()
    {
        var writer = new System.IO.StringWriter();
        var originalOut = System.Console.Out;
        try
        {
            System.Console.SetOut(writer);
            var logger = new ConsoleLogger();
            logger.Info("hello");
            var output = writer.ToString();
            Assert.Contains("[INFO]", output);
            Assert.Contains("hello", output);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void ConsoleLogger_Warning_WritesFormattedOutput()
    {
        var writer = new System.IO.StringWriter();
        var originalOut = System.Console.Out;
        try
        {
            System.Console.SetOut(writer);
            var logger = new ConsoleLogger();
            logger.Warning("caution");
            var output = writer.ToString();
            Assert.Contains("[WARNING]", output);
            Assert.Contains("caution", output);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void ConsoleLogger_Error_WritesFormattedOutput()
    {
        var writer = new System.IO.StringWriter();
        var originalOut = System.Console.Out;
        try
        {
            System.Console.SetOut(writer);
            var logger = new ConsoleLogger();
            logger.Error("fail");
            var output = writer.ToString();
            Assert.Contains("[ERROR]", output);
            Assert.Contains("fail", output);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void ConsoleLogger_Log_WithInfo_WritesInfoFormat()
    {
        var writer = new System.IO.StringWriter();
        var originalOut = System.Console.Out;
        try
        {
            System.Console.SetOut(writer);
            var logger = new ConsoleLogger();
            logger.Log(LogLevel.Info, "direct");
            var output = writer.ToString();
            Assert.Contains("[INFO]", output);
            Assert.Contains("direct", output);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    // ── FileLogger ───────────────────────────────────────────────────────

    [Fact]
    public void FileLogger_Info_CreatesFileWithContent()
    {
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScreenSafeTests",
            Guid.NewGuid().ToString());
        var logPath = System.IO.Path.Combine(tempDir, "screensafe.log");

        try
        {
            var logger = new FileLogger(logPath);
            logger.Info("file test");

            Assert.True(System.IO.File.Exists(logPath));
            var content = System.IO.File.ReadAllText(logPath);
            Assert.Contains("[INFO]", content);
            Assert.Contains("file test", content);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_Warning_CreatesFileWithContent()
    {
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScreenSafeTests",
            Guid.NewGuid().ToString());
        var logPath = System.IO.Path.Combine(tempDir, "screensafe.log");

        try
        {
            var logger = new FileLogger(logPath);
            logger.Warning("file warning");

            Assert.True(System.IO.File.Exists(logPath));
            var content = System.IO.File.ReadAllText(logPath);
            Assert.Contains("[WARNING]", content);
            Assert.Contains("file warning", content);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_Error_CreatesFileWithContent()
    {
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScreenSafeTests",
            Guid.NewGuid().ToString());
        var logPath = System.IO.Path.Combine(tempDir, "screensafe.log");

        try
        {
            var logger = new FileLogger(logPath);
            logger.Error("file error");

            Assert.True(System.IO.File.Exists(logPath));
            var content = System.IO.File.ReadAllText(logPath);
            Assert.Contains("[ERROR]", content);
            Assert.Contains("file error", content);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_AutoCreatesDirectory()
    {
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScreenSafeTests",
            Guid.NewGuid().ToString(),
            "SubDir");
        var logPath = System.IO.Path.Combine(tempDir, "screensafe.log");

        try
        {
            Assert.False(System.IO.Directory.Exists(tempDir));
            var logger = new FileLogger(logPath);
            logger.Info("auto create dir");
            Assert.True(System.IO.Directory.Exists(tempDir));
            Assert.True(System.IO.File.Exists(logPath));
        }
        finally
        {
            var baseDir = System.IO.Path.GetDirectoryName(tempDir);
            if (baseDir != null && System.IO.Directory.Exists(baseDir))
                System.IO.Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_MultipleEntries_AppendsToFile()
    {
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScreenSafeTests",
            Guid.NewGuid().ToString());
        var logPath = System.IO.Path.Combine(tempDir, "screensafe.log");

        try
        {
            var logger = new FileLogger(logPath);
            logger.Info("first");
            logger.Warning("second");
            logger.Error("third");

            var content = System.IO.File.ReadAllText(logPath);
            var lines = content.Split(
                new[] { System.Environment.NewLine },
                System.StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_LogFormat_IncludesTimestamp()
    {
        var tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ScreenSafeTests",
            Guid.NewGuid().ToString());
        var logPath = System.IO.Path.Combine(tempDir, "screensafe.log");

        try
        {
            var logger = new FileLogger(logPath);
            logger.Info("timestamp check");

            var content = System.IO.File.ReadAllText(logPath);
            // Format: yyyy-MM-dd HH:mm:ss [LEVEL] message
            Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[INFO\] timestamp check", content);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, recursive: true);
        }
    }
}
