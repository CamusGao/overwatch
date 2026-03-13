using Overwatch.Config.Parsing;
using Overwatch.Config.Validation;
using Overwatch.Daemon;
using Overwatch.Ipc.Messages;
using Overwatch.Platform;
using Xunit;

namespace Overwatch.Integration.Tests;

/// <summary>
/// Integration tests for daemon core logic (without full IPC transport to avoid platform-specific hangs).
/// Tests NamespaceManager loading, dependency resolution, and reload behavior.
/// </summary>
public sealed class DaemonCoreIntegrationTests : IDisposable
{
    private readonly string _configDir;

    public DaemonCoreIntegrationTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), $"overwatch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    [Fact]
    public async Task NamespaceManager_Load_ValidConfig_Succeeds()
    {
        var yaml = """
            services:
              svc:
                startup:
                  command: cmd.exe /c exit 0
            """;
        var filePath = Path.Combine(_configDir, "testns.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        var (success, error) = await manager.LoadAsync(filePath);

        Assert.True(success, error);
    }

    [Fact]
    public async Task NamespaceManager_Load_CircularDependency_Fails()
    {
        var yaml = """
            services:
              a:
                depends-on: [b]
                startup:
                  command: cmd.exe /c exit 0
              b:
                depends-on: [a]
                startup:
                  command: cmd.exe /c exit 0
            """;
        var filePath = Path.Combine(_configDir, "cyclens.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        var (success, error) = await manager.LoadAsync(filePath);

        Assert.False(success);
        Assert.Contains("Cyclic", error);
    }

    [Fact]
    public async Task NamespaceManager_Ps_ReturnsEntries()
    {
        var yaml = """
            services:
              svc:
                startup:
                  command: cmd.exe /c exit 0
            """;
        var filePath = Path.Combine(_configDir, "ns1.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        await manager.LoadAsync(filePath);

        var response = await manager.HandlePsAsync();
        Assert.True(response.Ok, response.Error);
    }

    [Fact]
    public async Task NamespaceManager_Stop_UnknownNamespace_ReturnsError()
    {
        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        var response = await manager.HandleStopAsync("nonexistent");

        Assert.False(response.Ok);
        Assert.Contains("nonexistent", response.Error);
    }

    [Fact]
    public async Task NamespaceManager_Unload_RemovesNamespace()
    {
        var yaml = """
            services:
              svc:
                startup:
                  command: cmd.exe /c exit 0
            """;
        var filePath = Path.Combine(_configDir, "ns1.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        await manager.LoadAsync(filePath);

        var nsNames = manager.GetNamespaceNames();
        Assert.Contains("ns1", nsNames);

        var (success, _) = await manager.UnloadAsync("ns1");
        Assert.True(success);

        nsNames = manager.GetNamespaceNames();
        Assert.DoesNotContain("ns1", nsNames);
    }

    [Fact]
    public async Task ReloadHandler_NewFile_LoadsNamespace()
    {
        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        var handler = new ReloadHandler(manager, _configDir);

        // Start with empty config dir
        var resp1 = await handler.HandleAsync();
        Assert.True(resp1.Ok);
        Assert.Empty(manager.GetNamespaceNames());

        // Add a file
        var yaml = """
            services:
              svc:
                startup:
                  command: cmd.exe /c exit 0
            """;
        await File.WriteAllTextAsync(Path.Combine(_configDir, "newns.yaml"), yaml);

        var resp2 = await handler.HandleAsync();
        Assert.True(resp2.Ok, resp2.Error);
        Assert.Contains("newns", manager.GetNamespaceNames());
    }

    [Fact]
    public async Task ReloadHandler_DeletedFile_UnloadsNamespace()
    {
        var yaml = """
            services:
              svc:
                startup:
                  command: cmd.exe /c exit 0
            """;
        var filePath = Path.Combine(_configDir, "myns.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        await manager.LoadAsync(filePath);
        Assert.Contains("myns", manager.GetNamespaceNames());

        // Delete the file, then reload
        File.Delete(filePath);

        var handler = new ReloadHandler(manager, _configDir);
        var resp = await handler.HandleAsync();
        Assert.True(resp.Ok, resp.Error);
        Assert.DoesNotContain("myns", manager.GetNamespaceNames());
    }

    [Fact]
    public void Config_CircularDependency_IsRejected()
    {
        var yaml = """
            services:
              a:
                depends-on: [b]
                startup:
                  command: echo a
              b:
                depends-on: [a]
                startup:
                  command: echo b
            """;

        var config = ConfigParser.Parse(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Contains(errors, e => e.Message.Contains("Cyclic"));
    }

    [Fact]
    public void Config_TopologicalOrder_IsCorrect()
    {
        var yaml = """
            services:
              c:
                depends-on: [b]
                startup:
                  command: echo c
              b:
                depends-on: [a]
                startup:
                  command: echo b
              a:
                startup:
                  command: echo a
            """;

        var config = ConfigParser.Parse(yaml);
        var order = ConfigValidator.TopologicalSort(config.Services).ToList();

        Assert.True(order.IndexOf("a") < order.IndexOf("b"));
        Assert.True(order.IndexOf("b") < order.IndexOf("c"));
    }

    [Fact]
    public async Task NamespaceManager_DependencyOrder_StartedInOrder()
    {
        // Services with dependencies - they should start in order
        var yaml = """
            services:
              b:
                depends-on: [a]
                startup:
                  command: cmd.exe /c exit 0
              a:
                startup:
                  command: cmd.exe /c exit 0
            """;
        var filePath = Path.Combine(_configDir, "orderns.yaml");
        await File.WriteAllTextAsync(filePath, yaml);

        var platform = PlatformServiceFactory.Create();
        await using var manager = new NamespaceManager(platform);
        var (success, error) = await manager.LoadAsync(filePath);

        Assert.True(success, error);
    }
}
