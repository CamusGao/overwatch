using Xunit;

namespace Overwatch.Platform.Tests;

public class PlatformServiceFactoryTests
{
    [Fact]
    public void Create_ReturnsCorrectPlatformService()
    {
        var service = PlatformServiceFactory.Create();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsPlatformService>(service);
        else
            Assert.IsType<LinuxPlatformService>(service);
    }

    [Fact]
    public void DefaultPaths_AreNotEmpty()
    {
        var service = PlatformServiceFactory.Create();
        Assert.False(string.IsNullOrWhiteSpace(service.DefaultConfigDir));
        Assert.False(string.IsNullOrWhiteSpace(service.DefaultSocketPath));
    }

    [Fact]
    public void WindowsService_DefaultPaths_AreExpected()
    {
        var svc = new WindowsPlatformService();
        Assert.Equal(@"C:\ProgramData\overwatch\", svc.DefaultConfigDir);
        Assert.Equal(@"\\.\pipe\overwatch", svc.DefaultSocketPath);
    }

    [Fact]
    public void LinuxService_DefaultPaths_AreExpected()
    {
        var svc = new LinuxPlatformService();
        Assert.Equal("/etc/overwatch/", svc.DefaultConfigDir);
        Assert.Equal("/var/run/overwatch.sock", svc.DefaultSocketPath);
    }

    [Fact]
    public void ApplyUser_DoesNotThrow()
    {
        var svc = PlatformServiceFactory.Create();
        var psi = new System.Diagnostics.ProcessStartInfo("echo");
        // Should not throw regardless of platform
        var result = svc.ApplyUser(psi, "testuser");
        Assert.NotNull(result);
    }
}
