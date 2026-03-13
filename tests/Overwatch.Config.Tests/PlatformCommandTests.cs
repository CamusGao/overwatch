using Overwatch.Config.Models;
using Overwatch.Config.Parsing;
using Xunit;

namespace Overwatch.Config.Tests;

public class PlatformCommandTests
{
    [Fact]
    public void Parse_StringForm_SetsBothPlatforms()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command: python app.py
            """;
        var config = ConfigParser.Parse(yaml);
        var cmd = config.Services["svc"].Startup.Command;
        Assert.Equal("python app.py", cmd.Linux);
        Assert.Equal("python app.py", cmd.Windows);
    }

    [Fact]
    public void Parse_ObjectForm_SetsIndividualPlatforms()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command:
                    linux: ./start.sh
                    windows: start.bat
            """;
        var config = ConfigParser.Parse(yaml);
        var cmd = config.Services["svc"].Startup.Command;
        Assert.Equal("./start.sh", cmd.Linux);
        Assert.Equal("start.bat", cmd.Windows);
    }

    [Fact]
    public void Resolve_OnWindows_ReturnsWindowsCommand()
    {
        var cmd = new PlatformCommand { Linux = "linux-cmd", Windows = "windows-cmd" };
        var resolved = cmd.Resolve();
        var expected = OperatingSystem.IsWindows() ? "windows-cmd" : "linux-cmd";
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Resolve_NullPlatform_ReturnsNull()
    {
        var cmd = new PlatformCommand();
        Assert.Null(cmd.Resolve());
    }
}
