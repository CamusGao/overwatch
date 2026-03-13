using Overwatch.Config.Models;
using Overwatch.Config.Parsing;
using Xunit;

namespace Overwatch.Config.Tests;

public class RestartPolicyTests
{
    [Theory]
    [InlineData("no", RestartMode.No, null)]
    [InlineData("always", RestartMode.Always, null)]
    [InlineData("on-failure", RestartMode.OnFailure, null)]
    [InlineData("on-failure:3", RestartMode.OnFailure, 3)]
    [InlineData("on-failure:10", RestartMode.OnFailure, 10)]
    public void RestartRule_Parse_StringVariants(string input, RestartMode mode, int? maxRetries)
    {
        var rule = RestartRule.Parse(input);
        Assert.Equal(mode, rule.Mode);
        Assert.Equal(maxRetries, rule.MaxRetries);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("on-failure:0")]
    [InlineData("on-failure:abc")]
    [InlineData("")]
    public void RestartRule_Parse_InvalidThrows(string input)
    {
        Assert.ThrowsAny<Exception>(() => RestartRule.Parse(input));
    }

    [Fact]
    public void Parse_RestartStringForm_AppliesToBothTriggers()
    {
        const string yaml = """
            services:
              svc:
                restart: on-failure:3
                startup:
                  command: echo
            """;
        var config = ConfigParser.Parse(yaml);
        var policy = config.Services["svc"].Restart!;
        Assert.Equal(RestartMode.OnFailure, policy.OnExit.Mode);
        Assert.Equal(3, policy.OnExit.MaxRetries);
        Assert.Equal(RestartMode.OnFailure, policy.OnUnhealthy.Mode);
        Assert.Equal(3, policy.OnUnhealthy.MaxRetries);
    }

    [Fact]
    public void Parse_RestartObjectForm_SetsIndividualTriggers()
    {
        const string yaml = """
            services:
              svc:
                restart:
                  on-exit: on-failure:3
                  on-unhealthy: always
                startup:
                  command: echo
            """;
        var config = ConfigParser.Parse(yaml);
        var policy = config.Services["svc"].Restart!;
        Assert.Equal(RestartMode.OnFailure, policy.OnExit.Mode);
        Assert.Equal(3, policy.OnExit.MaxRetries);
        Assert.Equal(RestartMode.Always, policy.OnUnhealthy.Mode);
    }

    [Fact]
    public void Parse_NoRestartField_IsNull()
    {
        const string yaml = """
            services:
              svc:
                startup:
                  command: echo
            """;
        var config = ConfigParser.Parse(yaml);
        Assert.Null(config.Services["svc"].Restart);
    }
}
