using Overwatch.Platform;

namespace Overwatch.Cli.Commands;

/// <summary>Handles install/uninstall commands for system service integration.</summary>
internal static class InstallCommand
{
    public static int Install(string executablePath, string configDir, string socketPath)
    {
        var platform = PlatformServiceFactory.Create();
        try
        {
            platform.InstallService(executablePath, configDir, socketPath);
            Console.WriteLine("Overwatch installed as a system service.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Install failed: {ex.Message}");
            return 1;
        }
    }

    public static int Uninstall()
    {
        var platform = PlatformServiceFactory.Create();
        try
        {
            platform.UninstallService();
            Console.WriteLine("Overwatch system service uninstalled.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Uninstall failed: {ex.Message}");
            return 1;
        }
    }
}
