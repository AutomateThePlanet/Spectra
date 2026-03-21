using Spectra.CLI.Dashboard;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Commands.Dashboard;

/// <summary>
/// Handles the dashboard command execution.
/// </summary>
public sealed class DashboardHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;

    public DashboardHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, bool dryRun = false)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
    }

    public async Task<int> ExecuteAsync(
        string outputPath,
        string? title,
        string? templatePath,
        CancellationToken ct = default)
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Resolve output path
        if (!Path.IsPathRooted(outputPath))
        {
            outputPath = Path.Combine(currentDir, outputPath);
        }

        try
        {
            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine("Collecting dashboard data...");
            }

            // Collect data
            var collector = new DataCollector(currentDir);
            var data = await collector.CollectAsync();

            // Override repository name with title if provided
            if (!string.IsNullOrEmpty(title))
            {
                data = data with { Repository = title };
            }

            // Report statistics
            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine($"  Suites: {data.Suites.Count}");
                Console.WriteLine($"  Tests: {data.Tests.Count}");
                Console.WriteLine($"  Runs: {data.Runs.Count}");
            }

            if (data.Suites.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No test suites found. Run 'spectra index' to generate indexes.");
                return ExitCodes.Error;
            }

            if (_dryRun)
            {
                Console.WriteLine();
                Console.WriteLine($"Would generate dashboard to: {outputPath}");
                Console.WriteLine($"  index.html with {data.Tests.Count} tests embedded");
                Console.WriteLine($"  styles/main.css");
                Console.WriteLine($"  scripts/app.js");
                return ExitCodes.Success;
            }

            // Generate dashboard
            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine();
                Console.WriteLine($"Generating dashboard to: {outputPath}");
            }

            var generator = new DashboardGenerator(
                templatePath is not null ? Path.Combine(templatePath, "index.html") : null);
            await generator.GenerateAsync(data, outputPath);

            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine();
                Console.WriteLine("Dashboard generated successfully!");
                Console.WriteLine($"Open {Path.Combine(outputPath, "index.html")} in a browser to view.");
                var relativePath = Path.GetRelativePath(currentDir, outputPath);
                Console.WriteLine();
                Console.WriteLine($"Or serve it locally:  npx serve {relativePath}");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating dashboard: {ex.Message}");
            if (_verbosity >= VerbosityLevel.Detailed)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return ExitCodes.Error;
        }
    }
}
