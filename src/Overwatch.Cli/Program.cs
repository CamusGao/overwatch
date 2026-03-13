using Overwatch.Cli;
using Overwatch.Cli.Commands;
using Overwatch.Ipc.Messages;
using Overwatch.Platform;

var parsed = CliArgs.Parse(args);
if (parsed is null) return 1;

var platform = PlatformServiceFactory.Create();
var configDir = parsed.ConfigDir ?? platform.DefaultConfigDir;
var socketPath = parsed.SocketPath ?? platform.DefaultSocketPath;
var logDir = parsed.LogDir ?? platform.DefaultLogDir;

return parsed.Command switch
{
    "daemon" => parsed.SubCommand switch
    {
        "start" => await DaemonCommand.StartAsync(configDir, socketPath, logDir),
        "stop" or "status" => UnsupportedSubCommand(parsed.Command, parsed.SubCommand),
        _ => UnknownSubCommand(parsed.Command, parsed.SubCommand),
    },
    "install" => InstallCommand.Install(
        System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "overwatch",
        configDir,
        socketPath),
    "uninstall" => InstallCommand.Uninstall(),
    "start" when parsed.SubCommand is not null =>
        await NsCommand.RunAsync(socketPath, IpcCommands.Start, parsed.SubCommand),
    "start" => MissingArg("start", "namespace"),
    "stop" when parsed.SubCommand is not null =>
        await NsCommand.RunAsync(socketPath, IpcCommands.Stop, parsed.SubCommand),
    "stop" => MissingArg("stop", "namespace"),
    "restart" when parsed.SubCommand is not null =>
        await NsCommand.RunAsync(socketPath, IpcCommands.Restart, parsed.SubCommand),
    "restart" => MissingArg("restart", "namespace"),
    "ps" => await PsCommand.RunAsync(socketPath),
    "reload" => await ReloadCommand.RunAsync(socketPath),
    _ => UnknownCommand(parsed.Command),
};

static int UnknownCommand(string cmd)
{
    Console.Error.WriteLine($"Unknown command: '{cmd}'. Run 'overwatch --help' for usage.");
    return 1;
}

static int UnknownSubCommand(string cmd, string? sub)
{
    Console.Error.WriteLine($"Unknown subcommand '{sub}' for '{cmd}'. Run 'overwatch --help' for usage.");
    return 1;
}

static int UnsupportedSubCommand(string cmd, string? sub)
{
    Console.Error.WriteLine($"Subcommand '{cmd} {sub}' is not yet implemented.");
    return 1;
}

static int MissingArg(string cmd, string argName)
{
    Console.Error.WriteLine($"'{cmd}' requires a {argName}. Usage: overwatch {cmd} <{argName}>");
    return 1;
}

