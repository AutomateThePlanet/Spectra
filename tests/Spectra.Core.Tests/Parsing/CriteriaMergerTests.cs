using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class CriteriaMergerTests
{
    private readonly CriteriaMerger _merger = new();

    [Fact]
    public void Merge_ByIdMatch_UpdatesExisting()
    {
        var existing = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-001", Text = "Old text", Priority = "low" }
        };

        var incoming = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-001", Text = "Updated text", Priority = "high" }
        };

        var result = _merger.Merge(existing, incoming);

        Assert.Single(result.Criteria);
        Assert.Equal("REQ-001", result.Criteria[0].Id);
        Assert.Equal("Updated text", result.Criteria[0].Text);
        Assert.Equal("high", result.Criteria[0].Priority);
        Assert.Equal(1, result.MergedCount);
        Assert.Equal(0, result.NewCount);
    }

    [Fact]
    public void Merge_BySourceMatch_UpdatesExisting()
    {
        var existing = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-010", Text = "Original", Source = "JIRA-500" }
        };

        var incoming = new List<AcceptanceCriterion>
        {
            new() { Text = "From Jira updated", Source = "JIRA-500" }
        };

        var result = _merger.Merge(existing, incoming);

        Assert.Single(result.Criteria);
        // ID is preserved from existing
        Assert.Equal("REQ-010", result.Criteria[0].Id);
        Assert.Equal("From Jira updated", result.Criteria[0].Text);
        Assert.Equal(1, result.MergedCount);
        Assert.Equal(0, result.NewCount);
    }

    [Fact]
    public void Merge_NoMatch_AppendsNew()
    {
        var existing = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-001", Text = "Existing" }
        };

        var incoming = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-002", Text = "Brand new criterion" }
        };

        var result = _merger.Merge(existing, incoming);

        Assert.Equal(2, result.Criteria.Count);
        Assert.Equal("REQ-001", result.Criteria[0].Id);
        Assert.Equal("REQ-002", result.Criteria[1].Id);
        Assert.Equal(0, result.MergedCount);
        Assert.Equal(1, result.NewCount);
    }

    [Fact]
    public void Replace_ClearsAndInserts()
    {
        var existing = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-001", Text = "Old one" },
            new() { Id = "REQ-002", Text = "Old two" }
        };

        var incoming = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-100", Text = "Replacement" }
        };

        var result = _merger.Replace(existing, incoming);

        Assert.Single(result.Criteria);
        Assert.Equal("REQ-100", result.Criteria[0].Id);
        Assert.Equal("Replacement", result.Criteria[0].Text);
        Assert.Equal(0, result.MergedCount);
        Assert.Equal(1, result.NewCount);
        Assert.Equal(2, result.ReplacedCount);
    }

    [Fact]
    public void Merge_EmptyExisting_AddsAll()
    {
        var existing = new List<AcceptanceCriterion>();

        var incoming = new List<AcceptanceCriterion>
        {
            new() { Id = "REQ-001", Text = "First" },
            new() { Id = "REQ-002", Text = "Second" }
        };

        var result = _merger.Merge(existing, incoming);

        Assert.Equal(2, result.Criteria.Count);
        Assert.Equal(0, result.MergedCount);
        Assert.Equal(2, result.NewCount);
    }
}
