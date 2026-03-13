using System.Diagnostics;

namespace Overwatch.Platform;

/// <summary>Windows platform service: integrates with Windows Service Control Manager via sc.exe.</summary>
public sealed class WindowsPlatformService : IPlatformService
{
    private const string ServiceName = "overwatch";

    public string DefaultConfigDir => @"C:\ProgramData\overwatch\";
    public string DefaultSocketPath => @"\\.\pipe\overwatch";

    public void InstallService(string executablePath, string configDir, string socketPath)
    {
        var binPath = $"\"{executablePath}\" daemon start --config-dir \"{configDir}\" --socket \"{socketPath}\"";
        RunSc($"create {ServiceName} binPath= \"{binPath}\" start= auto DisplayName= \"Overwatch Service Manager\"");
        RunSc($"description {ServiceName} \"Overwatch lightweight service manager\"");
        RunSc($"start {ServiceName}");
    }

    public void UninstallService()
    {
        RunSc($"stop {ServiceName}");
        RunSc($"delete {ServiceName}");
    }

    public ProcessStartInfo ApplyUser(ProcessStartInfo psi, string user)
    {
        // Windows user switching requires credentials; this is a no-op placeholder.
        // Full implementation would use LogonUser/CreateProcessWithLogonW.
        return psi;
    }

    private static void RunSc(string args)
    {
        var psi = new ProcessStartInfo("sc.exe", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start sc.exe.");
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"sc.exe {args} failed (exit {proc.ExitCode}): {err}");
        }
    }
}
