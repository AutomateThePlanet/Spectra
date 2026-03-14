namespace Spectra.MCP.Infrastructure;

/// <summary>
/// Logging infrastructure for MCP Server.
/// Logs to stderr to keep stdout clear for JSON-RPC messages.
/// </summary>
public sealed class McpLogging
{
    private readonly LogLevel _minLevel;
    private readonly TextWriter _output;

    public McpLogging(LogLevel minLevel = LogLevel.Info, TextWriter? output = null)
    {
        _minLevel = minLevel;
        _output = output ?? Console.Error;
    }

    public void LogDebug(string message) => Log(LogLevel.Debug, message);
    public void LogInfo(string message) => Log(LogLevel.Info, message);
    public void LogWarning(string message) => Log(LogLevel.Warning, message);
    public void LogError(string message) => Log(LogLevel.Error, message);

    public void Log(LogLevel level, string message)
    {
        if (level < _minLevel || _minLevel == LogLevel.None)
        {
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpperInvariant()[..3];
        _output.WriteLine($"[{timestamp}] [{levelStr}] {message}");
    }

    public void LogException(Exception ex, string context)
    {
        LogError($"{context}: {ex.Message}");
        if (_minLevel == LogLevel.Debug)
        {
            LogDebug(ex.StackTrace ?? "No stack trace available");
        }
    }
}
