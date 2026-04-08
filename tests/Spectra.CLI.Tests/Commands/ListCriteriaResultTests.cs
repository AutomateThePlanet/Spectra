using System.Text.Json;
using Spectra.CLI.Output;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Commands;

public class ListCriteriaResultTests
{
    [Fact]
    public void Serialize_ListCriteriaResult_IncludesAllFields()
    {
        var result = new ListCriteriaResult
        {
            Command = "list-criteria",
            Status = "success",
            Total = 3,
            Covered = 2,
            CoveragePct = 66.7m,
            Criteria =
            [
                new ListCriterionEntry
                {
                    Id = "AC-CHECKOUT-001",
                    Text = "User must be able to add items to cart",
                    Rfc2119 = "MUST",
                    SourceType = "document",
                    SourceDoc = "docs/checkout.md",
                    Component = "checkout",
                    Priority = "high",
                    LinkedTests = ["TC-001", "TC-002"],
                    Covered = true
                },
                new ListCriterionEntry
                {
                    Id = "AC-CHECKOUT-002",
                    Text = "System should validate payment info",
                    SourceType = "document",
                    Priority = "medium",
                    LinkedTests = [],
                    Covered = false
                }
            ]
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("list-criteria", root.GetProperty("command").GetString());
        Assert.Equal("success", root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("total").GetInt32());
        Assert.Equal(2, root.GetProperty("covered").GetInt32());
        Assert.Equal(66.7m, root.GetProperty("coverage_pct").GetDecimal());

        var criteria = root.GetProperty("criteria");
        Assert.Equal(2, criteria.GetArrayLength());

        var first = criteria[0];
        Assert.Equal("AC-CHECKOUT-001", first.GetProperty("id").GetString());
        Assert.Equal("User must be able to add items to cart", first.GetProperty("text").GetString());
        Assert.Equal("MUST", first.GetProperty("rfc2119").GetString());
        Assert.Equal("document", first.GetProperty("source_type").GetString());
        Assert.Equal("docs/checkout.md", first.GetProperty("source_doc").GetString());
        Assert.Equal("checkout", first.GetProperty("component").GetString());
        Assert.Equal("high", first.GetProperty("priority").GetString());
        Assert.True(first.GetProperty("covered").GetBoolean());
        Assert.Equal(2, first.GetProperty("linked_tests").GetArrayLength());
    }

    [Fact]
    public void Serialize_ListCriterionEntry_OmitsNullOptionalFields()
    {
        var result = new ListCriteriaResult
        {
            Command = "list-criteria",
            Status = "success",
            Total = 1,
            Covered = 0,
            CoveragePct = 0m,
            Criteria =
            [
                new ListCriterionEntry
                {
                    Id = "AC-001",
                    Text = "Some criterion",
                    SourceType = "import",
                    Priority = "low",
                    Covered = false
                }
            ]
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement.GetProperty("criteria")[0];

        Assert.False(entry.TryGetProperty("rfc2119", out _));
        Assert.False(entry.TryGetProperty("source_doc", out _));
        Assert.False(entry.TryGetProperty("component", out _));
    }

    [Fact]
    public void Serialize_EmptyCriteriaList_SerializesCorrectly()
    {
        var result = new ListCriteriaResult
        {
            Command = "list-criteria",
            Status = "success",
            Total = 0,
            Covered = 0,
            CoveragePct = 0m,
            Criteria = []
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(0, doc.RootElement.GetProperty("criteria").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("total").GetInt32());
    }
}
