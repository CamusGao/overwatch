using System.Text.Json;
using Overwatch.Ipc;
using Overwatch.Ipc.Messages;
using Overwatch.Ipc.Transport;

namespace Overwatch.Cli.Commands;

/// <summary>Sends the 'reload' command and displays the result messages.</summary>
internal static class ReloadCommand
{
    public static async Task<int> RunAsync(string socketPath)
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

        var response = await client.SendAsync(new IpcRequest { Cmd = IpcCommands.Reload });
        if (!response.Ok)
        {
            Console.Error.WriteLine($"Error: {response.Error}");
            return 1;
        }

        if (response.Data is JsonElement element)
        {
            var summary = JsonSerializer.Deserialize(element.GetRawText(), DaemonJsonContext.Default.ReloadSummary);
            if (summary is not null)
            {
                foreach (var msg in summary.Messages)
                    Console.WriteLine(msg);
                foreach (var err in summary.Errors)
                    Console.Error.WriteLine($"Warning: {err}");
            }
        }

        return 0;
    }
}
