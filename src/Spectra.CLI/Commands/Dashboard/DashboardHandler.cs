using System.Text.Json;
using Spectra.CLI.Dashboard;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Dashboard;

namespace Spectra.CLI.Commands.Dashboard;

/// <summary>
/// Handles the dashboard command execution.
/// </summary>
public sealed class DashboardHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;
    private readonly OutputFormat _outputFormat;

    public DashboardHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, bool dryRun = false, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(
        string outputPath,
        string? title,
        string? templatePath,
        bool preview = false,
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
            // Load config for branding
            BrandingConfig? brandingConfig = null;
            var configPath = Path.Combine(currentDir, "spectra.config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var configJson = await File.ReadAllTextAsync(configPath, ct);
                    var config = JsonSerializer.Deserialize<SpectraConfig>(configJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    brandingConfig = config?.Dashboard.Branding;
                }
                catch
                {
                    // Config parsing failure should not block dashboard generation
                }
            }

            // Log branding status
            if (_verbosity >= VerbosityLevel.Normal && brandingConfig is not null)
            {
                var name = brandingConfig.CompanyName ?? "custom";
                Console.WriteLine($"Branding: Applying \"{name}\" branding" +
                    (brandingConfig.Theme?.Equals("dark", StringComparison.OrdinalIgnoreCase) == true ? " with dark theme" : ""));
            }

            DashboardData data;

            if (preview)
            {
                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine("Preview mode: using sample data");
                }
                data = SampleDataFactory.CreateSampleData();
            }
            else
            {
                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine("Collecting dashboard data...");
                }

                // Collect data
                var collector = new DataCollector(currentDir);
                data = await collector.CollectAsync();

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

                if (data.Suites.Count == 0 && !preview)
                {
                    Console.WriteLine();
                    Console.WriteLine("No test suites found. Run 'spectra index' to generate indexes.");
                    return ExitCodes.Error;
                }
            }

            if (_dryRun)
            {
                Console.WriteLine();
                Console.WriteLine($"Would generate dashboard to: {outputPath}");
                Console.WriteLine($"  index.html with {data.Tests.Count} tests embedded");
                Console.WriteLine($"  styles/main.css");
                Console.WriteLine($"  scripts/app.js");
                if (brandingConfig is not null)
                {
                    Console.WriteLine($"  branding: {brandingConfig.CompanyName ?? "default"}");
                }
                return ExitCodes.Success;
            }

            // Generate dashboard
            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine();
                Console.WriteLine($"Generating dashboard to: {outputPath}");
            }

            var generator = new DashboardGenerator(
                templatePath is not null ? Path.Combine(templatePath, "index.html") : null,
                brandingConfig,
                currentDir);
            await generator.GenerateAsync(data, outputPath);

            // Report branding warnings
            foreach (var warning in generator.BrandingWarnings)
            {
                Console.WriteLine($"Branding: Warning — {warning}");
            }

            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new DashboardResult
                {
                    Command = "dashboard",
                    Status = "success",
                    OutputPath = Path.GetRelativePath(currentDir, outputPath),
                    PagesGenerated = 1,
                    SuitesIncluded = data.Suites.Count,
                    TestsIncluded = data.Tests.Count,
                    RunsIncluded = data.Runs.Count
                });
                return ExitCodes.Success;
            }

            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine();
                Console.WriteLine("Dashboard generated successfully!");
                Console.WriteLine($"Open {Path.Combine(outputPath, "index.html")} in a browser to view.");
                var relativePath = Path.GetRelativePath(currentDir, outputPath);
                Console.WriteLine();
                Console.WriteLine($"Or serve it locally:  npx serve {relativePath}");
            }

            NextStepHints.Print("dashboard", true, _verbosity, new HintContext { OutputPath = Path.GetRelativePath(currentDir, outputPath) }, _outputFormat);
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
