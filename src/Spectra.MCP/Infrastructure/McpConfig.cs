namespace Spectra.MCP.Infrastructure;

/// <summary>
/// Configuration for MCP Execution Server.
/// </summary>
public sealed class McpConfig
{
    /// <summary>
    /// Base path for the execution database and reports.
    /// Defaults to current directory.
    /// </summary>
    public string BasePath { get; set; } = Environment.CurrentDirectory;

    /// <summary>
    /// Timeout for paused runs before marking as abandoned.
    /// Defaults to 72 hours.
    /// </summary>
    public TimeSpan AbandonedRunTimeout { get; set; } = TimeSpan.FromHours(72);

    /// <summary>
    /// Path to test suite directories.
    /// Defaults to "test-cases" subdirectory.
    /// </summary>
    public string TestsPath { get; set; } = "test-cases";

    /// <summary>
    /// Path for generated reports.
    /// Defaults to "reports" subdirectory.
    /// </summary>
    public string ReportsPath { get; set; } = "reports";

    /// <summary>
    /// Logging verbosity level.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Loads configuration from environment variables.
    /// </summary>
    public static McpConfig FromEnvironment()
    {
        var config = new McpConfig();

        if (Environment.GetEnvironmentVariable("SPECTRA_BASE_PATH") is { } basePath)
        {
            config.BasePath = basePath;
        }

        if (Environment.GetEnvironmentVariable("SPECTRA_TESTS_PATH") is { } testsPath)
        {
            config.TestsPath = testsPath;
        }

        if (Environment.GetEnvironmentVariable("SPECTRA_REPORTS_PATH") is { } reportsPath)
        {
            config.ReportsPath = reportsPath;
        }

        if (Environment.GetEnvironmentVariable("SPECTRA_LOG_LEVEL") is { } logLevel &&
            Enum.TryParse<LogLevel>(logLevel, true, out var level))
        {
            config.LogLevel = level;
        }

        if (Environment.GetEnvironmentVariable("SPECTRA_ABANDONED_TIMEOUT_HOURS") is { } timeoutStr &&
            int.TryParse(timeoutStr, out var hours))
        {
            config.AbandonedRunTimeout = TimeSpan.FromHours(hours);
        }

        return config;
    }
}

/// <summary>
/// Logging verbosity levels.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    None
}
