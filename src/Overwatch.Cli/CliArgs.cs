namespace Overwatch.Cli;

/// <summary>Parsed global and command-specific arguments.</summary>
public sealed class CliArgs
{
    public string? ConfigDir { get; set; }
    public string? SocketPath { get; set; }
    public string? LogDir { get; set; }
    public string Command { get; set; } = string.Empty;
    public string? SubCommand { get; set; }
    public string? Namespace { get; set; }

    /// <summary>
    /// Parses argv into CliArgs. Returns null and prints usage on parse error.
    /// Format: overwatch [--config-dir <path>] [--socket <path>] <command> [subcommand] [ns]
    /// </summary>
    public static CliArgs? Parse(string[] args)
    {
        var result = new CliArgs();
        var i = 0;

        while (i < args.Length)
        {
            switch (args[i])
            {
                case "--config-dir" when i + 1 < args.Length:
                    result.ConfigDir = args[++i];
                    break;
                case "--socket" when i + 1 < args.Length:
                    result.SocketPath = args[++i];
                    break;
                case "--log-dir" when i + 1 < args.Length:
                    result.LogDir = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return null;
                default:
                    // First positional arg is the command
                    if (string.IsNullOrEmpty(result.Command))
                        result.Command = args[i];
                    else if (result.SubCommand is null)
                        result.SubCommand = args[i];
                    else if (result.Namespace is null)
                        result.Namespace = args[i];
                    break;
            }
            i++;
        }

        if (string.IsNullOrEmpty(result.Command))
        {
            PrintHelp();
            return null;
        }

        return result;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: overwatch [--config-dir <path>] [--socket <path>] <command> [options]

            Commands:
              daemon start              Start the daemon process
              daemon stop               Stop the daemon process
              daemon status             Show daemon status
              install                   Install as system service
              uninstall                 Uninstall system service
              start <ns>                Start all services in namespace
              stop <ns>                 Stop all services in namespace
              restart <ns>              Restart all services in namespace
              ps                        List all service statuses
              reload                    Reload configuration from disk

            Options:
              --config-dir <path>       Override configuration directory
              --socket <path>           Override IPC socket/pipe path
              --log-dir <path>          Override log output directory
              --help, -h                Show this help message
            """);
    }
}
