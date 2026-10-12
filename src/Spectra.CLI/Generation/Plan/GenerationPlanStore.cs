using Spectra.CLI.Baseline;

namespace Spectra.CLI.Generation.Plan;

/// <summary>Spec 094: the outcome of loading a suite's plan.</summary>
/// <param name="Plan">The plan, when it exists and parses.</param>
/// <param name="Problem">Null when loaded; otherwise "no plan" or why the file is unreadable.</param>
public sealed record GenerationPlanLoad(GenerationPlan? Plan, string? Problem);

/// <summary>
/// Spec 094 (FR-003): where a suite's generation plan lives (<c>.spectra/generation-plan/&lt;suite&gt;.json</c>), its atomic
/// save, and a strict load. The plan is scratch and gitignored; no lock — one generation per suite at a time.
/// </summary>
public static class GenerationPlanStore
{
    public const string Folder = ".spectra/generation-plan";

    /// <summary>The repo-relative path of <paramref name="suite"/>'s plan, with <c>/</c> separators.</summary>
    public static string RelativePathFor(string suite) => $"{Folder}/{suite}.json";

    public static string PathFor(string repoRoot, string suite) =>
        Path.Combine(repoRoot, ".spectra", "generation-plan", $"{suite}.json");

    public static async Task SaveAsync(string repoRoot, GenerationPlan plan, CancellationToken ct)
    {
        var path = PathFor(repoRoot, plan.Suite);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await BaselineStore.WriteAtomicAsync(path, GenerationPlan.Serialize(plan), ct);
    }

    public static async Task<GenerationPlanLoad> LoadAsync(string repoRoot, string suite, CancellationToken ct)
    {
        var path = PathFor(repoRoot, suite);
        if (!File.Exists(path))
        {
            return new GenerationPlanLoad(null, null);
        }

        try
        {
            return new GenerationPlanLoad(GenerationPlan.Parse(await File.ReadAllTextAsync(path, ct)), null);
        }
        catch (GenerationPlanFormatException ex)
        {
            return new GenerationPlanLoad(null, ex.Message);
        }
        catch (IOException ex)
        {
            return new GenerationPlanLoad(null, ex.Message);
        }
    }
}
