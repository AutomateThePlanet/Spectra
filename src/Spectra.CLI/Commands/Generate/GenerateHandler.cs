using System.Text.Json;
using Spectra.CLI.Agent;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Handles the generate command execution.
/// </summary>
public sealed class GenerateHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;
    private readonly bool _noReview;

    public GenerateHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        bool dryRun = false,
        bool noReview = false)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _noReview = noReview;
    }

    public async Task<int> ExecuteAsync(string suite, int? count, CancellationToken ct = default)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Load config
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

        if (config is null)
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitCodes.Error;
        }

        // Build document map
        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine("Scanning source documentation...");
        }

        var mapBuilder = new DocumentMapBuilder(config.Source);
        var documentMap = await mapBuilder.BuildAsync(currentDir, ct);

        if (documentMap.Documents.Count == 0)
        {
            Console.Error.WriteLine("No source documentation found. Check your source configuration.");
            return ExitCodes.Error;
        }

        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine($"Found {documentMap.Documents.Count} source file(s)");
        }

        // Load existing tests
        var testsDir = config.Tests?.Dir ?? "tests";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);

        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine($"Found {existingTests.Count} existing test(s) in '{suite}'");
        }

        // Create agent
        var agent = AgentFactory.Create(config.Ai);

        if (!await agent.IsAvailableAsync(ct))
        {
            Console.Error.WriteLine($"AI provider '{agent.ProviderName}' is not available.");
            return ExitCodes.Error;
        }

        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine($"Using AI provider: {agent.ProviderName}");
            Console.WriteLine("Generating tests...");
        }

        // Generate tests
        var prompt = BuildPrompt(suite, count ?? 5, existingTests);
        var result = await agent.GenerateTestsAsync(prompt, documentMap, existingTests, ct);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine("Generation failed:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  {error}");
            }
            return ExitCodes.Error;
        }

        if (result.Tests.Count == 0)
        {
            Console.WriteLine("No new tests generated.");
            return ExitCodes.Success;
        }

        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine($"Generated {result.Tests.Count} test(s)");
        }

        // Preview or write tests
        if (_dryRun)
        {
            Console.WriteLine("\nDry run - tests would be written:");
            foreach (var test in result.Tests)
            {
                Console.WriteLine($"  {test.Id}: {test.Title}");
            }
            return ExitCodes.Success;
        }

        // Interactive review (unless --no-review)
        var testsToWrite = result.Tests.ToList();
        if (!_noReview && testsToWrite.Count > 0)
        {
            testsToWrite = await InteractiveReviewAsync(testsToWrite, ct);
        }

        if (testsToWrite.Count == 0)
        {
            Console.WriteLine("No tests selected for writing.");
            return ExitCodes.Success;
        }

        // Write tests
        var writer = new TestFileWriter();
        foreach (var test in testsToWrite)
        {
            var filePath = TestFileWriter.GetFilePath(testsPath, suite, test.Id);

            // Update test with correct file path
            var testWithPath = new TestCase
            {
                Id = test.Id,
                Title = test.Title,
                Priority = test.Priority,
                Tags = test.Tags,
                Component = test.Component,
                Preconditions = test.Preconditions,
                Environment = test.Environment,
                EstimatedDuration = test.EstimatedDuration,
                DependsOn = test.DependsOn,
                SourceRefs = test.SourceRefs,
                RelatedWorkItems = test.RelatedWorkItems,
                Custom = test.Custom,
                Steps = test.Steps,
                ExpectedResult = test.ExpectedResult,
                TestData = test.TestData,
                FilePath = Path.GetRelativePath(testsPath, filePath)
            };

            await writer.WriteAsync(filePath, testWithPath, ct);

            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine($"  Created: {testWithPath.FilePath}");
            }
        }

        // Update index
        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine("\nUpdating index...");
        }

        var allTests = new List<TestCase>(existingTests);
        allTests.AddRange(testsToWrite);

        var indexGenerator = new IndexGenerator();
        var index = indexGenerator.Generate(suite, allTests);

        var indexWriter = new IndexWriter();
        await indexWriter.WriteAsync(Path.Combine(suitePath, "_index.json"), index, ct);

        // Summary
        Console.WriteLine($"\nGenerated {testsToWrite.Count} test(s) in '{suite}'");
        if (result.TokenUsage is not null)
        {
            Console.WriteLine($"Token usage: {result.TokenUsage.InputTokens} in / {result.TokenUsage.OutputTokens} out");
        }

        return ExitCodes.Success;
    }

    private async Task<List<TestCase>> LoadExistingTestsAsync(
        string suitePath,
        string testsPath,
        CancellationToken ct)
    {
        var tests = new List<TestCase>();

        if (!Directory.Exists(suitePath))
        {
            return tests;
        }

        var parser = new TestCaseParser();
        var files = Directory.GetFiles(suitePath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith("_"));

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, ct);
            var relativePath = Path.GetRelativePath(testsPath, file);
            var result = parser.Parse(content, relativePath);

            if (result.IsSuccess)
            {
                tests.Add(result.Value!);
            }
        }

        return tests;
    }

    private static string BuildPrompt(string suite, int count, IReadOnlyList<TestCase> existingTests)
    {
        var existingIds = string.Join(", ", existingTests.Select(t => t.Id));

        return $"""
            Generate {count} new manual test cases for the '{suite}' feature.

            Requirements:
            - Each test must have a unique ID in format TC-XXX (where XXX is a number)
            - Do not duplicate these existing test IDs: {existingIds}
            - Tests should cover different aspects of the feature
            - Include clear, numbered steps
            - Include expected results for each test
            - Assign appropriate priority (high, medium, low)

            For each test, provide:
            - id: unique identifier
            - title: descriptive title
            - priority: high/medium/low
            - steps: numbered list of actions
            - expected_result: what should happen
            """;
    }

    private async Task<List<TestCase>> InteractiveReviewAsync(
        List<TestCase> tests,
        CancellationToken ct)
    {
        Console.WriteLine("\nReview generated tests:");
        Console.WriteLine("(y=accept, n=reject, e=edit, a=accept all, q=quit)\n");

        var accepted = new List<TestCase>();

        foreach (var test in tests)
        {
            ct.ThrowIfCancellationRequested();

            Console.WriteLine($"[{test.Id}] {test.Title}");
            Console.WriteLine($"  Priority: {test.Priority}");
            Console.WriteLine($"  Steps: {test.Steps.Count}");
            Console.Write("  Accept? [y/n/e/a/q]: ");

            var key = Console.ReadKey();
            Console.WriteLine();

            switch (key.KeyChar)
            {
                case 'y' or 'Y':
                    accepted.Add(test);
                    break;
                case 'n' or 'N':
                    // Skip
                    break;
                case 'e' or 'E':
                    // TODO: Implement editing
                    Console.WriteLine("  Editing not yet implemented, accepting as-is");
                    accepted.Add(test);
                    break;
                case 'a' or 'A':
                    accepted.Add(test);
                    accepted.AddRange(tests.SkipWhile(t => t != test).Skip(1));
                    return accepted;
                case 'q' or 'Q':
                    return accepted;
            }
        }

        return accepted;
    }
}
