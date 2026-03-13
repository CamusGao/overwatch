using System.Diagnostics;

namespace Overwatch.Platform;

/// <summary>
/// Platform-specific services for service installation and OS integration.
/// </summary>
public interface IPlatformService
{
    /// <summary>Installs Overwatch as a system service (systemd or Windows Service).</summary>
    void InstallService(string executablePath, string configDir, string socketPath);

    /// <summary>Uninstalls the Overwatch system service.</summary>
    void UninstallService();

    /// <summary>Default configuration directory for this platform.</summary>
    string DefaultConfigDir { get; }

    /// <summary>Default log directory for this platform.</summary>
    string DefaultLogDir { get; }

    /// <summary>Default IPC socket/pipe path for this platform.</summary>
    string DefaultSocketPath { get; }

    /// <summary>
    /// Applies user switching to the given ProcessStartInfo.
    /// On Linux: sets UserName. On Windows: may set credentials.
    /// Returns the (possibly modified) psi.
    /// </summary>
    ProcessStartInfo ApplyUser(ProcessStartInfo psi, string user);
}
