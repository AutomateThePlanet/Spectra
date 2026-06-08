using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 062 — command-level contract for boundary-coverage gaps on <c>ai ingest-analysis</c>.
/// Verifies the JSON success output carries <c>boundary_gaps</c> (US1), that a malformed payload
/// fails loud with exit 6 (US2/FR-003), and that legacy output without the key still succeeds with
/// an empty array (backward-compatible). No model, no network.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class IngestAnalysisBoundaryGapTests : IDisposable
{
    private readonly string _dir;

    public IngestAnalysisBoundaryGapTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-ingest-bg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var config = new
        {
            source = new { local_dir = "docs/", include_patterns = new[] { "**/*.md" } },
            tests = new { dir = "test-cases/" },
            ai = new { providers = new[] { new { name = "test", model = "test-model", enabled = true, priority = 1 } } }
        };
        File.WriteAllText(Path.Combine(_dir, "spectra.config.json"), JsonSerializer.Serialize(config));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private async Task<(int Exit, string Out, string Err)> RunAsync(string analysisJson)
    {
        var from = Path.Combine(_dir, "analysis.json");
        File.WriteAllText(from, analysisJson);

        var original = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outW = new StringWriter();
        var errW = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            Console.SetOut(outW);
            Console.SetError(errW);

            var root = new RootCommand();
            GlobalOptions.AddTo(root);
            root.AddCommand(AiCommand.Create());
            var exit = await root.InvokeAsync(
                new[] { "ai", "ingest-analysis", "--suite", "signup", "--from", from, "--output-format", "json" });
            return (exit, outW.ToString(), errW.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task Ingest_WellFormedBoundaryGaps_Exit0_OutputCarriesGaps()
    {
        var json = """
            {"behaviors":[{"category":"boundary","title":"Username max length","source":"signup.md","technique":"BVA"}],
             "boundary_gaps":[{"field":"username","kind":"max-length","description":"21-char input untested","source":"docs/signup.md"}]}
            """;

        var (exit, stdout, _) = await RunAsync(json);

        Assert.Equal(0, exit);
        Assert.Contains("boundary_gaps", stdout);
        Assert.Contains("max-length", stdout);
        Assert.Contains("username", stdout);
    }

    [Fact]
    public async Task Ingest_NoBoundaryGapsKey_Exit0_EmptyArrayPresent()
    {
        var json = """
            {"behaviors":[{"category":"happy_path","title":"Login succeeds","source":"auth.md","technique":"UC"}]}
            """;

        var (exit, stdout, _) = await RunAsync(json);

        Assert.Equal(0, exit);
        Assert.Contains("\"boundary_gaps\":[]", stdout.Replace(" ", ""));
    }

    [Fact]
    public async Task Ingest_MalformedBoundaryGaps_NotArray_Exit6_WithSpecificError()
    {
        var json = """
            {"behaviors":[{"category":"x","title":"t","source":"s.md","technique":"EP"}],"boundary_gaps":{"field":"x"}}
            """;

        var (exit, _, stderr) = await RunAsync(json);

        Assert.Equal(6, exit);
        Assert.Contains("boundary_gaps must be a JSON array", stderr);
    }

    [Fact]
    public async Task Ingest_MalformedBoundaryGaps_MissingField_Exit6_NamingIndexAndField()
    {
        var json = """
            {"behaviors":[{"category":"x","title":"t","source":"s.md","technique":"EP"}],
             "boundary_gaps":[{"field":"username","description":"untested","source":"s.md"}]}
            """;

        var (exit, _, stderr) = await RunAsync(json);

        Assert.Equal(6, exit);
        Assert.Contains("boundary_gaps[0]", stderr);
        // The JSON encoder escapes the single quotes around the field name (' → '),
        // so assert on the bare field name rather than the quoted form.
        Assert.Contains("kind", stderr);
    }
}
