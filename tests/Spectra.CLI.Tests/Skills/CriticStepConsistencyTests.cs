using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 102 F8 (FR-017; research R8; contracts C3): agents can't include a shared file, so the "Verify with the critic"
/// step is copied into the generator, the repairer and the updater. The block from the Agent call to "routes each test
/// by your verdict line." must be identical in all three; the only allowed differences are in the heading paragraph
/// before it, listed in <see cref="Prefixes"/>. A new difference has to be added there on purpose.
/// </summary>
public sealed class CriticStepConsistencyTests
{
    private const string SharedStart = "`spectra-critic` subagent with the Agent tool";
    private const string SharedEnd = "routes each test by your verdict line.";

    /// <summary>The allowed differences: step number, which ids are verified, and the <c>verify:</c> rule.</summary>
    private static readonly (string File, string Step, string[] Ids, string? Verify)[] Prefixes =
    [
        ("spectra-generator.agent.md", "4", ["For each id in `ids`"], null),
        ("spectra-repairer.agent.md", "5", ["your final `written` lines", "`unchanged` ids"], "Skip it with `verify: no`"),
        ("spectra-updater.agent.md", "6", ["your final `written` lines", "never for `written (manual verdict kept)`"], "Only with `verify: yes`"),
    ];

    private static (string Prefix, string Shared) Split(string file, string step)
    {
        var text = AgentContent.All[file].Replace("\r\n", "\n");
        var start = text.IndexOf($"**{step}. Verify with the critic**", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{file}: no step {step} 'Verify with the critic'");
        var end = text.IndexOf(SharedEnd, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"{file}: no '{SharedEnd}'");
        var block = text[start..(end + SharedEnd.Length)];
        var split = block.IndexOf(SharedStart, StringComparison.Ordinal);
        Assert.True(split >= 0, $"{file}: no '{SharedStart}'");
        return (block[..split], block[split..]);
    }

    [Fact]
    public void TheSharedBlock_IsIdentical_InTheThreeWorkAgents()
    {
        var reference = Split(Prefixes[0].File, Prefixes[0].Step).Shared.Split('\n');
        foreach (var (file, step, _, _) in Prefixes.Skip(1))
        {
            var lines = Split(file, step).Shared.Split('\n');
            for (var i = 0; i < Math.Max(reference.Length, lines.Length); i++)
            {
                var expected = i < reference.Length ? reference[i] : "<end>";
                var actual = i < lines.Length ? lines[i] : "<end>";
                Assert.True(expected == actual,
                    $"{file} differs from {Prefixes[0].File} at shared line {i + 1}:\n  expected: {expected}\n  actual:   {actual}");
            }
        }
    }

    [Fact]
    public void EachHeadingParagraph_HoldsOnlyItsListedDifferences()
    {
        foreach (var (file, step, ids, verify) in Prefixes)
        {
            var prefix = Regex.Replace(Split(file, step).Prefix, @"\s+", " ");

            Assert.StartsWith($"**{step}. Verify with the critic**", prefix);
            foreach (var words in ids)
            {
                Assert.True(prefix.Contains(words, StringComparison.Ordinal), $"{file}: heading lacks '{words}'");
            }

            if (verify is null)
            {
                Assert.False(prefix.Contains("verify:", StringComparison.Ordinal), $"{file}: heading has a verify: rule");
            }
            else
            {
                Assert.True(prefix.Contains(verify, StringComparison.Ordinal), $"{file}: heading lacks '{verify}'");
            }

            Assert.EndsWith("one at a time, start the ", prefix);
        }
    }
}
