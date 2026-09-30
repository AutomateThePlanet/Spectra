using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Options;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Temp workspace for acceptance-criteria commands (Spec 079). Tests that use <see cref="RunAsync"/> must be in the
/// <see cref="CriteriaCommandsCollection"/>: the commands resolve everything from the current directory and the
/// harness captures the global console.
/// </summary>
public sealed class CriteriaTestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"spectra-079-criteria-{Guid.NewGuid():N}");

    public string CriteriaDir => PathOf("docs/criteria");

    public string IndexPath => PathOf("docs/criteria/_criteria_index.yaml");

    public CriteriaTestWorkspace()
    {
        Directory.CreateDirectory(PathOf("docs/criteria"));
        Directory.CreateDirectory(PathOf("test-cases"));
        Directory.CreateDirectory(PathOf(".spectra"));
        File.WriteAllText(PathOf("spectra.config.json"), JsonSerializer.Serialize(SpectraConfig.Default));
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public string PathOf(string relative) =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public string WriteDoc(string relative, string text)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text.Replace("\r\n", "\n"));
        return path;
    }

    public void WriteIndexYaml(string yaml) => File.WriteAllText(IndexPath, yaml.Replace("\r\n", "\n"));

    public string WriteCriteriaFile(string relativeToCriteriaDir, string yaml)
    {
        var path = Path.Combine(CriteriaDir, relativeToCriteriaDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml.Replace("\r\n", "\n"));
        return path;
    }

    /// <summary>Writes <c>test-cases/{suite}/_index.json</c> with each test's <c>criteria</c> list.</summary>
    public void WriteTestIndex(string suite, params (string Id, string[] Criteria)[] tests)
    {
        var path = PathOf($"test-cases/{suite}/_index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var index = new
        {
            suite,
            generated_at = "2026-01-01T00:00:00Z",
            tests = tests.Select(t => new { id = t.Id, file = $"{t.Id}.md", title = t.Id, priority = "medium", criteria = t.Criteria })
        };
        File.WriteAllText(path, JsonSerializer.Serialize(index));
    }

    public Task<CriteriaIndex> ReadIndexAsync() => new CriteriaIndexReader().ReadStrictAsync(IndexPath);

    public Task<IReadOnlyList<AcceptanceCriterion>> ReadCriteriaAsync(string relativeToCriteriaDir) =>
        new CriteriaFileReader().ReadStrictAsync(
            Path.Combine(CriteriaDir, relativeToCriteriaDir.Replace('/', Path.DirectorySeparatorChar)));

    public static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>SHA-256 of every file in the workspace except the lock and run-status files.</summary>
    public IReadOnlyDictionary<string, string> Snapshot() =>
        Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Where(r => !r.StartsWith(".spectra/", StringComparison.Ordinal) && r is not ".spectra-result.json" and not ".spectra-progress.html")
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToDictionary(r => r, r => Sha256(PathOf(r)));

    /// <summary>Serialises a model response to <c>.spectra/criteria.json</c> and returns its path.</summary>
    public string Response(params object[] items)
    {
        var path = PathOf(".spectra/criteria.json");
        File.WriteAllText(path, JsonSerializer.Serialize(items));
        return path;
    }

    /// <summary>Runs <c>spectra</c> from the workspace root, capturing stdout and stderr separately.</summary>
    public async Task<(int Exit, string Out, string Err)> RunAsync(params string[] args)
    {
        var originalDir = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(Root);
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var root = new RootCommand();
            GlobalOptions.AddTo(root);
            root.AddCommand(AiCommand.Create());
            root.AddCommand(new DocsCommand());
            var exit = await root.InvokeAsync(args);
            return (exit, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>Ingests <paramref name="items"/> for <paramref name="doc"/> in JSON mode.</summary>
    public Task<(int Exit, string Out, string Err)> IngestAsync(string doc, params object[] items)
    {
        Response(items);
        return RunAsync("ai", "ingest-criteria", "--doc", doc, "--from", ".spectra/criteria.json", "--output-format", "json");
    }

    public static JsonElement Json(string text)
    {
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }
}
