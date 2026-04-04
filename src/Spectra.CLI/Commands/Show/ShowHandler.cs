using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Show;

/// <summary>
/// Handles the show command execution.
/// </summary>
public sealed class ShowHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public ShowHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    /// <summary>
    /// Executes the show command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string testId,
        bool showRaw,
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

            var testsDir = Path.Combine(basePath, config?.Tests?.Dir ?? "tests");

            if (!Directory.Exists(testsDir))
            {
                Console.Error.WriteLine($"Tests directory not found: {testsDir}");
                return ExitCodes.Error;
            }

            // Search for the test in all suites
            var indexReader = new IndexWriter();
            var suiteDirs = Directory.GetDirectories(testsDir)
                .Where(d => !Path.GetFileName(d).StartsWith("_"))
                .ToList();

            foreach (var suiteDir in suiteDirs)
            {
                var indexPath = IndexWriter.GetIndexPath(suiteDir);
                if (!File.Exists(indexPath))
                {
                    continue;
                }

                var index = await indexReader.ReadAsync(indexPath, ct);
                if (index is null)
                {
                    continue;
                }

                var testEntry = index.Tests.FirstOrDefault(t =>
                    t.Id.Equals(testId, StringComparison.OrdinalIgnoreCase));

                if (testEntry is not null)
                {
                    var testPath = Path.Combine(testsDir, testEntry.File);
                    return await DisplayTestAsync(testPath, showRaw, ct);
                }
            }

            Console.Error.WriteLine($"Test not found: {testId}");
            return ExitCodes.Error;
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

    private async Task<int> DisplayTestAsync(string testPath, bool showRaw, CancellationToken ct)
    {
        if (!File.Exists(testPath))
        {
            Console.Error.WriteLine($"Test file not found: {testPath}");
            return ExitCodes.Error;
        }

        var content = await File.ReadAllTextAsync(testPath, ct);

        if (showRaw)
        {
            Console.WriteLine(content);
            return ExitCodes.Success;
        }

        // Parse and display formatted
        var parser = new TestCaseParser();
        var result = parser.Parse(content, testPath);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine("Error parsing test file:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  [{error.Code}] {error.Message}");
            }
            return ExitCodes.Error;
        }

        var test = result.Value!;

        Console.WriteLine($"ID:        {test.Id}");
        Console.WriteLine($"Title:     {test.Title}");
        Console.WriteLine($"Priority:  {test.Priority}");
        Console.WriteLine($"Component: {test.Component ?? "(none)"}");
        Console.WriteLine($"Tags:      {string.Join(", ", test.Tags)}");
        Console.WriteLine($"Source:    {string.Join(", ", test.SourceRefs)}");

        if (!string.IsNullOrEmpty(test.DependsOn))
        {
            Console.WriteLine($"Depends:   {test.DependsOn}");
        }

        if (!string.IsNullOrWhiteSpace(test.Preconditions))
        {
            Console.WriteLine();
            Console.WriteLine("Preconditions:");
            Console.WriteLine($"  {test.Preconditions}");
        }

        Console.WriteLine();
        Console.WriteLine("Steps:");
        for (var i = 0; i < test.Steps.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {test.Steps[i]}");
        }

        Console.WriteLine();
        Console.WriteLine("Expected Result:");
        Console.WriteLine($"  {test.ExpectedResult}");

        if (!string.IsNullOrWhiteSpace(test.TestData))
        {
            Console.WriteLine();
            Console.WriteLine("Test Data:");
            Console.WriteLine($"  {test.TestData}");
        }

        return ExitCodes.Success;
    }
}
