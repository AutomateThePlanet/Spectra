using System.Text;
using Spectra.Core.Baseline;

namespace Spectra.CLI.Generation;

/// <summary>
/// Spec 088 (research R2, contract): renders an <see cref="UpdateContext"/> into the template's <c>source_diff</c>,
/// <c>acceptance_criteria</c> and <c>tests</c> blocks. Deterministic: LF line endings, no timestamps, input order only.
/// Doc text is fenced with tildes (at least four, longer than any tilde run inside it), so a code fence in a doc can't
/// close the block.
/// </summary>
public static class UpdatePromptRenderer
{
    public static string SourceDiff(UpdateContext context)
    {
        var sb = new StringBuilder();
        foreach (var section in context.Sections)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"#### {section.Label} — {section.Ref}  (tests: {string.Join(", ", section.Tests)})\n");
            switch (section.State)
            {
                case SectionState.Removed:
                    sb.Append($"State: REMOVED — {section.Reason}\n");
                    continue;
                case SectionState.Relink:
                    sb.Append($"State: RELINK — {section.Reason}\n");
                    continue;
                case SectionState.Unchanged:
                    sb.Append("State: unchanged\n");
                    AppendFenced(sb, "Current section:", "markdown", section.Current);
                    continue;
                case SectionState.NotBaselined:
                    sb.Append("State: not baselined — previous version unavailable\n");
                    AppendFenced(sb, "Current section:", "markdown", section.Current);
                    continue;
            }

            var why = section.Why.Count > 0 ? $" — {string.Join("; ", section.Why)}" : "";
            sb.Append($"State: changed ({section.Bucket}){why}\n");
            AppendFenced(sb, "Current section:", "markdown", section.Current);
            if (section.Previous is not null)
            {
                AppendFenced(sb, "Previous version (normalised: lowercased, markup removed):", "text", section.Previous);
            }
            else
            {
                sb.Append($"Previous version: unavailable ({section.PreviousUnavailableReason})\n");
            }

            if (section.Diff.Count > 0)
            {
                sb.Append("What changed: ")
                    .Append(string.Join("; ", section.Diff.Select(d => $"{(d.Added ? "added" : "removed")} \"{string.Join(" ", d.Words)}\"")))
                    .Append('\n');
            }
        }

        return sb.ToString();
    }

    public static string Criteria(UpdateContext context)
    {
        var sb = new StringBuilder();
        foreach (var criterion in context.Criteria)
        {
            sb.Append(criterion.State switch
            {
                LinkedCriterionState.Removed => $"- {criterion.Id} (removed)\n",
                LinkedCriterionState.ChangedSinceBaseline => $"- {criterion.Id} (changed since baseline): {OneLine(criterion.Text)}\n",
                LinkedCriterionState.NotBaselined =>
                    $"- {criterion.Id} (not baselined — compare the test with the current text): {OneLine(criterion.Text)}\n",
                _ => $"- {criterion.Id}: {OneLine(criterion.Text)}\n"
            });
        }

        return sb.ToString();
    }

    public static string Tests(UpdateContext context)
    {
        // Spec 098 (F7): criteria the group's tests see differently get each test's own state in its block; the shared
        // list (Criteria) keeps the strongest. A criterion every linking test sees the same way is not annotated.
        var disagreeing = context.Tests
            .SelectMany(b => b.CriterionIds.Select((id, i) => (Id: id, State: b.CriterionStates[i])))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.State).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        foreach (var block in context.Tests)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"### TEST {block.Test.Id}\n");
            sb.Append($"Sections: {(block.SectionLabels.Count > 0 ? string.Join(", ", block.SectionLabels) : "none")}\n");
            var criteria = block.CriterionIds.Select((id, i) => disagreeing.Contains(id) ? $"{id} ({StateLabel(block.CriterionStates[i])})" : id);
            sb.Append($"Criteria: {(block.CriterionIds.Count > 0 ? string.Join(", ", criteria) : "none")}\n");
            sb.Append("```json\n").Append(Lf(UpdatePromptCompiler.SerializeTest(block.Test))).Append("\n```\n");
        }

        return sb.ToString();
    }

    /// <summary>Spec 098 (F7): a test's own state of a criterion, as shown next to its id.</summary>
    private static string StateLabel(LinkedCriterionState state) => state switch
    {
        LinkedCriterionState.ChangedSinceBaseline => "changed",
        LinkedCriterionState.NotBaselined => "not baselined",
        LinkedCriterionState.Removed => "removed",
        _ => "unchanged"
    };

    /// <summary>
    /// Appends <paramref name="heading"/>, then <paramref name="text"/> (LF, trailing newlines trimmed) in a <c>~~~~</c>
    /// fence longer than any tilde run in it. Spec 094: shared with the generation batch prompt.
    /// </summary>
    internal static void AppendFenced(StringBuilder sb, string heading, string language, string? text)
    {
        var body = Lf(text ?? string.Empty).TrimEnd('\n');
        var fence = new string('~', Math.Max(4, LongestTildeRun(body) + 1));
        sb.Append(heading).Append('\n')
            .Append(fence).Append(language).Append('\n')
            .Append(body).Append('\n')
            .Append(fence).Append('\n');
    }

    private static int LongestTildeRun(string text)
    {
        int longest = 0, run = 0;
        foreach (var c in text)
        {
            run = c == '~' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    private static string OneLine(string? text) =>
        string.Join(" ", Lf(text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Lf(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
