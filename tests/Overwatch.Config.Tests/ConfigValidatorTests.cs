using Overwatch.Config.Models;
using Overwatch.Config.Parsing;
using Overwatch.Config.Validation;
using Xunit;

namespace Overwatch.Config.Tests;

public class ConfigValidatorTests
{
    private static NamespaceConfig ParseYaml(string yaml) => ConfigParser.Parse(yaml);

    [Fact]
    public void Validate_ValidConfig_ReturnsNoErrors()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command: echo hello
            """;
        var config = ParseYaml(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingStartupCommand_ReturnsError()
    {
        // startup.command will be null/empty PlatformCommand
        const string yaml = """
            services:
              svc:
                startup:
                  type: simple
            """;
        var config = ParseYaml(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Contains(errors, e => e.ServiceName == "svc" && e.Message.Contains("startup.command"));
    }

    [Fact]
    public void Validate_ForkingWithoutStopCommand_ReturnsError()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command: python app.py
                  type: forking
            """;
        var config = ParseYaml(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Contains(errors, e => e.ServiceName == "svc" && e.Message.Contains("stop.command"));
    }

    [Fact]
    public void Validate_ForkingWithStopCommand_ReturnsNoError()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command: python app.py
                  type: forking
                stop:
                  command: pkill python
            """;
        var config = ParseYaml(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DependsOnUnknownService_ReturnsError()
    {
        const string yaml = """
            services:
              svc:
                depends-on:
                  - nonexistent
                startup:
                  command: echo
            """;
        var config = ParseYaml(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Contains(errors, e => e.ServiceName == "svc" && e.Message.Contains("nonexistent"));
    }

    [Fact]
    public void Validate_DirectCycle_ReturnsError()
    {
        const string yaml = """
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
        var config = ParseYaml(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Contains(errors, e => e.Message.Contains("Cyclic"));
    }

    [Fact]
    public void Validate_ThreeNodeCycle_ReturnsError()
    {
        const string yaml = """
            services:
              a:
                depends-on: [c]
                startup:
                  command: echo a
              b:
                depends-on: [a]
                startup:
                  command: echo b
              c:
                depends-on: [b]
                startup:
                  command: echo c
            """;
        var config = ParseYaml(yaml);
        var errors = ConfigValidator.Validate(config);
        Assert.Contains(errors, e => e.Message.Contains("Cyclic"));
    }

    [Fact]
    public void TopologicalSort_LinearDeps_ReturnsCorrectOrder()
    {
        const string yaml = """
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
        var config = ParseYaml(yaml);
        var order = ConfigValidator.TopologicalSort(config.Services).ToList();
        var indexA = order.IndexOf("a");
        var indexB = order.IndexOf("b");
        var indexC = order.IndexOf("c");
        Assert.True(indexA < indexB, "a should come before b");
        Assert.True(indexB < indexC, "b should come before c");
    }

    [Fact]
    public void Validate_DurationFields_ParsedCorrectly()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command: echo
                  timeout: 30s
                health-check:
                  test: [curl, http://localhost]
                  interval: 1m
                  timeout: 5s
                  retries: 3
                  start-period: 10s
                stop:
                  timeout: 7d
            """;
        var config = ParseYaml(yaml);
        var svc = config.Services["svc"];
        Assert.Equal(TimeSpan.FromSeconds(30), svc.Startup.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(1), svc.HealthCheck!.Interval);
        Assert.Equal(TimeSpan.FromSeconds(5), svc.HealthCheck.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(10), svc.HealthCheck.StartPeriod);
        Assert.Equal(TimeSpan.FromDays(7), svc.Stop!.Timeout);
    }
}
