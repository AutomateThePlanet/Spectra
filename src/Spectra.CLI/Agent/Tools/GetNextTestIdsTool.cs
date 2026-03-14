using Spectra.Core.Index;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that allocates the next available test IDs.
/// </summary>
public sealed class GetNextTestIdsTool
{
    private readonly TestIdAllocator _allocator;

    public GetNextTestIdsTool(TestIdAllocator allocator)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "get_next_test_ids";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Allocates the next available test IDs. Use this to get unique IDs for new test cases. " +
        "Specify the count to get multiple IDs at once.";

    /// <summary>
    /// Executes the tool and returns allocated test IDs.
    /// </summary>
    public GetNextTestIdsResult Execute(int count = 1)
    {
        if (count < 1)
        {
            return new GetNextTestIdsResult
            {
                Success = false,
                Error = "Count must be at least 1"
            };
        }

        if (count > 100)
        {
            return new GetNextTestIdsResult
            {
                Success = false,
                Error = "Count cannot exceed 100"
            };
        }

        try
        {
            var ids = _allocator.AllocateMany(count);

            return new GetNextTestIdsResult
            {
                Success = true,
                Ids = ids.ToList()
            };
        }
        catch (Exception ex)
        {
            return new GetNextTestIdsResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the get_next_test_ids tool.
/// </summary>
public sealed record GetNextTestIdsResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<string>? Ids { get; init; }
    public string? Error { get; init; }
}
