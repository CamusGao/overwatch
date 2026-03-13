using Overwatch.Ipc.Messages;
using Overwatch.Ipc.Transport;

namespace Overwatch.Cli.Commands;

/// <summary>Sends a namespace-scoped command (start/stop/restart) to the daemon.</summary>
internal static class NsCommand
{
    public static async Task<int> RunAsync(string socketPath, string cmd, string nsName)
    {
        await using var client = new IpcClient(socketPath);
        try
        {
            await client.ConnectAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to connect to daemon: {ex.Message}");
            return 1;
        }

        var response = await client.SendAsync(new IpcRequest { Cmd = cmd, Ns = nsName });
        if (response.Ok)
        {
            Console.WriteLine($"OK");
            return 0;
        }

        Console.Error.WriteLine($"Error: {response.Error}");
        return 1;
    }
}
