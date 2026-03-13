using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Overwatch.Ipc.Messages;

namespace Overwatch.Ipc.Transport;

/// <summary>
/// IPC client used by the CLI to send commands to the Daemon.
/// Connects via Unix Domain Socket (Linux) or Named Pipe (Windows).
/// </summary>
public sealed class IpcClient : IAsyncDisposable
{
    private readonly string _socketPath;
    private Stream? _stream;

    public IpcClient(string socketPath)
    {
        _socketPath = socketPath;
    }

    /// <summary>Connects to the daemon. Must be called before sending requests.</summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            var pipe = new NamedPipeClientStream(".", ExtractPipeName(_socketPath), PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000, ct);
            _stream = pipe;
        }
        else
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);
            _stream = new NetworkStream(socket, ownsSocket: true);
        }
    }

    /// <summary>Sends a request and waits for the daemon's response.</summary>
    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken ct = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        var json = JsonSerializer.Serialize(request, IpcJsonContext.Default.IpcRequest);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _stream.WriteAsync(bytes, ct);
        await _stream.FlushAsync(ct);

        return await ReadResponseAsync(ct);
    }

    private async Task<IpcResponse> ReadResponseAsync(CancellationToken ct)
    {
        var buffer = new List<byte>(256);
        var singleByte = new byte[1];

        while (true)
        {
            var read = await _stream!.ReadAsync(singleByte, ct);
            if (read == 0) break;
            if (singleByte[0] == '\n') break;
            buffer.Add(singleByte[0]);
        }

        var json = Encoding.UTF8.GetString(buffer.ToArray());
        return JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcResponse)
            ?? IpcResponse.Failure("Empty response from daemon.");
    }

    private static string ExtractPipeName(string path)
    {
        // Named pipe path: \\.\pipe\overwatch  → "overwatch"
        var lastSlash = path.LastIndexOf('\\');
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }
    }
}
