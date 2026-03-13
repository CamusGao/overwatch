using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Overwatch.Ipc.Messages;

namespace Overwatch.Ipc.Transport;

/// <summary>
/// IPC server used by the Daemon to receive commands from CLI clients.
/// Listens on Unix Domain Socket (Linux) or Named Pipe (Windows).
/// </summary>
public sealed class IpcServer : IAsyncDisposable
{
    private readonly string _socketPath;
    private readonly Func<IpcRequest, Task<IpcResponse>> _handler;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public IpcServer(string socketPath, Func<IpcRequest, Task<IpcResponse>> handler)
    {
        _socketPath = socketPath;
        _handler = handler;
    }

    /// <summary>Starts listening for incoming connections in the background.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenTask = OperatingSystem.IsWindows()
            ? ListenNamedPipeAsync(_cts.Token)
            : ListenUnixSocketAsync(_cts.Token);
    }

    /// <summary>Stops the server.</summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
    }

    private async Task ListenUnixSocketAsync(CancellationToken ct)
    {
        // Clean up stale socket file
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(_socketPath));
        socket.Listen(10);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await socket.AcceptAsync(ct);
                _ = HandleUnixClientAsync(client, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            socket.Dispose();
            if (File.Exists(_socketPath))
                File.Delete(_socketPath);
        }
    }

    private async Task HandleUnixClientAsync(Socket client, CancellationToken ct)
    {
        using var stream = new NetworkStream(client, ownsSocket: true);
        await HandleStreamAsync(stream, ct);
    }

    private async Task ListenNamedPipeAsync(CancellationToken ct)
    {
        var pipeName = ExtractPipeName(_socketPath);

        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(ct);
                _ = HandleStreamAsync(pipe, ct);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (IOException)
            {
                pipe.Dispose();
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private async Task HandleStreamAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            var request = await ReadRequestAsync(stream, ct);
            if (request is null) return;

            var response = await _handler(request);
            var json = JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcResponse);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, ct);
            await stream.FlushAsync(ct);
        }
        catch (Exception)
        {
            // Swallow connection-level errors; they shouldn't crash the server
        }
    }

    private static async Task<IpcRequest?> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new List<byte>(256);
        var singleByte = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(singleByte, ct);
            if (read == 0) break;
            if (singleByte[0] == '\n') break;
            buffer.Add(singleByte[0]);
        }

        if (buffer.Count == 0) return null;
        var json = Encoding.UTF8.GetString(buffer.ToArray());
        return JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcRequest);
    }

    private static string ExtractPipeName(string path)
    {
        var lastSlash = path.LastIndexOf('\\');
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
