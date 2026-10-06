using Spectra.CLI.Baseline;
using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Config;
using Spectra.Core.Coverage;
using Spectra.Core.Git;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Spec 086 (FR-019, research R8): the logic of <c>spectra docs topology</c> — the repositories that own the docs
/// tests reference (root, submodule, HEAD, shallow, how many referenced docs), and the referenced docs no repository
/// owns, with the reason. Read-only; exit 0.
/// </summary>
public static class DocsTopologyHandler
{
    public const string Command = "docs-topology";

    private sealed record OwnerRow(string Root, bool IsSubmodule, string Head, bool Shallow, int ReferencedDocs);

    private sealed record UnownedRow(string Doc, string Reason);

    public static async Task<int> RunAsync(string root, bool json, CancellationToken ct)
    {
        var loaded = await ConfigLoader.LoadForReadAsync(root, ConfigSections.Source | ConfigSections.Tests, ct);
        if (!loaded.IsSuccess)
        {
            return BaselineCommandIO.Fail(json, Command, loaded.ErrorCode!, loaded.Error!);
        }

        var config = loaded.Config!;
        var warnings = new List<string>(loaded.Warnings);
        BaselineCommandIO.PrintWarnings(loaded.Warnings);

        var sourceDir = config.Source?.LocalDir;
        var resolver = new SourceRefResolver(root, string.IsNullOrWhiteSpace(sourceDir) ? "docs/" : sourceDir);
        var testsPath = BaselineCommandIO.TestsPath(root, config);
        var refs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suite in BaselineCommandIO.ListSuites(testsPath))
        {
            var tests = await BaselineCommandIO.LoadSuiteTestsAsync(root, testsPath, suite, warnings, ct);
            refs.UnionWith(tests.Tests.SelectMany(t => t.SourceRefs).Where(r => !string.IsNullOrWhiteSpace(r)));
        }

        var docs = resolver.Resolve(refs.OrderBy(r => r, StringComparer.Ordinal)).DistinctFullPaths;
        var owners = GitAccess.CreateOwnerResolver();
        var counts = new Dictionary<GitOwner, int>();
        var unowned = new List<UnownedRow>();
        foreach (var doc in docs)
        {
            var (owner, reason) = await owners.OwnerOfAsync(doc, ct);
            if (owner is null)
            {
                unowned.Add(new UnownedRow(Display(root, doc), reason!));
            }
            else
            {
                counts[owner] = counts.GetValueOrDefault(owner) + 1;
            }
        }

        var rows = counts
            .Select(c => new OwnerRow(Display(root, c.Key.Root), c.Key.IsSubmodule, c.Key.Head, c.Key.Shallow, c.Value))
            .OrderBy(r => r.Root, StringComparer.Ordinal)
            .ToList();
        unowned = unowned.OrderBy(u => u.Doc, StringComparer.Ordinal).ToList();

        BaselineCommandIO.PrintLateWarnings(warnings, loaded.Warnings.Count);
        var document = new Dictionary<string, object?>
        {
            ["command"] = Command,
            ["success"] = true,
            ["owners"] = rows.Select(r => new Dictionary<string, object?>
            {
                ["root"] = r.Root,
                ["is_submodule"] = r.IsSubmodule,
                ["head"] = r.Head,
                ["shallow"] = r.Shallow,
                ["referenced_docs"] = r.ReferencedDocs
            }).ToList(),
            ["unowned"] = unowned.Select(u => new Dictionary<string, object?> { ["doc"] = u.Doc, ["reason"] = u.Reason }).ToList(),
            ["warnings"] = warnings
        };

        BaselineCommandIO.Emit(json, document, writer =>
        {
            writer.WriteLine($"Docs topology — {docs.Count} referenced doc(s), {rows.Count} repository(ies)");
            foreach (var r in rows)
            {
                writer.WriteLine($"  {r.Root,-40} {(r.IsSubmodule ? "submodule" : "repository"),-10} {r.Head[..Math.Min(12, r.Head.Length)]}"
                    + $"{(r.Shallow ? " (shallow)" : "")}  {r.ReferencedDocs} doc(s)");
            }

            if (unowned.Count > 0)
            {
                writer.WriteLine($"  Not in a readable git repository ({unowned.Count}):");
                foreach (var u in unowned)
                {
                    writer.WriteLine($"    {u.Doc} — {u.Reason}");
                }
            }
        });

        return ExitCodes.Success;
    }

    /// <summary>Repo-relative with <c>/</c> (<c>.</c> for the root itself) when inside the repo; otherwise absolute.</summary>
    private static string Display(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        return relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? fullPath.Replace('\\', '/')
            : relative;
    }
}
