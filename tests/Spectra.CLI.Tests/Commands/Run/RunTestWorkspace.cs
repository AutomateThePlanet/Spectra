using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Run;

/// <summary>
/// Spec 065: builds a throwaway workspace with a <c>test-cases/{suite}/_index.json</c> so the
/// <c>spectra run</c> handlers can be driven end-to-end against a real on-disk index + SQLite db,
/// with NO MCP server and NO MCP config. Disposes the temp directory.
/// </summary>
public sealed class RunTestWorkspace : IDisposable
{
    public string Root { get; }

    public RunTestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), $"spectra-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    /// <summary>Writes a suite index. Entries are (id, title, priority, dependsOn).</summary>
    public void WriteSuite(string suite, params (string Id, string Title, string Priority, string? DependsOn)[] entries)
    {
        var suiteDir = Path.Combine(Root, "test-cases", suite);
        Directory.CreateDirectory(suiteDir);

        var index = new MetadataIndex
        {
            Suite = suite,
            GeneratedAt = DateTime.UtcNow,
            Tests = entries.Select(e => new TestIndexEntry
            {
                Id = e.Id,
                Title = e.Title,
                Priority = e.Priority,
                File = $"{e.Id}.md",
                DependsOn = e.DependsOn
            }).ToList()
        };

        new IndexWriter().WriteAsync(Path.Combine(suiteDir, "_index.json"), index).GetAwaiter().GetResult();

        // Minimal test-case files so loaders that read them succeed.
        foreach (var e in entries)
        {
            var md = $"---\nid: {e.Id}\ntitle: {e.Title}\npriority: {e.Priority}\n---\n\n## Steps\n1. do it\n\n## Expected Result\nit works\n";
            File.WriteAllText(Path.Combine(suiteDir, $"{e.Id}.md"), md);
        }
    }

    public bool HasMcpConfig =>
        File.Exists(Path.Combine(Root, ".mcp.json")) ||
        File.Exists(Path.Combine(Root, ".vscode", "mcp.json")) ||
        File.Exists(Path.Combine(Root, "claude_desktop_config.json"));

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch { }
    }
}
