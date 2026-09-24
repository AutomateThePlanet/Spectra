using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 078 (US4, FR-006/FR-007, SC-006): orphan marking changes only <c>status</c>, <c>orphaned_reason</c> and
/// <c>orphaned_date</c>, and keeps the date of a test that was already orphaned.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class UpdateHandlerOrphanMarkingTests : IDisposable
{
    private const string Suite = "smoke";
    private readonly UpdateWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private static string WithoutLifecycle(string markdown) =>
        string.Join('\n', markdown.Split('\n').Where(l =>
            !l.StartsWith("status:") && !l.StartsWith("orphaned_reason:") && !l.StartsWith("orphaned_date:")));

    private async Task<string> MarkOrphanedAsync(string markdown)
    {
        // The fixture's only ref (docs/checkout.md#Payment) does not exist in this workspace → ORPHANED.
        var path = _ws.WriteTest(Suite, FullFieldTestCase.Id, markdown);

        var (exit, output) = await _ws.RunAsync("ai", "update", Suite, "--no-interaction", "--no-review");

        Assert.True(exit == 0, output);
        return File.ReadAllText(path);
    }

    [Fact]
    public async Task CanonicalFile_DiffIsExactlyTheThreeOrphanLines()
    {
        var before = WithoutLifecycle(FullFieldTestCase.CanonicalMarkdown);

        var after = await MarkOrphanedAsync(before);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var added = after.Split('\n').Except(before.Split('\n')).ToList();
        var removed = before.Split('\n').Except(after.Split('\n')).ToList();
        Assert.Empty(removed);
        Assert.Equal(3, added.Count);
        Assert.Contains("status: orphaned", added);
        Assert.Contains(added, l => l.StartsWith("orphaned_reason: \""));
        Assert.Contains($"orphaned_date: {today}", added);
        Assert.Equal(WithoutLifecycle(after), before);
    }

    [Fact]
    public async Task AlreadyOrphaned_KeepsOriginalDate()
    {
        var after = await MarkOrphanedAsync(FullFieldTestCase.Markdown);

        Assert.Contains("orphaned_date: 2026-01-15", after);
        Assert.Contains("status: orphaned", after);
    }

    [Fact]
    public async Task FullFieldFile_KeepsEverythingElseByteForByte()
    {
        var before = FullFieldTestCase.Markdown;

        var after = await MarkOrphanedAsync(before);

        foreach (var marker in new[]
                 {
                     "owner: qa-team\n", "x_ticket: {id: 42, system: jira}\n",
                     "## Notes\n\nHand-written notes the parser ignores.", "   - use card 4000 0000 0000 0002\n",
                     "criteria:\n  - AC-CHECKOUT-001\n  - AC-CHECKOUT-002\n",
                     "scenario_from_doc: \"Card is declined by the gateway\"\n",
                     "description: \"Verifies payment: card declined path\"\n",
                     "automated_by:\n  - tests/e2e/checkout.spec.ts\n", "requirements:\n  - REQ-12\n",
                     "bugs:\n  - BUG-77\n", "  note: \"Checked against gateway spec\"\n", "  verified_at: 2026-02-03T04:05:06.789Z\n"
                 })
        {
            Assert.Contains(marker, after);
        }

        // Only the reason can differ (classifier wording); every other original line survives.
        var lost = before.Split('\n').Except(after.Split('\n')).ToList();
        Assert.All(lost, l => Assert.StartsWith("orphaned_reason:", l));
    }
}
