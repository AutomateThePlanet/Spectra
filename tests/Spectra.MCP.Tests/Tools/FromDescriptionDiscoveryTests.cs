using System.Text.Json;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Tools;

/// <summary>
/// Spec 049 — From-Description discovery side. Stages disk state via
/// <see cref="IndexGenerator"/> + <see cref="IndexWriter"/> (the same
/// primitives the from-description CLI flow now writes through, just without
/// the upstream <c>TestFileWriter</c> step which is irrelevant to discovery)
/// and asserts that MCP discovery tools return the newly-added test through
/// their normal index-loader path.
/// </summary>
public sealed class FromDescriptionDiscoveryTests : IDisposable
{
    private readonly string _root;
    private readonly string _testsPath;
    private readonly IndexGenerator _generator = new();
    private readonly IndexWriter _writer = new();

    public FromDescriptionDiscoveryTests()
    {
        _root = Directory.CreateTempSubdirectory("spectra-mcp-fromdesc-").FullName;
        _testsPath = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsPath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task FindTestCases_HighPriority_ReturnsFromDescriptionTest()
    {
        const string suite = "checkout";

        var pre = new[]
        {
            CreateTest("TC-101", "Add item to cart", Priority.High, tags: ["smoke"], component: "checkout"),
            CreateTest("TC-102", "Apply discount", Priority.Medium, tags: ["regression"], component: "checkout"),
        };
        await WriteSuiteAsync(suite, pre);

        // From-description add — single new high-priority test joins the existing index.
        var fromDesc = CreateTest("TC-201", "Guest checkout shows shipping estimate", Priority.High,
            tags: ["smoke", "checkout"], component: "checkout");
        await WriteSuiteAsync(suite, pre.Append(fromDesc).ToArray());

        var findTool = BuildFindTool();
        var result = await findTool.ExecuteAsync(
            JsonDocument.Parse("""{"priorities":["high"]}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var data = response.GetProperty("data");
        Assert.Equal(2, data.GetProperty("matched").GetInt32());
        var ids = data.GetProperty("tests").EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()).ToHashSet();
        Assert.Contains("TC-101", ids);
        Assert.Contains("TC-201", ids); // The from-description test must appear.
    }

    [Fact]
    public async Task SavedSelection_Smoke_IncludesFromDescriptionHighTest()
    {
        const string suite = "auth";

        var pre = new[]
        {
            CreateTest("TC-001", "Existing smoke", Priority.High, tags: ["smoke"], component: "auth"),
        };
        await WriteSuiteAsync(suite, pre);

        var fromDesc = CreateTest("TC-050", "Session expiry redirect", Priority.High,
            tags: ["smoke", "auth"], component: "auth");
        await WriteSuiteAsync(suite, pre.Append(fromDesc).ToArray());

        var listTool = BuildListSavedSelectionsTool();
        var result = await listTool.ExecuteAsync(parameters: null);
        var response = JsonDocument.Parse(result).RootElement;

        var selections = response.GetProperty("data").GetProperty("selections");
        var smoke = selections.EnumerateArray()
            .FirstOrDefault(s => s.GetProperty("name").GetString() == "smoke");
        Assert.NotEqual(default, smoke);
        // The smoke selection (priorities=["high"] + tags=["smoke"]) must include both
        // the pre-existing high+smoke test AND the new from-description high+smoke test.
        Assert.Equal(2, smoke.GetProperty("estimated_test_count").GetInt32());
    }

    private async Task WriteSuiteAsync(string suite, IReadOnlyList<TestCase> tests)
    {
        var suiteDir = Path.Combine(_testsPath, suite);
        Directory.CreateDirectory(suiteDir);
        var index = _generator.Generate(suite, tests);
        await _writer.WriteAsync(Path.Combine(suiteDir, "_index.json"), index);
    }

    private FindTestCasesTool BuildFindTool()
        => new(SuiteListLoader, IndexLoader);

    private ListSavedSelectionsTool BuildListSavedSelectionsTool()
    {
        var selections = new Dictionary<string, SavedSelectionConfig>
        {
            ["smoke"] = new() { Description = "Smoke tests", Priorities = ["high"], Tags = ["smoke"] },
        };
        return new ListSavedSelectionsTool(() => selections, SuiteListLoader, IndexLoader);
    }

    private IEnumerable<string> SuiteListLoader()
    {
        if (!Directory.Exists(_testsPath)) yield break;
        foreach (var dir in Directory.GetDirectories(_testsPath))
        {
            var indexPath = Path.Combine(dir, "_index.json");
            if (File.Exists(indexPath))
                yield return Path.GetFileName(dir);
        }
    }

    private IEnumerable<TestIndexEntry> IndexLoader(string suite)
    {
        var indexPath = Path.Combine(_testsPath, suite, "_index.json");
        if (!File.Exists(indexPath)) return [];
        var index = _writer.ReadAsync(indexPath).GetAwaiter().GetResult();
        return index?.Tests ?? [];
    }

    private static TestCase CreateTest(
        string id,
        string title,
        Priority priority,
        IReadOnlyList<string>? tags = null,
        string? component = null)
        => new()
        {
            Id = id,
            Title = title,
            Priority = priority,
            Tags = tags ?? [],
            Component = component,
            Steps = ["A step"],
            ExpectedResult = "An outcome",
            FilePath = $"{id}.md",
        };
}
