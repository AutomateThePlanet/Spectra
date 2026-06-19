using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Show;
using Spectra.CLI.Options;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 072 FR3 — show {id} --output-format json includes the file field.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class ShowHandlerFileFieldTests : IDisposable
{
    private readonly string _dir;

    public ShowHandlerFileFieldTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-show-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private void WriteTestMd(string suite, string id)
    {
        var dir = Path.Combine(_dir, "test-cases", suite);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{id}.md"), $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            ---

            # Verify {id}

            ## Steps

            1. Open the application

            ## Expected Result

            Application opens successfully
            """);
        File.WriteAllText(Path.Combine(dir, "_index.json"),
            $$"""{"suite":"{{suite}}","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"{{id}}","title":"Verify {{id}}","priority":"medium","file":"{{suite}}/{{id}}.md"}]}""");
    }

    private static async Task<int> RunAsync(params string[] args)
    {
        var root = new RootCommand();
        GlobalOptions.AddTo(root);
        root.AddCommand(new ShowCommand());
        return await root.InvokeAsync(args);
    }

    [Fact]
    public async Task JsonOutput_IncludesFileField()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-200");
            var exit = await RunAsync("show", "TC-200", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<ShowResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(result?.Test.File);
            Assert.EndsWith(".md", result!.Test.File);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task FileField_IsRelativeToWorkingDirectory()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-201");
            var exit = await RunAsync("show", "TC-201", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<ShowResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(result?.Test.File);
            Assert.False(Path.IsPathRooted(result!.Test.File), $"file should be relative, got: {result.Test.File}");
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task FileField_ContainsTestId()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-202");
            var exit = await RunAsync("show", "TC-202", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = JsonSerializer.Deserialize<ShowResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(result?.Test.File);
            Assert.Contains("TC-202", result!.Test.File);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task HumanOutput_DoesNotContainFileLine()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-203");
            var exit = await RunAsync("show", "TC-203");
            Assert.Equal(0, exit);
            var output = captured.ToString();
            // Human output intentionally omits the file: line
            Assert.DoesNotContain("File:", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }
}
