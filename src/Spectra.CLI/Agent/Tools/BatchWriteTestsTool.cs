using Spectra.CLI.IO;
using Spectra.Core.Models;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that writes multiple test files in batch.
/// </summary>
public sealed class BatchWriteTestsTool
{
    private readonly TestFileWriter _writer;

    public BatchWriteTestsTool(TestFileWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "batch_write_tests";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Writes multiple test case files to disk. Each test is written to its own markdown file. " +
        "Returns the paths of successfully written files.";

    /// <summary>
    /// Executes the tool and writes test files.
    /// </summary>
    public async Task<BatchWriteTestsResult> ExecuteAsync(
        string suitePath,
        IReadOnlyList<TestCase> tests,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suitePath);
        ArgumentNullException.ThrowIfNull(tests);

        try
        {
            if (!Directory.Exists(suitePath))
            {
                Directory.CreateDirectory(suitePath);
            }

            var writtenFiles = new List<string>();
            var errors = new List<WriteError>();

            foreach (var test in tests)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var path = Path.Combine(suitePath, $"{test.Id}.md");

                    if (!dryRun)
                    {
                        await _writer.WriteAsync(path, test, ct);
                    }

                    writtenFiles.Add(path);
                }
                catch (Exception ex)
                {
                    errors.Add(new WriteError
                    {
                        TestId = test.Id,
                        Error = ex.Message
                    });
                }
            }

            return new BatchWriteTestsResult
            {
                Success = errors.Count == 0,
                SuitePath = suitePath,
                WrittenFiles = writtenFiles,
                Errors = errors,
                TotalRequested = tests.Count,
                TotalWritten = writtenFiles.Count,
                DryRun = dryRun
            };
        }
        catch (Exception ex)
        {
            return new BatchWriteTestsResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the batch_write_tests tool.
/// </summary>
public sealed record BatchWriteTestsResult
{
    public required bool Success { get; init; }
    public string? SuitePath { get; init; }
    public IReadOnlyList<string>? WrittenFiles { get; init; }
    public IReadOnlyList<WriteError>? Errors { get; init; }
    public int TotalRequested { get; init; }
    public int TotalWritten { get; init; }
    public bool DryRun { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Error writing a specific test.
/// </summary>
public sealed record WriteError
{
    public required string TestId { get; init; }
    public required string Error { get; init; }
}
