using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that reads the test index for existing tests.
/// </summary>
public sealed class ReadTestIndexTool
{
    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "read_test_index";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Reads the test index (_index.json) for a suite to get information about existing tests. " +
        "Use this to understand what tests already exist before generating new ones.";

    /// <summary>
    /// Executes the tool and returns the test index.
    /// </summary>
    public async Task<ReadTestIndexResult> ExecuteAsync(
        string suitePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suitePath);

        try
        {
            var indexPath = Path.Combine(suitePath, "_index.json");

            if (!File.Exists(indexPath))
            {
                return new ReadTestIndexResult
                {
                    Success = true,
                    SuitePath = suitePath,
                    IndexExists = false,
                    Tests = []
                };
            }

            var indexWriter = new IndexWriter();
            var index = await indexWriter.ReadAsync(indexPath, ct);

            if (index is null)
            {
                return new ReadTestIndexResult
                {
                    Success = true,
                    SuitePath = suitePath,
                    IndexExists = false,
                    Tests = []
                };
            }

            return new ReadTestIndexResult
            {
                Success = true,
                SuitePath = suitePath,
                IndexExists = true,
                SuiteName = index.Suite,
                Tests = index.Tests.Select(t => new TestIndexItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    Priority = t.Priority,
                    Tags = t.Tags,
                    SourceRefs = t.SourceRefs,
                    FilePath = t.File
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            return new ReadTestIndexResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the read_test_index tool.
/// </summary>
public sealed record ReadTestIndexResult
{
    public required bool Success { get; init; }
    public string? SuitePath { get; init; }
    public bool IndexExists { get; init; }
    public string? SuiteName { get; init; }
    public IReadOnlyList<TestIndexItem>? Tests { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Summary of a test in the index.
/// </summary>
public sealed record TestIndexItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Priority { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyList<string>? SourceRefs { get; init; }
    public string? FilePath { get; init; }
}
