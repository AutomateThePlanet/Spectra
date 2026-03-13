using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Index;

/// <summary>
/// Handles the index command execution.
/// </summary>
public sealed class IndexHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;

    public IndexHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, bool dryRun = false)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
    }

    public async Task<int> ExecuteAsync(string? suite, bool rebuild, CancellationToken ct = default)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Load config if exists
        SpectraConfig? config = null;
        if (File.Exists(configPath))
        {
            try
            {
                var configJson = await File.ReadAllTextAsync(configPath, ct);
                config = JsonSerializer.Deserialize<SpectraConfig>(configJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error reading config: {ex.Message}");
                return ExitCodes.Error;
            }
        }

        var testsDir = config?.Tests?.Dir ?? "tests";
        var testsPath = Path.Combine(currentDir, testsDir);

        if (!Directory.Exists(testsPath))
        {
            Console.Error.WriteLine($"Tests directory not found: {testsPath}");
            Console.Error.WriteLine("Run 'spectra init' to initialize the project.");
            return ExitCodes.Error;
        }

        // Get suites to index
        var suitesToIndex = GetSuitesToIndex(testsPath, suite);

        if (suitesToIndex.Count == 0)
        {
            if (suite is not null)
            {
                Console.Error.WriteLine($"Suite not found: {suite}");
            }
            else
            {
                Console.Error.WriteLine("No test suites found.");
            }
            return ExitCodes.Error;
        }

        var parser = new TestCaseParser();
        var generator = new IndexGenerator();
        var writer = new IndexWriter();

        var totalIndexed = 0;
        var errors = 0;

        foreach (var suitePath in suitesToIndex)
        {
            var suiteName = Path.GetFileName(suitePath);
            var indexPath = IndexWriter.GetIndexPath(suitePath);

            // Check if rebuild is needed
            if (!rebuild && writer.Exists(indexPath))
            {
                var testFiles = Directory.GetFiles(suitePath, "*.md")
                    .Where(f => !Path.GetFileName(f).StartsWith("_"))
                    .ToList();

                var indexTime = File.GetLastWriteTimeUtc(indexPath);
                var needsUpdate = testFiles.Any(f => File.GetLastWriteTimeUtc(f) > indexTime);

                if (!needsUpdate)
                {
                    if (_verbosity >= VerbosityLevel.Detailed)
                    {
                        Console.WriteLine($"Suite '{suiteName}': Index up to date");
                    }
                    continue;
                }
            }

            // Parse all test files in suite
            var testFiles2 = Directory.GetFiles(suitePath, "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList();

            if (testFiles2.Count == 0)
            {
                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine($"Suite '{suiteName}': No test files");
                }
                continue;
            }

            var tests = new List<TestCase>();
            foreach (var file in testFiles2)
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var relativePath = Path.GetRelativePath(testsPath, file);
                var parseResult = parser.Parse(content, relativePath);

                if (!parseResult.IsSuccess)
                {
                    Console.Error.WriteLine($"Parse error in {relativePath}:");
                    foreach (var error in parseResult.Errors)
                    {
                        Console.Error.WriteLine($"  [{error.Code}] {error.Message}");
                    }
                    errors++;
                    continue;
                }

                tests.Add(parseResult.Value!);
            }

            // Generate index
            var index = generator.Generate(suiteName, tests);

            if (_dryRun)
            {
                Console.WriteLine($"Would write index for '{suiteName}' ({index.TestCount} tests)");
            }
            else
            {
                await writer.WriteAsync(indexPath, index, ct);

                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine($"Indexed '{suiteName}': {index.TestCount} tests");
                }
            }

            totalIndexed += index.TestCount;
        }

        // Summary
        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine();
            Console.WriteLine($"Indexed {totalIndexed} test(s) in {suitesToIndex.Count} suite(s)");
            if (errors > 0)
            {
                Console.WriteLine($"Parse errors: {errors}");
            }
        }

        return errors > 0 ? ExitCodes.Error : ExitCodes.Success;
    }

    private static List<string> GetSuitesToIndex(string testsPath, string? suite)
    {
        if (suite is not null)
        {
            var suitePath = Path.Combine(testsPath, suite);
            return Directory.Exists(suitePath) ? [suitePath] : [];
        }

        return Directory.GetDirectories(testsPath)
            .Where(d => !Path.GetFileName(d).StartsWith("_"))
            .ToList();
    }
}
