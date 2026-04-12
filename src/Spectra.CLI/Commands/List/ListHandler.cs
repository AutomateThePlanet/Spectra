using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.List;

/// <summary>
/// Handles the list command execution.
/// </summary>
public sealed class ListHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public ListHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    /// <summary>
    /// Executes the list command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? suite,
        bool showAll,
        CancellationToken ct = default)
    {
        try
        {
            var basePath = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(basePath, "spectra.config.json");

            // Load configuration
            SpectraConfig? config = null;
            if (File.Exists(configPath))
            {
                var configJson = await File.ReadAllTextAsync(configPath, ct);
                config = JsonSerializer.Deserialize<SpectraConfig>(configJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            var testsDir = Path.Combine(basePath, config?.Tests?.Dir ?? "test-cases");

            if (!Directory.Exists(testsDir))
            {
                Console.Error.WriteLine($"Tests directory not found: {testsDir}");
                Console.Error.WriteLine("Run 'spectra init' first.");
                return ExitCodes.Error;
            }

            var indexReader = new IndexWriter();

            // Get suites to list
            var suiteDirs = suite is not null
                ? new[] { Path.Combine(testsDir, suite) }.Where(Directory.Exists).ToList()
                : Directory.GetDirectories(testsDir)
                    .Where(d => !Path.GetFileName(d).StartsWith("_"))
                    .ToList();

            if (suiteDirs.Count == 0)
            {
                if (suite is not null)
                {
                    Console.Error.WriteLine($"Suite not found: {suite}");
                    return ExitCodes.Error;
                }
                Console.WriteLine("No test suites found.");
                return ExitCodes.Success;
            }

            var totalTests = 0;
            var suiteEntries = new List<SuiteEntry>();

            foreach (var suiteDir in suiteDirs)
            {
                var suiteName = Path.GetFileName(suiteDir);
                var indexPath = IndexWriter.GetIndexPath(suiteDir);

                if (!File.Exists(indexPath))
                {
                    suiteEntries.Add(new SuiteEntry { Name = suiteName, TestCount = 0 });
                    if (_outputFormat != OutputFormat.Json)
                        Console.WriteLine($"{suiteName}/ (no index - run 'spectra index')");
                    continue;
                }

                var index = await indexReader.ReadAsync(indexPath, ct);
                if (index is null)
                {
                    suiteEntries.Add(new SuiteEntry { Name = suiteName, TestCount = 0 });
                    if (_outputFormat != OutputFormat.Json)
                        Console.WriteLine($"{suiteName}/ (invalid index)");
                    continue;
                }

                suiteEntries.Add(new SuiteEntry
                {
                    Name = suiteName,
                    TestCount = index.TestCount
                });

                if (_outputFormat != OutputFormat.Json)
                {
                    Console.WriteLine($"{suiteName}/ ({index.TestCount} tests)");

                    if (showAll || _verbosity >= VerbosityLevel.Detailed)
                    {
                        foreach (var test in index.Tests)
                        {
                            var priority = test.Priority.ToString().ToLowerInvariant();
                            Console.WriteLine($"  {test.Id}: {test.Title} [{priority}]");
                        }
                    }
                }

                totalTests += index.TestCount;
            }

            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new ListResult
                {
                    Command = "list",
                    Status = "success",
                    Message = $"{totalTests} test(s) in {suiteDirs.Count} suite(s)",
                    Suites = suiteEntries
                });
                return ExitCodes.Success;
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {totalTests} test(s) in {suiteDirs.Count} suite(s)");

            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return ExitCodes.Error;
        }
    }
}
