namespace Overwatch.Platform;

/// <summary>Creates the appropriate <see cref="IPlatformService"/> for the current OS.</summary>
public static class PlatformServiceFactory
{
    public static IPlatformService Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsPlatformService();
        return new LinuxPlatformService();
    }
}
