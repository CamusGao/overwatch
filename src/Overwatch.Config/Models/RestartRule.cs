namespace Overwatch.Config.Models;

/// <summary>How to restart a service after it exits or becomes unhealthy.</summary>
public enum RestartMode
{
    /// <summary>Do not restart.</summary>
    No,
    /// <summary>Always restart.</summary>
    Always,
    /// <summary>Restart on failure only.</summary>
    OnFailure,
}

/// <summary>Restart rule for a single trigger (on-exit or on-unhealthy).</summary>
public sealed class RestartRule
{
    public RestartMode Mode { get; set; } = RestartMode.No;

    /// <summary>Maximum consecutive restart attempts before giving up. Null means unlimited.</summary>
    public int? MaxRetries { get; set; }

    public static readonly RestartRule None = new() { Mode = RestartMode.No };

    /// <summary>Parses a restart rule string like "no", "always", "on-failure", or "on-failure:3".</summary>
    public static RestartRule Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Restart rule value cannot be empty.", nameof(value));

        value = value.Trim();

        if (value == "no") return new RestartRule { Mode = RestartMode.No };
        if (value == "always") return new RestartRule { Mode = RestartMode.Always };
        if (value == "on-failure") return new RestartRule { Mode = RestartMode.OnFailure };

        if (value.StartsWith("on-failure:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = value.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var n) && n > 0)
                return new RestartRule { Mode = RestartMode.OnFailure, MaxRetries = n };
            throw new FormatException($"Invalid restart rule: '{value}'. Expected 'on-failure:N' where N is a positive integer.");
        }

        throw new FormatException($"Invalid restart rule: '{value}'. Expected 'no', 'always', 'on-failure', or 'on-failure:N'.");
    }

    /// <summary>Tries to parse a restart rule string, returning false if invalid.</summary>
    public static bool TryParse(string value, out RestartRule rule)
    {
        try
        {
            rule = Parse(value);
            return true;
        }
        catch
        {
            rule = None;
            return false;
        }
    }
}
