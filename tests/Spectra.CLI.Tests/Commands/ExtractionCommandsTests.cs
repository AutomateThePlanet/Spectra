using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 054 — exit-code contract for the model-free extraction commands.
/// US2 (compile-extraction-prompt empty-source short-circuit + refuse) and
/// US3 (ingest-criteria Extracted→0, EmptyResponse→5, ParseFailure→6).
/// </summary>
[Collection("WorkingDirectory")]
public sealed class ExtractionCommandsTests : IDisposable
{
    private readonly string _dir;

    public ExtractionCommandsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-extract-cmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private static async Task<int> RunAsync(params string[] args)
    {
        var root = new RootCommand();
        root.AddCommand(AiCommand.Create());
        return await root.InvokeAsync(args);
    }

    private void WriteConfig() =>
        File.WriteAllText(
            Path.Combine(_dir, "spectra.config.json"),
            JsonSerializer.Serialize(SpectraConfig.Default));

    private string WriteFile(string relative, string content)
    {
        var full = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return relative.Replace('\\', '/');
    }

    private const string GoodJson =
        "[{\"text\":\"System MUST validate IBAN\",\"rfc2119\":\"MUST\",\"priority\":\"high\"}]";

    // ---------- compile-extraction-prompt ----------

    [Fact]
    public async Task CompileExtractionPrompt_MissingDoc_Refuses_Exit4()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var exit = await RunAsync("ai", "compile-extraction-prompt");
            Assert.Equal(4, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileExtractionPrompt_EmptySource_ShortCircuits_Exit0()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var doc = WriteFile("docs/empty.md", "   \n  ");
            var exit = await RunAsync("ai", "compile-extraction-prompt", "--doc", doc);
            Assert.Equal(0, exit); // FR-003 short-circuit: no prompt, no model turn
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    // ---------- ingest-criteria ----------

    [Fact]
    public async Task IngestCriteria_Extracted_Exit0()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteConfig();
            var from = Path.Combine(_dir, "resp.json");
            File.WriteAllText(from, GoodJson);

            var exit = await RunAsync("ai", "ingest-criteria", "--doc", "docs/payment.md", "--from", from);
            Assert.Equal(0, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestCriteria_EmptyResponse_Exit5()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteConfig();
            var from = Path.Combine(_dir, "resp.json");
            File.WriteAllText(from, "   ");

            var exit = await RunAsync("ai", "ingest-criteria", "--doc", "docs/payment.md", "--from", from);
            Assert.Equal(5, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestCriteria_ParseFailure_Exit6()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteConfig();
            var from = Path.Combine(_dir, "resp.json");
            File.WriteAllText(from, "not json at all");

            var exit = await RunAsync("ai", "ingest-criteria", "--doc", "docs/payment.md", "--from", from);
            Assert.Equal(6, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }
}
