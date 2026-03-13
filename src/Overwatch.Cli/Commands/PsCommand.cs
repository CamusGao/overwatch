using System.Text.Json;
using Overwatch.Ipc;
using Overwatch.Ipc.Messages;
using Overwatch.Ipc.Transport;

namespace Overwatch.Cli.Commands;

/// <summary>Sends the 'ps' command and formats the table output.</summary>
internal static class PsCommand
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

        var response = await client.SendAsync(new IpcRequest { Cmd = IpcCommands.Ps });
        if (!response.Ok)
        {
            Console.Error.WriteLine($"Error: {response.Error}");
            return 1;
        }

        // Deserialize PsData from the response data
        PsData? psData = null;
        if (response.Data is JsonElement element)
        {
            psData = JsonSerializer.Deserialize(element.GetRawText(), IpcJsonContext.Default.PsData);
        }

        if (psData is null || psData.Services.Count == 0)
        {
            Console.WriteLine("No services found.");
            return 0;
        }

        PrintTable(psData.Services);
        return 0;
    }

    private static void PrintTable(List<ServiceStatusEntry> services)
    {
        const int nsW = 12, svcW = 12, statusW = 10, pidW = 8, healthW = 10, restW = 9, uptW = 10;

        Console.WriteLine(
            $"{"NAMESPACE",-nsW} {"SERVICE",-svcW} {"STATUS",-statusW} {"PID",-pidW} {"HEALTH",-healthW} {"RESTARTS",-restW} {"UPTIME",-uptW}");
        Console.WriteLine(new string('-', nsW + svcW + statusW + pidW + healthW + restW + uptW + 7));

        foreach (var s in services)
        {
            Console.WriteLine(
                $"{s.Ns,-nsW} {s.Service,-svcW} {s.Status,-statusW} {(s.Pid?.ToString() ?? "-"),-pidW} " +
                $"{(s.Health ?? "-"),-healthW} {s.Restarts,-restW} {(s.Uptime ?? "-"),-uptW}");
        }
    }
}
