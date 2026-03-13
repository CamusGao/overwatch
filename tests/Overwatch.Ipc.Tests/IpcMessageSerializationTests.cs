using System.Text.Json;
using Overwatch.Ipc;
using Overwatch.Ipc.Messages;
using Xunit;

namespace Overwatch.Ipc.Tests;

public class IpcMessageSerializationTests
{
    [Fact]
    public void IpcRequest_Serializes_WithCmd()
    {
        var req = new IpcRequest { Cmd = IpcCommands.Start, Ns = "myapp" };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.IpcRequest);
        Assert.Contains("\"cmd\":\"start\"", json);
        Assert.Contains("\"ns\":\"myapp\"", json);
    }

    [Fact]
    public void IpcRequest_Ps_DoesNotSerializeNs()
    {
        var req = new IpcRequest { Cmd = IpcCommands.Ps };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.IpcRequest);
        Assert.Contains("\"cmd\":\"ps\"", json);
        Assert.DoesNotContain("\"ns\"", json);
    }

    [Theory]
    [InlineData(IpcCommands.Start)]
    [InlineData(IpcCommands.Stop)]
    [InlineData(IpcCommands.Restart)]
    [InlineData(IpcCommands.Ps)]
    [InlineData(IpcCommands.Reload)]
    public void IpcRequest_RoundTrip_AllCommands(string cmd)
    {
        var req = new IpcRequest { Cmd = cmd, Ns = cmd == IpcCommands.Ps ? null : "ns1" };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.IpcRequest);
        var deserialized = JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcRequest)!;
        Assert.Equal(cmd, deserialized.Cmd);
        Assert.Equal(req.Ns, deserialized.Ns);
    }

    [Fact]
    public void IpcResponse_Success_Serializes()
    {
        var resp = IpcResponse.Success();
        var json = JsonSerializer.Serialize(resp, IpcJsonContext.Default.IpcResponse);
        Assert.Contains("\"ok\":true", json);
        Assert.DoesNotContain("\"error\"", json);
    }

    [Fact]
    public void IpcResponse_Failure_Serializes()
    {
        var resp = IpcResponse.Failure("namespace 'xyz' not found");
        var json = JsonSerializer.Serialize(resp, IpcJsonContext.Default.IpcResponse);
        Assert.Contains("\"ok\":false", json);
        Assert.Contains("\"error\"", json);
        Assert.Contains("xyz", json);
    }

    [Fact]
    public void IpcResponse_RoundTrip()
    {
        var resp = IpcResponse.Failure("test error");
        var json = JsonSerializer.Serialize(resp, IpcJsonContext.Default.IpcResponse);
        var deserialized = JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcResponse)!;
        Assert.False(deserialized.Ok);
        Assert.Equal("test error", deserialized.Error);
    }

    [Fact]
    public void PsData_Serializes()
    {
        var data = new PsData
        {
            Services =
            [
                new ServiceStatusEntry
                {
                    Ns = "app",
                    Service = "serv0",
                    Status = "running",
                    Pid = 1234,
                    Health = "healthy",
                    Restarts = 0,
                    Uptime = "2h30m",
                }
            ]
        };

        var json = JsonSerializer.Serialize(data, IpcJsonContext.Default.PsData);
        Assert.Contains("\"ns\":\"app\"", json);
        Assert.Contains("\"service\":\"serv0\"", json);
        Assert.Contains("\"pid\":1234", json);
    }
}
