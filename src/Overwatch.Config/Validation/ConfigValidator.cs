using Overwatch.Config.Models;

namespace Overwatch.Config.Validation;

/// <summary>
/// Validates a <see cref="NamespaceConfig"/> for semantic correctness.
/// </summary>
public static class ConfigValidator
{
    /// <summary>
    /// Validates the given namespace config. Returns a list of errors (empty if valid).
    /// </summary>
    public static IReadOnlyList<ValidationError> Validate(NamespaceConfig config)
    {
        var errors = new List<ValidationError>();

        foreach (var (name, service) in config.Services)
        {
            ValidateService(name, service, config.Services, errors);
        }

        ValidateCyclicDependencies(config.Services, errors);

        return errors;
    }

    private static void ValidateService(
        string name,
        ServiceConfig service,
        Dictionary<string, ServiceConfig> allServices,
        List<ValidationError> errors)
    {
        // startup.command must not be empty
        var startupCommand = service.Startup.Command?.Resolve();
        if (string.IsNullOrWhiteSpace(startupCommand))
        {
            errors.Add(new ValidationError(name, "startup.command is required and cannot be empty."));
        }

        // forking type requires a stop.command
        if (service.Startup.Type == StartupType.Forking)
        {
            var stopCommand = service.Stop?.Command?.Resolve();
            if (string.IsNullOrWhiteSpace(stopCommand))
            {
                errors.Add(new ValidationError(name, "stop.command is required when startup.type is 'forking'."));
            }
        }

        // depends-on services must exist in the same namespace
        if (service.DependsOn is { Count: > 0 })
        {
            foreach (var dep in service.DependsOn)
            {
                if (!allServices.ContainsKey(dep))
                {
                    errors.Add(new ValidationError(name, $"depends-on references unknown service '{dep}'."));
                }
            }
        }
    }

    private static void ValidateCyclicDependencies(
        Dictionary<string, ServiceConfig> services,
        List<ValidationError> errors)
    {
        // Kahn's algorithm for cycle detection
        var inDegree = new Dictionary<string, int>(services.Count);
        var adjacency = new Dictionary<string, List<string>>(services.Count);

        foreach (var name in services.Keys)
        {
            inDegree[name] = 0;
            adjacency[name] = [];
        }

        foreach (var (name, service) in services)
        {
            if (service.DependsOn is null) continue;
            foreach (var dep in service.DependsOn)
            {
                if (!services.ContainsKey(dep)) continue; // already reported above
                adjacency[dep].Add(name);
                inDegree[name]++;
            }
        }

        var queue = new Queue<string>();
        foreach (var (name, degree) in inDegree)
        {
            if (degree == 0) queue.Enqueue(name);
        }

        var processed = 0;
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            processed++;
            foreach (var neighbor in adjacency[node])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0) queue.Enqueue(neighbor);
            }
        }

        if (processed < services.Count)
        {
            // Find nodes involved in cycles
            var cycleNodes = inDegree
                .Where(kv => kv.Value > 0)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var node in cycleNodes)
            {
                errors.Add(new ValidationError(node,
                    $"Cyclic dependency detected involving service '{node}'."));
            }
        }
    }

    /// <summary>
    /// Returns the topological order of services (dependencies first).
    /// Throws if cycles are present — call <see cref="Validate"/> first.
    /// </summary>
    public static IReadOnlyList<string> TopologicalSort(Dictionary<string, ServiceConfig> services)
    {
        var inDegree = new Dictionary<string, int>(services.Count);
        var adjacency = new Dictionary<string, List<string>>(services.Count);

        foreach (var name in services.Keys)
        {
            inDegree[name] = 0;
            adjacency[name] = [];
        }

        foreach (var (name, service) in services)
        {
            if (service.DependsOn is null) continue;
            foreach (var dep in service.DependsOn)
            {
                if (!services.ContainsKey(dep)) continue;
                adjacency[dep].Add(name);
                inDegree[name]++;
            }
        }

        var queue = new Queue<string>();
        foreach (var (name, degree) in inDegree)
        {
            if (degree == 0) queue.Enqueue(name);
        }

        var result = new List<string>(services.Count);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);
            foreach (var neighbor in adjacency[node])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0) queue.Enqueue(neighbor);
            }
        }

        if (result.Count < services.Count)
            throw new InvalidOperationException("Cyclic dependency detected; cannot perform topological sort.");

        return result;
    }
}
