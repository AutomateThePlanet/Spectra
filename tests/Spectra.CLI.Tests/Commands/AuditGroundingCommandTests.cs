using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Review;
using Spectra.CLI.Options;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 072 FR2 — audit-grounding command: per-test grounding state report.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class AuditGroundingCommandTests : IDisposable
{
    private readonly string _dir;

    public AuditGroundingCommandTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-audit-grounding-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private void WriteTestMd(string suite, string id, bool withGrounding, bool flagged = false)
    {
        var dir = Path.Combine(_dir, "test-cases", suite);
        Directory.CreateDirectory(dir);
        var grounding = withGrounding
            ? $"grounding:\n  verdict: {(flagged ? "partial" : "grounded")}\n  score: {(flagged ? "0.6" : "0.95")}\n  generator: claude-code-session\n  critic: claude-sonnet-4-6\n  verified_at: 2026-06-19T10:00:00Z\n  flagged_for_review: {flagged.ToString().ToLower()}\n"
            : "";
        File.WriteAllText(Path.Combine(dir, $"{id}.md"), $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            {grounding}---

            # Test {id}

            ## Steps

            1. Step one

            ## Expected Result

            All good
            """);
        File.WriteAllText(Path.Combine(dir, "_index.json"),
            $$"""{"suite":"{{suite}}","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"{{id}}","title":"Test {{id}}","priority":"medium","file":"{{suite}}/{{id}}.md"}]}""");
    }

    private void WriteVerdictFile(string id, string verdict, double score)
    {
        var verdictDir = Path.Combine(_dir, ".spectra", "verdicts");
        Directory.CreateDirectory(verdictDir);
        File.WriteAllText(Path.Combine(verdictDir, $"critic-verdict-{id}.json"),
            $$"""{"verdict":"{{verdict}}","score":{{score}},"summary":"Test {{id}} findings"}""");
    }

    private static async Task<int> RunAsync(params string[] args)
    {
        var root = new RootCommand();
        GlobalOptions.AddTo(root);
        root.AddCommand(new AuditGroundingCommand());
        return await root.InvokeAsync(args);
    }

    [Fact]
    public async Task EmptyVerdictDir_EmitsEmptySummary()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-300", withGrounding: false);
            // No verdict dir — should return success with empty list
            var exit = await RunAsync("audit-grounding", "--suite", "smoke", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<AuditGroundingResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(result);
            Assert.Equal(0, result!.Summary.Total);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task GroundedTest_ReportsActionNoneGroundingTrue()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-301", withGrounding: true);
            WriteVerdictFile("TC-301", "grounded", 0.95);

            var exit = await RunAsync("audit-grounding", "--suite", "smoke", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<AuditGroundingResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(result);
            Assert.Equal(1, result!.Summary.Total);
            Assert.Equal(1, result.Summary.GroundingWritten);
            Assert.Equal(0, result.Summary.PartialPendingRepair);

            var entry = Assert.Single(result.Tests);
            Assert.Equal("TC-301", entry.Id);
            Assert.True(entry.GroundingWritten);
            Assert.Equal("none", entry.ActionNeeded);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task PartialWithoutGrounding_ReportsActionRepair()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-302", withGrounding: false);
            WriteVerdictFile("TC-302", "partial", 0.65);

            var exit = await RunAsync("audit-grounding", "--suite", "smoke", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<AuditGroundingResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(result);
            var entry = Assert.Single(result!.Tests);
            Assert.Equal("TC-302", entry.Id);
            Assert.False(entry.GroundingWritten);
            Assert.Equal("repair", entry.ActionNeeded);
            Assert.Equal(1, result.Summary.PartialPendingRepair);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task FlaggedTest_ReportsActionReview()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-303", withGrounding: true, flagged: true);
            WriteVerdictFile("TC-303", "partial", 0.55);

            var exit = await RunAsync("audit-grounding", "--suite", "smoke", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<AuditGroundingResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(result);
            var entry = Assert.Single(result!.Tests);
            Assert.True(entry.FlaggedForReview);
            Assert.Equal("review", entry.ActionNeeded);
            Assert.Equal(1, result.Summary.FlaggedForReview);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task MixedSuite_SummaryCounts()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            // 1 grounded, 1 partial-pending-repair, 1 flagged
            // WriteTestMd also writes _index.json with a single entry each time — consolidate after all three.
            WriteTestMd("smoke", "TC-310", withGrounding: true);
            WriteTestMd("smoke", "TC-311", withGrounding: false);
            WriteTestMd("smoke", "TC-312", withGrounding: true, flagged: true);
            File.WriteAllText(
                Path.Combine(_dir, "test-cases", "smoke", "_index.json"),
                """{"suite":"smoke","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"TC-310","title":"Test TC-310","priority":"medium","file":"smoke/TC-310.md"},{"id":"TC-311","title":"Test TC-311","priority":"medium","file":"smoke/TC-311.md"},{"id":"TC-312","title":"Test TC-312","priority":"medium","file":"smoke/TC-312.md"}]}""");
            WriteVerdictFile("TC-310", "grounded", 0.95);
            WriteVerdictFile("TC-311", "partial", 0.60);
            WriteVerdictFile("TC-312", "partial", 0.50);

            var exit = await RunAsync("audit-grounding", "--suite", "smoke", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<AuditGroundingResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(result);
            Assert.Equal(3, result!.Summary.Total);
            Assert.Equal(2, result.Summary.GroundingWritten); // TC-310 + TC-312 (flagged still has grounding)
            Assert.Equal(1, result.Summary.PartialPendingRepair);
            Assert.Equal(1, result.Summary.FlaggedForReview);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task FileField_EmittedInJsonOutput()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-304", withGrounding: false);
            WriteVerdictFile("TC-304", "partial", 0.65);

            var exit = await RunAsync("audit-grounding", "--suite", "smoke", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<AuditGroundingResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(result);
            var entry = Assert.Single(result!.Tests);
            Assert.NotNull(entry.File);
            Assert.Contains("TC-304", entry.File);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task MissingSuite_Returns1()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var exit = await RunAsync("audit-grounding", "--suite", "nonexistent");
            Assert.Equal(1, exit);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task HumanOutput_PrintsSummaryLine()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-305", withGrounding: true);
            WriteVerdictFile("TC-305", "grounded", 0.90);

            var exit = await RunAsync("audit-grounding", "--suite", "smoke");
            Assert.Equal(0, exit);
            var output = captured.ToString();
            Assert.Contains("Summary:", output);
            Assert.Contains("total", output);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task SuiteRequired_MissingFails()
    {
        var exit = await RunAsync("audit-grounding");
        Assert.NotEqual(0, exit);
    }
}
