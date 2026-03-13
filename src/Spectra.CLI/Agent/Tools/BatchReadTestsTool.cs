using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that reads multiple test files in batch.
/// </summary>
public sealed class BatchReadTestsTool
{
    private readonly TestCaseParser _parser;

    public BatchReadTestsTool()
    {
        _parser = new TestCaseParser();
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "batch_read_tests";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Reads multiple test case files from a suite. Returns the parsed test cases " +
        "with their content for analysis and comparison.";

    /// <summary>
    /// Executes the tool and returns test cases.
    /// </summary>
    public async Task<BatchReadTestsResult> ExecuteAsync(
        string suitePath,
        IEnumerable<string>? testIds = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suitePath);

        try
        {
            if (!Directory.Exists(suitePath))
            {
                return new BatchReadTestsResult
                {
                    Success = false,
                    Error = $"Suite directory not found: {suitePath}"
                };
            }

            var testFiles = Directory.GetFiles(suitePath, "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList();

            // Filter by IDs if specified
            var idSet = testIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tests = new List<TestCase>();
            var errors = new List<ReadError>();

            foreach (var file in testFiles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var content = await File.ReadAllTextAsync(file, ct);
                    var parseResult = _parser.Parse(content, file);

                    if (!parseResult.IsSuccess || parseResult.Value is null)
                    {
                        errors.Add(new ReadError
                        {
                            FilePath = file,
                            Error = parseResult.Errors.FirstOrDefault()?.Message ?? "Parse error"
                        });
                        continue;
                    }

                    var test = parseResult.Value;

                    // Filter by ID if specified
                    if (idSet is not null && !idSet.Contains(test.Id))
                    {
                        continue;
                    }

                    tests.Add(test);
                }
                catch (Exception ex)
                {
                    errors.Add(new ReadError
                    {
                        FilePath = file,
                        Error = ex.Message
                    });
                }
            }

            return new BatchReadTestsResult
            {
                Success = true,
                SuitePath = suitePath,
                Tests = tests,
                Errors = errors,
                TotalFiles = testFiles.Count,
                SuccessfullyRead = tests.Count
            };
        }
        catch (Exception ex)
        {
            return new BatchReadTestsResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the batch_read_tests tool.
/// </summary>
public sealed record BatchReadTestsResult
{
    public required bool Success { get; init; }
    public string? SuitePath { get; init; }
    public IReadOnlyList<TestCase>? Tests { get; init; }
    public IReadOnlyList<ReadError>? Errors { get; init; }
    public int TotalFiles { get; init; }
    public int SuccessfullyRead { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Error reading a specific test file.
/// </summary>
public sealed record ReadError
{
    public required string FilePath { get; init; }
    public required string Error { get; init; }
}
