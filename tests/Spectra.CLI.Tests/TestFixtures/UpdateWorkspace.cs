using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;
using Spectre.Console;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Temp workspace for driving <c>spectra ai update</c> end to end (Spec 078). Tests using it must be in the
/// <c>WorkingDirectory</c> collection, because the command resolves everything from the current directory.
/// </summary>
public sealed class UpdateWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"spectra-078-update-{Guid.NewGuid():N}");

    public UpdateWorkspace()
    {
        Directory.CreateDirectory(Path.Combine(Root, "docs"));
        Directory.CreateDirectory(Path.Combine(Root, "test-cases"));
        var config = new
        {
            source = new { local_dir = "docs/", include_patterns = new[] { "**/*.md" } },
            tests = new { dir = "test-cases/" },
            ai = new { }
        };
        File.WriteAllText(Path.Combine(Root, "spectra.config.json"), JsonSerializer.Serialize(config));
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }

    public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public void WriteDoc(string relative, string content)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public string WriteTest(string suite, string id, string markdown)
    {
        var path = PathOf($"test-cases/{suite}/{id}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown);
        return path;
    }

    /// <summary>A simple test whose words all appear in <see cref="MatchingDoc"/>.</summary>
    public static string SimpleTest(string id, params string[] sourceRefs) =>
        "---\n" +
        $"id: {id}\n" +
        "priority: high\n" +
        "source_refs:\n" +
        string.Concat(sourceRefs.Select(r => $"  - \"{r.Replace("\\", "\\\\")}\"\n")) +
        "---\n\n" +
        $"# Checkout payment flow {id}\n\n" +
        "## Steps\n\n" +
        "1. Open checkout payment page\n\n" +
        "## Expected Result\n\n" +
        "Payment accepted\n";

    public const string MatchingDoc =
        "# Checkout\n\n## Payment\n\nCheckout payment flow. Open checkout payment page. Payment accepted.\n";

    /// <summary>SHA-256 of every file, excluding run-status artifacts (result/progress files, .spectra/).</summary>
    public IReadOnlyDictionary<string, string> Snapshot() =>
        Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Where(r => r is not ".spectra-result.json" and not ".spectra-progress.html" && !r.StartsWith(".spectra/"))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToDictionary(r => r, r => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(PathOf(r)))));

    /// <summary>Runs <c>spectra</c> with <paramref name="args"/> from the workspace root, capturing console output.</summary>
    public async Task<(int Exit, string Output)> RunAsync(params string[] args)
    {
        var originalDir = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalAnsi = AnsiConsole.Console;
        var writer = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(Root);
            Console.SetOut(writer);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer),
                Interactive = InteractionSupport.No
            });

            var root = new RootCommand();
            GlobalOptions.AddTo(root);
            root.AddCommand(AiCommand.Create());
            var exit = await root.InvokeAsync(args);
            return (exit, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            AnsiConsole.Console = originalAnsi;
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    public JsonElement ReadResult()
    {
        var path = PathOf(".spectra-result.json");
        Assert.True(File.Exists(path), ".spectra-result.json was not written");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }
}
