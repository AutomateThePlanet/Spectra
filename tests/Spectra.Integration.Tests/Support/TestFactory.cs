using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.Integration.Tests.Support;

/// <summary>Spec 052: small builders for hermetic test fixtures.</summary>
public static class TestFactory
{
    /// <summary>
    /// Builds a <see cref="TestCase"/> with a relative <c>FilePath</c> shaped like
    /// the production from-description/batch flows ("{suite}/{id}.md" or "{id}.md").
    /// </summary>
    public static TestCase Make(
        string id,
        string title,
        Priority priority,
        IReadOnlyList<string>? tags = null,
        string? component = null,
        IReadOnlyList<string>? criteria = null,
        string? filePath = null)
        => new()
        {
            Id = id,
            Title = title,
            Priority = priority,
            Tags = tags ?? [],
            Component = component,
            Criteria = criteria ?? [],
            Steps = ["Step 1", "Step 2"],
            ExpectedResult = "Expected result",
            FilePath = filePath ?? $"{id}.md",
        };

    /// <summary>Builds N synthetic <see cref="DocumentEntry"/> with realistic content size.</summary>
    public static IReadOnlyList<DocumentEntry> SyntheticCorpus(int count, int contentKb = 8)
        => Enumerable.Range(1, count)
            .Select(i => new DocumentEntry
            {
                Path = $"docs/features/doc-{i:D3}.md",
                Title = $"Feature {i}",
                SizeKb = contentKb,
                Headings = ["Overview", "Requirements", "Acceptance"],
                Preview = new string('x', 200),
            })
            .ToList();

    /// <summary>One extracted requirement for a document, used to populate the aggregate.</summary>
#pragma warning disable CS0618 // RequirementDefinition is the type ExtractCriteriaLoopAsync aggregates
    public static IReadOnlyList<RequirementDefinition> OneRequirement(string docPath)
        => [new RequirementDefinition { Id = "REQ-" + Math.Abs(docPath.GetHashCode()).ToString("X"), Title = "req for " + docPath }];
#pragma warning restore CS0618
}
