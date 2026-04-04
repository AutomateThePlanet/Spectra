using System.Text.Json;
using Spectra.CLI.Output;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Output;

public class JsonResultWriterTests
{
    [Fact]
    public void Serialize_CommandResult_UsesCamelCase()
    {
        var result = new ErrorResult
        {
            Command = "test",
            Status = "failed",
            Error = "something broke"
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("command", out _));
        Assert.True(doc.RootElement.TryGetProperty("status", out _));
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void Serialize_OmitsNullFields()
    {
        var result = new ErrorResult
        {
            Command = "generate",
            Status = "failed",
            Error = "missing config",
            MissingArguments = null
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("missing_arguments", out _));
    }

    [Fact]
    public void Serialize_IncludesNonNullArrays()
    {
        var result = new ErrorResult
        {
            Command = "generate",
            Status = "failed",
            Error = "Missing required arguments",
            MissingArguments = ["suite"]
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("missing_arguments", out var arr));
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal("suite", arr[0].GetString());
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var result = new ValidateResult
        {
            Command = "validate",
            Status = "errors_found",
            TotalFiles = 10,
            Valid = 8,
            Errors =
            [
                new ValidationError { File = "tests/TC-001.md", Line = 3, Message = "Missing priority" }
            ]
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json); // Should not throw

        Assert.Equal("validate", doc.RootElement.GetProperty("command").GetString());
        Assert.Equal(10, doc.RootElement.GetProperty("total_files").GetInt32());
        Assert.Equal(8, doc.RootElement.GetProperty("valid").GetInt32());
        Assert.Single(doc.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void Serialize_DashboardResult_IncludesAllFields()
    {
        var result = new DashboardResult
        {
            Command = "dashboard",
            Status = "completed",
            OutputPath = "./site",
            PagesGenerated = 4,
            SuitesIncluded = 6,
            TestsIncluded = 58,
            RunsIncluded = 24
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("./site", doc.RootElement.GetProperty("output_path").GetString());
        Assert.Equal(4, doc.RootElement.GetProperty("pages_generated").GetInt32());
        Assert.Equal(6, doc.RootElement.GetProperty("suites_included").GetInt32());
    }

    [Fact]
    public void Serialize_ListResult_WithSuites()
    {
        var result = new ListResult
        {
            Command = "list",
            Status = "completed",
            Suites =
            [
                new SuiteEntry { Name = "checkout", TestCount = 28, LastModified = "2026-04-03" },
                new SuiteEntry { Name = "auth", TestCount = 15 }
            ]
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        var suites = doc.RootElement.GetProperty("suites");
        Assert.Equal(2, suites.GetArrayLength());
        Assert.Equal("checkout", suites[0].GetProperty("name").GetString());
        Assert.Equal(28, suites[0].GetProperty("test_count").GetInt32());
        // auth suite has no last_modified - should be omitted
        Assert.False(suites[1].TryGetProperty("last_modified", out _));
    }

    [Fact]
    public void Serialize_IncludesTimestamp()
    {
        var result = new ErrorResult
        {
            Command = "test",
            Status = "failed",
            Error = "err"
        };

        var json = JsonResultWriter.Serialize(result);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("timestamp", out var ts));
        // Should be parseable as DateTimeOffset
        Assert.True(DateTimeOffset.TryParse(ts.GetString(), out _));
    }
}
