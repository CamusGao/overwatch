using System.Text;
using Overwatch.Config.Models;

namespace Overwatch.ServiceHost;

/// <summary>
/// Writes service stdout/stderr to log files with size-based and age-based rotation.
/// </summary>
public sealed class LogWriter : IDisposable
{
    private readonly LogsConfig _config;
    private readonly string _serviceName;
    private readonly string _logDir;
    private StreamWriter? _writer;
    private string? _currentLogPath;
    private long _bytesWritten;
    private readonly long _maxSizeBytes;
    private readonly TimeSpan _maxAge;
    private readonly object _lock = new();

    public LogWriter(LogsConfig config, string serviceName, string? defaultLogBaseDir = null)
    {
        _config = config;
        _serviceName = serviceName;
        _logDir = config.Path ?? Path.Combine(defaultLogBaseDir ?? "logs", serviceName);
        _maxSizeBytes = ParseSize(config.MaxSize);
        _maxAge = ParseAge(config.MaxAge);
    }

    /// <summary>Opens the log file for writing.</summary>
    public void Open()
    {
        Directory.CreateDirectory(_logDir);
        OpenNewFile();
        CleanOldFiles();
    }

    /// <summary>Writes a line to the log file, rotating if necessary.</summary>
    public void WriteLine(string line)
    {
        lock (_lock)
        {
            if (_writer is null) return;

            var bytes = Encoding.UTF8.GetByteCount(line) + 1; // +1 for newline
            if (_bytesWritten + bytes > _maxSizeBytes)
            {
                Rotate();
            }

            _writer.WriteLine(line);
            _writer.Flush();
            _bytesWritten += bytes;
        }
    }

    private void Rotate()
    {
        _writer?.Close();
        _writer = null;

        // Rename current log to timestamped backup
        if (_currentLogPath is not null && File.Exists(_currentLogPath))
        {
            var rotated = Path.Combine(_logDir, $"service-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
            File.Move(_currentLogPath, rotated, overwrite: true);
        }

        OpenNewFile();
        CleanOldFiles();
    }

    private void OpenNewFile()
    {
        _currentLogPath = Path.Combine(_logDir, "service.log");
        _writer = new StreamWriter(_currentLogPath, append: true, Encoding.UTF8) { AutoFlush = false };
        _bytesWritten = new FileInfo(_currentLogPath).Exists ? new FileInfo(_currentLogPath).Length : 0;
    }

    private void CleanOldFiles()
    {
        if (_maxAge == TimeSpan.Zero) return;
        var cutoff = DateTime.UtcNow - _maxAge;
        foreach (var file in Directory.GetFiles(_logDir, "service-*.log"))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
            {
                try { File.Delete(file); } catch { /* ignore */ }
            }
        }
    }

    public static long ParseSize(string value)
    {
        value = value.Trim();
        if (value.EndsWith("GB", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(value[..^2], out var gb)) return gb * 1024 * 1024 * 1024;
        if (value.EndsWith("MB", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(value[..^2], out var mb)) return mb * 1024 * 1024;
        if (value.EndsWith("KB", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(value[..^2], out var kb)) return kb * 1024;
        if (long.TryParse(value, out var bytes)) return bytes;
        return 100 * 1024 * 1024; // default 100MB
    }

    public static TimeSpan ParseAge(string value)
    {
        value = value.Trim();
        if (value.EndsWith('d') && int.TryParse(value[..^1], out var days)) return TimeSpan.FromDays(days);
        if (value.EndsWith('h') && int.TryParse(value[..^1], out var hours)) return TimeSpan.FromHours(hours);
        if (value.EndsWith('m') && int.TryParse(value[..^1], out var mins)) return TimeSpan.FromMinutes(mins);
        return TimeSpan.FromDays(7); // default 7d
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
