using Overwatch.Config.Models;
using Overwatch.ServiceHost;
using Xunit;

namespace Overwatch.ServiceHost.Tests;

public class LogWriterTests
{
    [Theory]
    [InlineData("100MB", 100L * 1024 * 1024)]
    [InlineData("10MB", 10L * 1024 * 1024)]
    [InlineData("1GB", 1L * 1024 * 1024 * 1024)]
    [InlineData("512KB", 512L * 1024)]
    [InlineData("1024", 1024L)]
    public void ParseSize_Variants(string input, long expected)
    {
        Assert.Equal(expected, LogWriter.ParseSize(input));
    }

    [Theory]
    [InlineData("7d", 7)]
    [InlineData("1d", 1)]
    [InlineData("30d", 30)]
    public void ParseAge_Days(string input, int expectedDays)
    {
        Assert.Equal(TimeSpan.FromDays(expectedDays), LogWriter.ParseAge(input));
    }

    [Fact]
    public void ParseAge_Hours()
    {
        Assert.Equal(TimeSpan.FromHours(12), LogWriter.ParseAge("12h"));
    }

    [Fact]
    public void LogWriter_WriteAndRotate()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "overwatch-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new LogsConfig
            {
                Path = tmpDir,
                MaxSize = "100", // 100 bytes to force rotation
                MaxAge = "7d",
            };
            using var writer = new LogWriter(config, "testsvc");
            writer.Open();

            // Write enough to trigger rotation
            for (int i = 0; i < 20; i++)
            {
                writer.WriteLine($"line {i:D2}: some log content here to fill up size");
            }

            // Current log file should exist
            Assert.True(File.Exists(Path.Combine(tmpDir, "service.log")));
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }
}
