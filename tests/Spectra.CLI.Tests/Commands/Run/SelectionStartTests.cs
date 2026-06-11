using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Commands.Run;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Models.Config;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run;

/// <summary>
/// Spec 070 — preserves the retired MCP SmartSelection behavior on the sole surface: starting a run by a
/// saved selection name applies that selection's criteria (a filtered subset), not the whole corpus.
/// Covers the <c>--selection</c> resolution that <c>RunHandler.StartAsync</c> performs via
/// <c>RunServices.SelectionsLoader</c> reading <c>spectra.config.json</c>.
/// </summary>
public class SelectionStartTests
{
    private static RunHandler Handler(string root) => new(VerbosityLevel.Quiet, OutputFormat.Json, root);

    /// <summary>Writes a complete spectra.config.json (required blocks from Default) plus the given selections.</summary>
    private static void WriteConfig(string root, JsonObject selections)
    {
        var cfg = JsonNode.Parse(JsonSerializer.Serialize(SpectraConfig.Default))!.AsObject();
        cfg["selections"] = selections;
        File.WriteAllText(Path.Combine(root, "spectra.config.json"), cfg.ToJsonString());
    }

    [Fact]
    public async Task StartBySelection_AppliesSelectionFilters_NotWholeCorpus()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("checkout",
            ("TC-001", "High A", "high", null),
            ("TC-002", "Medium B", "medium", null),
            ("TC-003", "High C", "high", null),
            ("TC-004", "Low D", "low", null));

        // A saved selection that filters to high priority only.
        WriteConfig(ws.Root, new JsonObject
        {
            ["high-only"] = new JsonObject { ["priorities"] = new JsonArray("high") }
        });

        Assert.Equal(ExitCodes.Success,
            await Handler(ws.Root).StartAsync(null, null, null, null, null, "high-only", null));

        // The run enqueued exactly the two high-priority tests — not all four.
        await using var db = new ExecutionDb(Path.Combine(ws.Root, ".execution"));
        var engine = new ExecutionEngine(new RunRepository(db), new ResultRepository(db),
            new QueueSnapshotRepository(db), new UserIdentityResolver(), new McpConfig { BasePath = ws.Root });
        var active = await engine.GetActiveRunAsync();
        Assert.NotNull(active);
        var counts = await engine.GetStatusCountsAsync(active!.RunId);
        Assert.Equal(2, counts.Values.Sum());
    }

    [Fact]
    public async Task StartByUnknownSelection_FailsNotFound()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("checkout", ("TC-001", "A", "high", null));
        WriteConfig(ws.Root, new JsonObject());

        var code = await Handler(ws.Root).StartAsync(null, null, null, null, null, "nope", null);
        Assert.NotEqual(ExitCodes.Success, code);
    }
}
