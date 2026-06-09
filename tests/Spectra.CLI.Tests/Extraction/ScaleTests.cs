using System.Diagnostics;
using Spectra.CLI.Extraction;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Commands.Docs;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Tests.Extraction;

/// <summary>
/// Spec 052 Part C — the large-corpus scale guard for Spec 047. The defect that
/// triggered 047 only manifested at scale: a single corpus-wide deadline silently
/// killed extraction for the whole document set. These tests drive
/// <see cref="DocsIndexHandler.ExtractCriteriaLoopAsync"/> over a synthetic corpus
/// with per-call latency injected and prove the deadline is enforced PER DOCUMENT,
/// not corpus-wide — i.e. a slow document times out alone while the rest succeed.
///
/// Tagged <c>[Trait("Category","Scale")]</c> so fast-feedback runs can exclude it
/// (<c>dotnet test --filter "Category!=Scale"</c>) while full CI runs everything.
///
/// Latency/deadline are small constants so the property is proven without literal
/// multi-second provider latency; bump <see cref="SlowLatency"/>/<see cref="Deadline"/>
/// to simulate heavier providers.
/// </summary>
public sealed class ScaleTests
{
    private const int CorpusSize = 30;
    private static readonly TimeSpan Deadline = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan FastLatency = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan SlowLatency = TimeSpan.FromMilliseconds(800); // exceeds Deadline

#pragma warning disable CS0618 // ExtractCriteriaLoopAsync aggregates the (obsolete) RequirementDefinition

    [Fact]
    [Trait("Category", "Scale")]
    public async Task LargeCorpus_PerDocumentDeadline_NotCorpusWide()
    {
        var docs = SyntheticCorpus(CorpusSize);
        // Make a handful of documents slow (they should time out INDIVIDUALLY).
        var slow = new HashSet<string> { docs[5].Path, docs[15].Path, docs[25].Path };

        var loop = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            docs,
            existing: [],
            extractPerDoc: async (doc, ct) =>
            {
                await Task.Delay(slow.Contains(doc.Path) ? SlowLatency : FastLatency, ct);
                return OneRequirement(doc.Path);
            },
            perDocDeadline: Deadline,
            onSlowDoc: null,
            onDocFailure: null,
            ct: CancellationToken.None);

        // Partial success: the fast majority extracted; the corpus did NOT abort wholesale.
        Assert.Equal(docs.Count - slow.Count, loop.Aggregated.Count);
        Assert.True(loop.Aggregated.Count > 0);

        // Exactly the designated slow docs timed out — proving per-document deadlines.
        Assert.Equal(slow.OrderBy(p => p), loop.TimedOutDocuments.OrderBy(p => p));
        Assert.Empty(loop.FailedDocuments);
    }

    [Fact]
    [Trait("Category", "Scale")]
    public async Task LargeCorpus_SlowDocument_DoesNotAbortRemaining()
    {
        var docs = SyntheticCorpus(CorpusSize);
        // The FIRST document is slow. Under a single shared corpus budget it would
        // consume the whole budget and starve the rest. Per-document, it times out
        // alone and every later document still extracts.
        var slowDoc = docs[0].Path;

        var sw = Stopwatch.StartNew();
        var loop = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            docs,
            existing: [],
            extractPerDoc: async (doc, ct) =>
            {
                await Task.Delay(doc.Path == slowDoc ? SlowLatency : FastLatency, ct);
                return OneRequirement(doc.Path);
            },
            perDocDeadline: Deadline,
            onSlowDoc: null,
            onDocFailure: null,
            ct: CancellationToken.None);
        sw.Stop();

        Assert.Equal(new[] { slowDoc }, loop.TimedOutDocuments.ToArray());
        Assert.Equal(docs.Count - 1, loop.Aggregated.Count); // every doc AFTER the slow one still ran
    }

#pragma warning restore CS0618

    private static IReadOnlyList<DocumentEntry> SyntheticCorpus(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new DocumentEntry
            {
                Path = $"docs/features/doc-{i:D3}.md",
                Title = $"Feature {i}",
                SizeKb = 8,
                Headings = ["Overview", "Requirements"],
                Preview = new string('x', 200),
            })
            .ToList();

#pragma warning disable CS0618
    private static RequirementsExtractionResult OneRequirement(string docPath) =>
        new(ExtractionOutcome.Extracted,
            [new RequirementDefinition { Id = "REQ-" + Math.Abs(docPath.GetHashCode()).ToString("X"), Title = docPath }]);
#pragma warning restore CS0618
}
