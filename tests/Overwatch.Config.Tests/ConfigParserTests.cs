using Overwatch.Config.Models;
using Overwatch.Config.Parsing;
using Xunit;

namespace Overwatch.Config.Tests;

public class ConfigParserTests
{
    [Fact]
    public void Parse_FullExample_MapsAllFields()
    {
        const string yaml = """
            services:
              serv0:
                enabled: true
                user: www-data
                depends-on:
                  - serv1
                working-dir: /app/serv0
                restart: on-failure:3
                environments:
                  JAVA_HOME: /usr/lib/jvm/java-11
                  APP_ENV: production
                logs:
                  path: /var/log/overwatch/serv0
                  max-size: 100MB
                  max-age: 7d
                health-check:
                  test: ["curl", "-f", "http://localhost:8080/health"]
                  interval: 20s
                  timeout: 5s
                  retries: 3
                  start-period: 10s
                startup:
                  command:
                    linux: python app.py
                    windows: python app.py
                  type: simple
                  timeout: 20s
                stop:
                  command:
                    linux: kill -9 ${_PID}
                    windows: taskkill /F /PID ${_PID}
                  timeout: 10s
                  retries: 3
              serv1:
                startup:
                  command: dotnet server.dll
            """;

        var config = ConfigParser.Parse(yaml);

        Assert.Equal(2, config.Services.Count);

        var serv0 = config.Services["serv0"];
        Assert.True(serv0.Enabled);
        Assert.Equal("www-data", serv0.User);
        Assert.Equal(["serv1"], serv0.DependsOn);
        Assert.Equal("/app/serv0", serv0.WorkingDir);
        Assert.Equal("/var/log/overwatch/serv0", serv0.Logs!.Path);
        Assert.Equal("100MB", serv0.Logs.MaxSize);
        Assert.Equal("7d", serv0.Logs.MaxAge);
        Assert.Equal(["curl", "-f", "http://localhost:8080/health"], serv0.HealthCheck!.Test);
        Assert.Equal(TimeSpan.FromSeconds(20), serv0.HealthCheck.Interval);
        Assert.Equal(TimeSpan.FromSeconds(5), serv0.HealthCheck.Timeout);
        Assert.Equal(3, serv0.HealthCheck.Retries);
        Assert.Equal(TimeSpan.FromSeconds(10), serv0.HealthCheck.StartPeriod);
        Assert.Equal(StartupType.Simple, serv0.Startup.Type);
        Assert.Equal(TimeSpan.FromSeconds(20), serv0.Startup.Timeout);
        Assert.NotNull(serv0.Stop);
        Assert.Equal(TimeSpan.FromSeconds(10), serv0.Stop!.Timeout);
        Assert.Equal(3, serv0.Stop.Retries);

        var serv1 = config.Services["serv1"];
        Assert.True(serv1.Enabled);
        Assert.Equal("dotnet server.dll", serv1.Startup.Command.Linux);
    }

    [Fact]
    public void Parse_EmptyYaml_ReturnsEmptyConfig()
    {
        var config = ConfigParser.Parse("");
        Assert.Empty(config.Services);
    }

    [Fact]
    public void Parse_ServiceEnabledDefault_IsTrue()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command: echo hello
            """;
        var config = ConfigParser.Parse(yaml);
        Assert.True(config.Services["svc"].Enabled);
    }

    [Fact]
    public void Parse_ServiceEnabledFalse_IsCorrect()
    {
        const string yaml = """
            services:
              svc:
                enabled: false
                startup:
                  command: echo hello
            """;
        var config = ConfigParser.Parse(yaml);
        Assert.False(config.Services["svc"].Enabled);
    }
}
