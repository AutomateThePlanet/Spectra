using Microsoft.Extensions.Logging;

namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Configures logging based on verbosity level.
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// Creates a logger factory configured for the specified verbosity level.
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory(VerbosityLevel verbosity)
    {
        var minLevel = verbosity switch
        {
            VerbosityLevel.Quiet => LogLevel.Error,
            VerbosityLevel.Minimal => LogLevel.Warning,
            VerbosityLevel.Normal => LogLevel.Information,
            VerbosityLevel.Detailed => LogLevel.Debug,
            VerbosityLevel.Diagnostic => LogLevel.Trace,
            _ => LogLevel.Information
        };

        return LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(minLevel)
                .AddConsole(options =>
                {
                    options.FormatterName = "spectra";
                })
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = verbosity < VerbosityLevel.Detailed;
                    options.TimestampFormat = verbosity >= VerbosityLevel.Detailed
                        ? "[HH:mm:ss] "
                        : null;
                    options.IncludeScopes = verbosity >= VerbosityLevel.Diagnostic;
                });
        });
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    public static ILogger<T> CreateLogger<T>(VerbosityLevel verbosity)
    {
        return CreateLoggerFactory(verbosity).CreateLogger<T>();
    }
}
