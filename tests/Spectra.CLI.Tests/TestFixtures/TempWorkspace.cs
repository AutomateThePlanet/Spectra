using System.Text.Json;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Disposable temp workspace for lifecycle and cancellation tests. Creates
/// <c>test-cases/</c>, <c>.spectra/</c>, and an empty <c>spectra.config.json</c>
/// under a unique temp dir; cleans up on dispose.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    public string Root { get; }
    public string TestCasesDir => Path.Combine(Root, "test-cases");
    public string SpectraDir => Path.Combine(Root, ".spectra");
    public string ConfigPath => Path.Combine(Root, "spectra.config.json");

    public TempWorkspace(string? prefix = null)
    {
        var name = (prefix ?? "spectra-test") + "-" + Guid.NewGuid().ToString("N");
        Root = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(TestCasesDir);
        Directory.CreateDirectory(SpectraDir);
        File.WriteAllText(ConfigPath, "{}");
    }

    /// <summary>
    /// Creates a suite directory with an empty <c>_index.json</c>.
    /// </summary>
    public string AddSuite(string name)
    {
        var path = Path.Combine(TestCasesDir, name);
        Directory.CreateDirectory(path);
        var index = new
        {
            suite = name,
            generated_at = DateTimeOffset.UtcNow.ToString("o"),
            test_count = 0,
            tests = Array.Empty<object>()
        };
        File.WriteAllText(Path.Combine(path, "_index.json"), JsonSerializer.Serialize(index));
        return path;
    }

    /// <summary>
    /// Writes a test-case markdown file with frontmatter.
    /// </summary>
    public string AddTestCase(
        string suite,
        string id,
        string? title = null,
        IEnumerable<string>? automatedBy = null,
        IEnumerable<string>? dependsOn = null)
    {
        var suitePath = Path.Combine(TestCasesDir, suite);
        if (!Directory.Exists(suitePath))
        {
            AddSuite(suite);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.Append("id: ").AppendLine(id);
        sb.Append("title: ").AppendLine(title ?? id);
        sb.Append("suite: ").AppendLine(suite);
        if (automatedBy != null)
        {
            var list = automatedBy.ToList();
            if (list.Count > 0)
            {
                sb.AppendLine("automated_by:");
                foreach (var a in list)
                {
                    sb.Append("  - ").AppendLine(a);
                }
            }
        }
        if (dependsOn != null)
        {
            var list = dependsOn.ToList();
            if (list.Count > 0)
            {
                sb.AppendLine("depends_on:");
                foreach (var d in list)
                {
                    sb.Append("  - ").AppendLine(d);
                }
            }
        }
        sb.AppendLine("priority: medium");
        sb.AppendLine("tags: []");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# " + (title ?? id));
        sb.AppendLine();
        sb.AppendLine("## Steps");
        sb.AppendLine("1. Step one");
        sb.AppendLine();
        sb.AppendLine("## Expected Results");
        sb.AppendLine("- Result one");

        var path = Path.Combine(suitePath, $"{id}.md");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
