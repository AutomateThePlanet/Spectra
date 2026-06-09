#pragma warning disable CS0618 // RequirementDefinition is obsolete — the docs-index path still produces it.
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Extraction;
using Spectra.CLI.Commands.Docs;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 047 test plan rows 13–14. Exercises <see cref="DocsIndexHandler.ExtractCriteriaLoopAsync"/>
/// which is the per-document extraction loop the <c>docs index</c> path uses
/// after Spec 047's removal of the corpus-wide 60-second deadline.
/// </summary>
public class DocsIndexCriteriaTimeoutTests
{
    private static DocumentEntry Doc(string path) => new()
    {
        Path = path,
        Title = path,
        SizeKb = 1,
        Headings = Array.Empty<string>(),
        Preview = string.Empty,
    };

    private static RequirementDefinition Req(string title) => new()
    {
        Title = title,
        Priority = "medium",
    };

    private static IReadOnlyList<RequirementDefinition> Empty =>
        Array.Empty<RequirementDefinition>();

    [Fact]
    public async Task DocsIndex_LargeCorpus_NoCorpusDeadlineAbort()
    {
        // 30 docs, each "extraction" returns instantly with 1 requirement. Cumulative
        // time on the legacy 60s corpus deadline would not be an issue here in test
        // terms, but the test's point is that the loop processes every document
        // individually rather than wrapping them in a single deadline.
        var docs = Enumerable.Range(1, 30).Select(i => Doc($"docs/req-{i:D2}.md")).ToList();
        var perDocCalls = new List<string>();

        var result = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            documents: docs,
            existing: Empty,
            extractPerDoc: (doc, _) =>
            {
                perDocCalls.Add(doc.Path);
                return Task.FromResult(new RequirementsExtractionResult(
                    ExtractionOutcome.Extracted, new[] { Req($"Requirement from {doc.Path}") }));
            },
            perDocDeadline: TimeSpan.FromSeconds(5),
            onSlowDoc: _ => Assert.Fail("No doc should time out in this test."),
            onDocFailure: (_, _) => Assert.Fail("No doc should fail in this test."),
            ct: CancellationToken.None);

        Assert.Equal(30, perDocCalls.Count);                            // every doc visited
        Assert.Equal(30, result.Aggregated.Count);                      // every doc contributed a requirement
        Assert.Empty(result.TimedOutDocuments);
        Assert.Empty(result.FailedDocuments);
    }

    [Fact]
    public async Task DocsIndex_SingleSlowDoc_TimesOutThatDocOnly()
    {
        // Slow doc hangs past the per-doc deadline; fast docs return immediately.
        // The loop must skip the slow doc and continue extracting the others.
        var docs = new[]
        {
            Doc("docs/fast-a.md"),
            Doc("docs/slow.md"),
            Doc("docs/fast-b.md"),
        };

        var timedOutPaths = new List<string>();

        var result = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            documents: docs,
            existing: Empty,
            extractPerDoc: async (doc, token) =>
            {
                if (doc.Path == "docs/slow.md")
                {
                    // Hang past the 200ms deadline; respect cancellation if observed.
                    try { await Task.Delay(TimeSpan.FromSeconds(5), token); }
                    catch (OperationCanceledException) { }
                    return new RequirementsExtractionResult(
                        ExtractionOutcome.Extracted, new[] { Req("should not be observed") });
                }
                return new RequirementsExtractionResult(
                    ExtractionOutcome.Extracted, new[] { Req($"Requirement from {doc.Path}") });
            },
            perDocDeadline: TimeSpan.FromMilliseconds(200),
            onSlowDoc: path => timedOutPaths.Add(path),
            onDocFailure: (_, _) => Assert.Fail("No exception expected in this test."),
            ct: CancellationToken.None);

        Assert.Equal(2, result.Aggregated.Count);                       // fast-a + fast-b extracted
        Assert.Contains(result.Aggregated, r => r.Title == "Requirement from docs/fast-a.md");
        Assert.Contains(result.Aggregated, r => r.Title == "Requirement from docs/fast-b.md");
        Assert.Single(result.TimedOutDocuments, "docs/slow.md");
        Assert.Single(timedOutPaths, "docs/slow.md");
    }

    [Fact]
    public async Task DocsIndex_DocException_DoesNotAbortOtherDocs()
    {
        // Belt-and-braces for FR-008: an exception in one doc must not kill the loop.
        var docs = new[]
        {
            Doc("docs/a.md"),
            Doc("docs/throws.md"),
            Doc("docs/c.md"),
        };
        var failures = new List<string>();

        var result = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            documents: docs,
            existing: Empty,
            extractPerDoc: (doc, _) =>
            {
                if (doc.Path == "docs/throws.md")
                    throw new InvalidOperationException("simulated provider error");
                return Task.FromResult(new RequirementsExtractionResult(
                    ExtractionOutcome.Extracted, new[] { Req($"R from {doc.Path}") }));
            },
            perDocDeadline: TimeSpan.FromSeconds(5),
            onSlowDoc: _ => Assert.Fail("No doc should time out in this test."),
            onDocFailure: (path, _) => failures.Add(path),
            ct: CancellationToken.None);

        Assert.Equal(2, result.Aggregated.Count);
        Assert.Single(result.FailedDocuments, "docs/throws.md");
        Assert.Single(failures, "docs/throws.md");
    }
}
