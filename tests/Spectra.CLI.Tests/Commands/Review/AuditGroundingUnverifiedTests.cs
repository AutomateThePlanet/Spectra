using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands.Review;

/// <summary>
/// Spec 090 T14 (FR-A7): <c>audit-grounding</c> also lists indexed tests without a verdict file, judged by their grounding
/// block (verdict files are gitignored, so a fresh clone has blocks but no verdicts): no block → <c>none</c> / <c>critic</c>;
/// a critic block → its verdict; Manual → <c>manual</c>. Entries for tests with a verdict file are unchanged.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class AuditGroundingUnverifiedTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public AuditGroundingUnverifiedTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        _ws.WriteTest("storage", "TC-102", [Quotas]);
        _ws.InitBaselineAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    private async Task<JsonElement> AuditAsync()
    {
        var (exit, output, error) = await _ws.AuditGroundingJsonAsync();
        Assert.True(exit == 0, error);
        return BaselineTestWorkspace.Json(output);
    }

    private static JsonElement Entry(JsonElement audit, string id) =>
        audit.GetProperty("tests").EnumerateArray().Single(t => t.GetProperty("id").GetString() == id);

    [Fact]
    public async Task AnEditedTest_IsUnverified_CriticNeeded()
    {
        var (compileExit, _, compileError) = await _ws.CompileCriticAsync("storage", "TC-101");
        Assert.True(compileExit == 0, compileError);
        _ws.WriteVerdict("TC-101", "grounded");
        await _ws.WriteCriticGroundingAsync("storage", "TC-101");
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101") with { ExpectedResult = "Edited" });
        var (exit, _, error) = await _ws.IngestAllAsync();
        Assert.True(exit == 0, error);

        var audit = await AuditAsync();

        var entry = Entry(audit, "TC-101");
        Assert.Equal("none", entry.GetProperty("verdict").GetString());
        Assert.False(entry.GetProperty("grounding_written").GetBoolean());
        Assert.Equal("critic", entry.GetProperty("action_needed").GetString());
        Assert.Equal(2, audit.GetProperty("summary").GetProperty("unverified").GetInt32()); // TC-101 and TC-102
        Assert.Equal(2, audit.GetProperty("summary").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task AFreshCloneLayout_UsesTheBlocksVerdict_NeverNone()
    {
        await _ws.WriteCriticGroundingAsync("storage", "TC-101");
        await _ws.WriteCriticGroundingAsync("storage", "TC-102", VerificationVerdict.Partial, flagged: true);

        var audit = await AuditAsync();

        var grounded = Entry(audit, "TC-101");
        Assert.Equal("grounded", grounded.GetProperty("verdict").GetString());
        Assert.Equal(0.95, grounded.GetProperty("score").GetDouble());
        Assert.True(grounded.GetProperty("grounding_written").GetBoolean());
        Assert.Equal("none", grounded.GetProperty("action_needed").GetString());
        var flagged = Entry(audit, "TC-102");
        Assert.Equal("partial", flagged.GetProperty("verdict").GetString());
        Assert.True(flagged.GetProperty("flagged_for_review").GetBoolean());
        Assert.Equal("review", flagged.GetProperty("action_needed").GetString());
        Assert.Equal(0, audit.GetProperty("summary").GetProperty("unverified").GetInt32());
    }

    [Fact]
    public async Task AManualTest_IsManual_NothingToDo()
    {
        await _ws.WriteManualGroundingAsync("storage", "TC-101");

        var entry = Entry(await AuditAsync(), "TC-101");

        Assert.Equal("manual", entry.GetProperty("verdict").GetString());
        Assert.True(entry.GetProperty("grounding_written").GetBoolean());
        Assert.Equal("none", entry.GetProperty("action_needed").GetString());
    }

    [Fact]
    public async Task APre090Workspace_WithVerdictFilesForEveryTest_IsReportedAsBefore()
    {
        // Every test has a verdict file and a block: the entries are exactly what fc82b7a reported (verdict and
        // score from the verdict file), and the only addition is the unverified count.
        foreach (var id in new[] { "TC-101", "TC-102" })
        {
            _ws.WriteVerdict(id, "grounded", score: 0.88);
            await _ws.WriteCriticGroundingAsync("storage", id);
        }

        var audit = await AuditAsync();

        var tests = audit.GetProperty("tests").EnumerateArray().ToList();
        Assert.Equal(["TC-101", "TC-102"], tests.Select(t => t.GetProperty("id").GetString()));
        Assert.All(tests, t =>
        {
            Assert.Equal("grounded", t.GetProperty("verdict").GetString());
            Assert.Equal(0.88, t.GetProperty("score").GetDouble());
            Assert.True(t.GetProperty("grounding_written").GetBoolean());
            Assert.False(t.GetProperty("flagged_for_review").GetBoolean());
            Assert.Equal("none", t.GetProperty("action_needed").GetString());
            Assert.Equal(["id", "verdict", "score", "grounding_written", "flagged_for_review", "action_needed", "file"],
                t.EnumerateObject().Select(p => p.Name));
        });
        var summary = audit.GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("total").GetInt32());
        Assert.Equal(2, summary.GetProperty("grounding_written").GetInt32());
        Assert.Equal(0, summary.GetProperty("unverified").GetInt32());
    }
}
