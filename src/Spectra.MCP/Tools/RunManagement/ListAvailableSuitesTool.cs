using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// Lists available test suites for execution.
/// </summary>
public sealed class ListAvailableSuitesTool : IMcpTool
{
    private readonly Func<string, IEnumerable<SuiteInfo>> _suiteLoader;

    public string Description => "Lists all available test suites with their test counts";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            base_path = new { type = "string", description = "Base path to search for suites" }
        }
    };

    public ListAvailableSuitesTool(Func<string, IEnumerable<SuiteInfo>> suiteLoader)
    {
        _suiteLoader = suiteLoader;
    }

    public Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var basePath = parameters?.TryGetProperty("base_path", out var bp) == true ? bp.GetString() ?? "" : "";

        var suites = _suiteLoader(basePath).ToList();

        if (suites.Count == 0)
        {
            return Task.FromResult(JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NO_SUITES_FOUND",
                "No test suites found in the specified path")));
        }

        var suiteData = suites.Select(s => new
        {
            name = s.Name,
            test_count = s.TestCount,
            path = s.Path,
            stale = s.IsStale
        }).ToList();

        var data = new
        {
            suites = suiteData,
            total_tests = suites.Sum(s => s.TestCount),
            suite_count = suites.Count
        };

        return Task.FromResult(JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            nextExpectedAction: "start_execution_run")));
    }
}
