using System.Text.Json;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Criteria;

/// <summary>
/// Spec 079 FR-012: which tests still reference each criterion ID, read from every suite's <c>_index.json</c>
/// (<c>tests[].criteria</c>). Read-only and best effort: an unreadable index is skipped with a warning.
/// </summary>
public static class CriteriaReferenceCounter
{
    public static async Task<(IReadOnlyDictionary<string, IReadOnlyList<string>> TestsById, IReadOnlyList<string> Warnings)> LoadAsync(
        string repoRoot, SpectraConfig config, CancellationToken ct)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var testsDir = Path.Combine(repoRoot, config.Tests.Dir);

        if (Directory.Exists(testsDir))
        {
            foreach (var indexPath in Directory.GetFiles(testsDir, "_index.json", SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath, ct));
                    if (!doc.RootElement.TryGetProperty("tests", out var tests) || tests.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var test in tests.EnumerateArray())
                    {
                        var id = test.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        if (string.IsNullOrWhiteSpace(id)
                            || !test.TryGetProperty("criteria", out var criteria)
                            || criteria.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var criterionId in criteria.EnumerateArray().Select(c => c.GetString())
                                     .Where(c => !string.IsNullOrWhiteSpace(c)))
                        {
                            if (!map.TryGetValue(criterionId!, out var list))
                            {
                                map[criterionId!] = list = [];
                            }

                            if (!list.Contains(id, StringComparer.OrdinalIgnoreCase))
                            {
                                list.Add(id);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
                {
                    warnings.Add($"Could not read {Path.GetRelativePath(repoRoot, indexPath)}: {ex.Message}");
                }
            }
        }

        var result = map.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            StringComparer.OrdinalIgnoreCase);
        return (result, warnings);
    }
}
