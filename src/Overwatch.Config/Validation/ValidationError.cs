using Overwatch.Config.Models;

namespace Overwatch.Config.Validation;

/// <summary>Represents a single validation error in a namespace configuration.</summary>
public sealed class ValidationError
{
    public string ServiceName { get; }
    public string Message { get; }

    public ValidationError(string serviceName, string message)
    {
        ServiceName = serviceName;
        Message = message;
    }

    public override string ToString() => $"[{ServiceName}] {Message}";
}
