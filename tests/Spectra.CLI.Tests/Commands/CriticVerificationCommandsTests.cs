using System.CommandLine;
using Spectra.CLI.Commands.Ai;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 055 — exit-code contract for the model-free critic commands.
/// US1 (compile-critic-prompt refuse → 4), US3 (ingest-verdict Verdict→0, EmptyResponse→5,
/// ParseFailure→6). No model, no network. ingest-verdict persists nothing, so it needs no config.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class CriticVerificationCommandsTests : IDisposable
{
    private readonly string _dir;

    public CriticVerificationCommandsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-critic-cmd-{Guid.NewGuid():N}");
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

    private string WriteFile(string relative, string content)
    {
        var full = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return relative.Replace('\\', '/');
    }

    // ---------- compile-critic-prompt ----------

    [Fact]
    public async Task CompileCriticPrompt_MissingTest_Refuses_Exit4()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var exit = await RunAsync("ai", "compile-critic-prompt");
            Assert.Equal(4, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileCriticPrompt_WithTest_Emits_Exit0()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var test = WriteFile("tc.json",
                "{\"id\":\"TC-1\",\"title\":\"Login\",\"steps\":[\"open\"],\"expected_result\":\"ok\"}");
            var exit = await RunAsync("ai", "compile-critic-prompt", "--test", test);
            Assert.Equal(0, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    // ---------- compile-critic-prompt source_refs auto-load (Spec 078: shared resolver) ----------

    private void WriteSuiteTestWithRefs(string suite, string id, params string[] refs)
    {
        var refLines = string.Concat(refs.Select(r => $"  - \"{r.Replace("\\", "\\\\")}\"\n"));
        WriteFile($"test-cases/{suite}/{id}.md",
            $"---\nid: {id}\npriority: medium\nsource_refs:\n{refLines}---\n\n# Login\n\n## Steps\n\n1. open\n\n## Expected Result\n\n- ok\n");
        WriteSuiteIndex(suite, (id, "Login"));
    }

    private async Task<(int Exit, string Out, string Err)> RunCapturedAsync(params string[] args)
    {
        var original = Directory.GetCurrentDirectory();
        var stdout = Console.Out;
        var stderr = Console.Error;
        var output = new StringWriter();
        var error = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            Console.SetOut(output);
            Console.SetError(error);
            var exit = await RunAsync(args);
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            Directory.SetCurrentDirectory(original);
        }
    }

    private static int Occurrences(string text, string marker) =>
        (text.Length - text.Replace(marker, "").Length) / marker.Length;

    [Fact]
    public async Task CompileCriticPrompt_ResolvesFragmentBackslashAndLegacyRefs_EachDocOnce()
    {
        WriteFile("docs/a.md", "DOC-A-MARKER");
        WriteFile("docs/b.md", "DOC-B-MARKER");
        WriteFile("docs/c.md", "DOC-C-MARKER");
        WriteSuiteTestWithRefs("auth", "TC-1", "docs/a.md#Section", "docs\\b.md", "c.md", "docs/a.md#Other");

        var (exit, output, error) = await RunCapturedAsync("ai", "compile-critic-prompt", "--suite", "auth", "--test", "TC-1");

        Assert.True(exit == 0, error);
        Assert.Equal(1, Occurrences(output, "DOC-A-MARKER"));
        Assert.Equal(1, Occurrences(output, "DOC-B-MARKER"));
        Assert.Equal(1, Occurrences(output, "DOC-C-MARKER"));
    }

    [Fact]
    public async Task CompileCriticPrompt_MissingRef_FailsLoud_Exit1()
    {
        WriteFile("docs/a.md", "DOC-A-MARKER");
        WriteSuiteTestWithRefs("auth", "TC-1", "docs/a.md", "docs/gone.md#S");

        var (exit, _, error) = await RunCapturedAsync("ai", "compile-critic-prompt", "--suite", "auth", "--test", "TC-1");

        Assert.Equal(1, exit);
        Assert.Contains("Source ref not found: docs/gone.md#S", error);
    }

    [Fact]
    public async Task CompileCriticPrompt_RefOutsideRepo_FailsLoud_Exit1()
    {
        WriteSuiteTestWithRefs("auth", "TC-1", "../outside.md");

        var (exit, _, error) = await RunCapturedAsync("ai", "compile-critic-prompt", "--suite", "auth", "--test", "TC-1");

        Assert.Equal(1, exit);
        Assert.Contains("Source ref not found: ../outside.md", error);
    }

    // ---------- compile-critic-prompt --suite (Spec 071: ID→path resolution) ----------

    private void WriteSuiteTest(string suite, string id, string title)
    {
        WriteFile($"test-cases/{suite}/{id}.md",
            $"---\nid: {id}\npriority: medium\n---\n\n# {title}\n\n## Steps\n\n1. open\n\n## Expected Result\n\n- ok\n");
    }

    private void WriteSuiteIndex(string suite, params (string Id, string Title)[] tests)
    {
        var entries = string.Join(",\n",
            tests.Select(t => $"    {{ \"id\": \"{t.Id}\", \"file\": \"{t.Id}.md\", \"title\": \"{t.Title}\", \"priority\": \"medium\" }}"));
        WriteFile($"test-cases/{suite}/_index.json",
            $"{{\n  \"suite\": \"{suite}\",\n  \"generated_at\": \"2026-01-01T00:00:00Z\",\n  \"tests\": [\n{entries}\n  ]\n}}");
    }

    [Fact]
    public async Task CompileCriticPrompt_SuiteAndId_ResolvesById_Exit0()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteSuiteTest("calc", "TC-1", "Add two numbers");
            WriteSuiteIndex("calc", ("TC-1", "Add two numbers"));

            var exit = await RunAsync("ai", "compile-critic-prompt", "--suite", "calc", "--test", "TC-1");
            Assert.Equal(0, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileCriticPrompt_SuiteOnly_EmitsAll_Exit0()
    {
        var original = Directory.GetCurrentDirectory();
        var stdout = Console.Out;
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteSuiteTest("calc", "TC-1", "Add two numbers");
            WriteSuiteTest("calc", "TC-2", "Subtract two numbers");
            WriteSuiteIndex("calc", ("TC-1", "Add two numbers"), ("TC-2", "Subtract two numbers"));

            var captured = new StringWriter();
            Console.SetOut(captured);
            var exit = await RunAsync("ai", "compile-critic-prompt", "--suite", "calc");
            Console.SetOut(stdout);

            Assert.Equal(0, exit);
            var output = captured.ToString();
            Assert.Contains("TC-1", output);
            Assert.Contains("TC-2", output);
        }
        finally { Console.SetOut(stdout); Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileCriticPrompt_MissingSuite_FailsLoud_Exit1()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var exit = await RunAsync("ai", "compile-critic-prompt", "--suite", "no-such-suite");
            Assert.Equal(1, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileCriticPrompt_UnknownId_FailsLoud_Exit1()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteSuiteTest("calc", "TC-1", "Add two numbers");
            WriteSuiteIndex("calc", ("TC-1", "Add two numbers"));

            var exit = await RunAsync("ai", "compile-critic-prompt", "--suite", "calc", "--test", "TC-999");
            Assert.Equal(1, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    // ---------- ingest-verdict ----------

    [Fact]
    public async Task IngestVerdict_WellFormed_Exit0()
    {
        var from = Path.Combine(_dir, "v.json");
        File.WriteAllText(from, "{\"verdict\":\"grounded\",\"score\":0.95,\"findings\":[]}");

        var exit = await RunAsync("ai", "ingest-verdict", "--from", from);
        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task IngestVerdict_Empty_Exit5()
    {
        var from = Path.Combine(_dir, "v.json");
        File.WriteAllText(from, "   ");

        var exit = await RunAsync("ai", "ingest-verdict", "--from", from);
        Assert.Equal(5, exit);
    }

    [Fact]
    public async Task IngestVerdict_MissingVerdict_Exit6()
    {
        var from = Path.Combine(_dir, "v.json");
        File.WriteAllText(from, "{\"score\":0.5}");

        var exit = await RunAsync("ai", "ingest-verdict", "--from", from);
        Assert.Equal(6, exit);
    }

    [Fact]
    public async Task IngestVerdict_Garbage_Exit6()
    {
        var from = Path.Combine(_dir, "v.json");
        File.WriteAllText(from, "not json at all");

        var exit = await RunAsync("ai", "ingest-verdict", "--from", from);
        Assert.Equal(6, exit);
    }
}
