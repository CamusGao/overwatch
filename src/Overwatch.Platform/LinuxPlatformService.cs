using System.Diagnostics;

namespace Overwatch.Platform;

/// <summary>Linux platform service: integrates with systemd.</summary>
public sealed class LinuxPlatformService : IPlatformService
{
    private const string ServiceName = "overwatch";
    private const string ServiceFile = $"/etc/systemd/system/{ServiceName}.service";

    public string DefaultConfigDir => "/etc/overwatch/";
    public string DefaultSocketPath => "/var/run/overwatch.sock";
    public string DefaultLogDir => "/var/log/overwatch";

    public void InstallService(string executablePath, string configDir, string socketPath)
    {
        var unit = $"""
            [Unit]
            Description=Overwatch Service Manager
            After=network.target

            [Service]
            Type=simple
            ExecStart={executablePath} daemon start --config-dir {configDir} --socket {socketPath}
            Restart=on-failure
            RestartSec=5

            [Install]
            WantedBy=multi-user.target
            """;

        File.WriteAllText(ServiceFile, unit);
        RunSystemctl("daemon-reload");
        RunSystemctl($"enable {ServiceName}");
    }

    public void UninstallService()
    {
        RunSystemctl($"disable {ServiceName}");
        if (File.Exists(ServiceFile))
            File.Delete(ServiceFile);
        RunSystemctl("daemon-reload");
    }

    public ProcessStartInfo ApplyUser(ProcessStartInfo psi, string user)
    {
        psi.UserName = user;
        return psi;
    }

    private static void RunSystemctl(string args)
    {
        var psi = new ProcessStartInfo("systemctl", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start systemctl.");
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"systemctl {args} failed: {err}");
        }
    }
}
