using System.Text;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 101 (T002): documents and tests for the section-scoped critic input over a <see cref="BaselineTestWorkspace"/>.
/// All text is built here with <c>\n</c> (never read from checked-out files), so the rows don't depend on the checkout's
/// line endings (099 lesson). Uses only APIs that exist on <c>6a4c41a</c>, so the proof rows compile against it.
/// </summary>
public static class CriticSourceWorkspace
{
    public const string LongDoc = "docs/long.md";
    public const string ShortDoc = "docs/short.md";
    public const string Suite = "s";

    /// <summary>The golden <c>--full-docs</c> prompt captured from the <c>6a4c41a</c> build (T003), LF line endings.</summary>
    public const string GoldenFile = "TestFixtures/CriticGolden/full-docs-6a4c41a.txt";

    /// <summary>The refs of the golden test TC-001.</summary>
    public static readonly string[] GoldenRefs = [LongDoc + "#section-30", LongDoc + "#section-2", ShortDoc];

    /// <summary>
    /// <c># Feature</c>, an intro line, then <c>## Section 1</c> … <c>## Section N</c>, five lines each, and
    /// <c>### Sub 12.1</c> under Section 12. Section 30 starts well after the 8,000th character.
    /// </summary>
    public static string LongDocText(int sections = 40)
    {
        var sb = new StringBuilder("# Feature\n\nIntro line of the feature document.\n");
        for (var i = 1; i <= sections; i++)
        {
            sb.Append($"\n## Section {i}\n\n");
            for (var j = 1; j <= 5; j++)
            {
                sb.Append($"Section {i} paragraph line {j} with some filler words to take space.\n");
            }

            if (i == 12)
            {
                sb.Append("\n### Sub 12.1\n\nSub 12.1 detail line.\n");
            }
        }

        return sb.ToString();
    }

    public const string ShortDocText = "# Short\n\nShort document body line.\n";

    /// <summary>Writes <c>docs/long.md</c> and <c>docs/short.md</c>.</summary>
    public static void WriteCriticDocs(this BaselineTestWorkspace ws)
    {
        ws.WriteDoc(LongDoc, LongDocText());
        ws.WriteDoc(ShortDoc, ShortDocText);
    }

    /// <summary>A test in suite <see cref="Suite"/> citing <paramref name="refs"/>, with its <c>_index.json</c> entry.</summary>
    public static string CitingTest(this BaselineTestWorkspace ws, string id, params string[] refs) =>
        ws.WriteTest(Suite, id, refs, title: "Check the cited section");

    /// <summary>A criteria file that isn't valid YAML.</summary>
    public static void MalformedCriteria(this BaselineTestWorkspace ws) =>
        ws.WriteCriteriaFile("x.criteria.yaml", "criteria: [unclosed\n  - : : :\n\t- broken\n");

    /// <summary>A criteria file whose text holds a marker that must never reach the critic prompt.</summary>
    public static void MarkerCriteria(this BaselineTestWorkspace ws) =>
        ws.WriteCriteria("long.criteria.yaml", ("AC-LONG-001", "UNIQUECRITERIONMARKER the system MUST show section 30"));

    /// <summary>
    /// The golden workspace (T003): the two documents and TC-001 citing <see cref="GoldenRefs"/>. The golden file was
    /// made by running <c>spectra ai compile-critic-prompt --suite s --test TC-001</c> (human mode) on this workspace
    /// with the <c>6a4c41a</c> build and saving stdout with CRLF replaced by LF.
    /// </summary>
    public static void GoldenWorkspace(this BaselineTestWorkspace ws)
    {
        ws.WriteCriticDocs();
        ws.CitingTest("TC-001", GoldenRefs);
    }

    /// <summary>The golden text, LF line endings.</summary>
    public static string Golden() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, GoldenFile)).Replace("\r\n", "\n");

    public static string Lf(string text) => text.Replace("\r\n", "\n");

    /// <summary>Spec 102 (R2): the 102 <c>--full-docs</c> golden — the template's system part, the unchanged user part.</summary>
    public const string Golden102File = "TestFixtures/CriticGolden/full-docs-102.txt";

    /// <summary>The 102 golden text, LF line endings.</summary>
    public static string Golden102() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, Golden102File)).Replace("\r\n", "\n");

    /// <summary>Where a critic prompt's user part starts (after the <c>{system}\n\n---\n\n</c> join).</summary>
    public const string UserPartMarker = "\n\n---\n\n## Test Case to Verify";

    /// <summary>The user part of a critic prompt (LF): the test and its documents, from <see cref="UserPartMarker"/> on.</summary>
    public static string UserPart(string prompt)
    {
        var at = prompt.IndexOf(UserPartMarker, StringComparison.Ordinal);
        if (at < 0) throw new InvalidOperationException("The prompt has no user part.");
        return prompt[at..];
    }

    /// <summary>
    /// Spec 102: writes <c>.spectra/prompts/critic-verification.md</c> with the bundled frontmatter (the three declared
    /// placeholders) and <paramref name="body"/>, LF.
    /// </summary>
    public static void WriteCriticTemplate(this BaselineTestWorkspace ws, string body) =>
        ws.WriteDoc(".spectra/prompts/critic-verification.md",
            "---\n" +
            "spectra_version: \"1.0\"\n" +
            "template_id: critic-verification\n" +
            "description: \"How SPECTRA's critic verifies generated tests against source documentation\"\n" +
            "placeholders:\n" +
            "  - name: test_case\n    description: \"Generated test case (full markdown)\"\n" +
            "  - name: source_document\n    description: \"Source documentation the test references\"\n" +
            "  - name: acceptance_criteria\n    description: \"Linked acceptance criteria\"\n" +
            "---\n\n" + body + "\n");
}
