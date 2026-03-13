using Overwatch.Daemon;
using Overwatch.Platform;

namespace Overwatch.Cli.Commands;

/// <summary>Handles 'daemon start/stop/status' commands.</summary>
internal static class DaemonCommand
{
    public static async Task<int> StartAsync(string configDir, string socketPath, string logDir)
    {
        Console.WriteLine($"[daemon] Starting. Config: {configDir}, Socket: {socketPath}, Logs: {logDir}");

        var platform = PlatformServiceFactory.Create();
        var host = new DaemonHost(configDir, socketPath, logDir, platform);

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Handle SIGTERM on Linux
        if (!OperatingSystem.IsWindows())
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
        }

        await host.StartAsync(cts.Token);
        Console.WriteLine("[daemon] Running. Press Ctrl+C to stop.");

        try { await Task.Delay(Timeout.Infinite, cts.Token); }
        catch (OperationCanceledException) { }

        Console.WriteLine("[daemon] Stopping...");
        await host.StopAsync();
        Console.WriteLine("[daemon] Stopped.");
        return 0;
    }
}
