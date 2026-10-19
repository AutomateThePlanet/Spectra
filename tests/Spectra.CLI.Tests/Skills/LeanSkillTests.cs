using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 101 F2 (FR-009–FR-011; US3): both authoring SKILL.md bodies fit within the 5,000 tokens Claude Code re-attaches
/// after compaction (≤ 16,000 characters each), link their <c>reference.md</c>, and keep the NON-STOP contract and the 075
/// guard verbatim, with every question and STOP. On 6a4c41a the skills were 37,176 and 22,914 characters with no
/// reference file. The verbatim and decision-point rows are guards (copies of the 6a4c41a text; they pass on both).
/// </summary>
public sealed class LeanSkillTests
{
    private const int Cap = 16_000;

    private const string GenerateNonStop6a4c41a = """
        ## NON-STOP CONTRACT — Steps 7–9

        **Every step in this section must be a single `Bash(spectra *)` call or a `Write` to a spectra-authored path.** If no single `spectra` call covers a step, **STOP and report a missing affordance** — never work around it.

        **Prohibited behaviors (each is a missing-affordance signal, not a license to improvise):**
        - **(a) Shell loops over per-test verbs** — `for id in …; do spectra …; done` — NEVER. Use the corresponding `--all` batch verb.
        - **(b) Piping spectra output to any interpreter** — `python -c "…"`, `jq`, `node -e`, or any data-transform shell — NEVER. Read the output file with the Read tool and iterate in-context.
        - **(c) Manual `.md` file editing** — rewriting test content directly instead of using `ingest-update` — NEVER.
        - **(d) Verdict / grounding field rewrites by hand** — writing `grounding:`, `verdict:`, or `score:` frontmatter directly instead of using `ingest-grounding` / `record-drop` — NEVER.

        **Never accept a "scripting for all projects" allowlist option** — that is the inverse of this contract. The per-seam guards below (e.g., the grounding `written:0` check) are specific instances of this general rule.
        """;

    private const string GenerateGuard6a4c41a = """
        - **If `written == 0` AND `kept-grounded > 0`:** STOP immediately. Do NOT proceed to 8b. Emit a diagnostic:
          > GROUNDING-INGEST ANOMALY: `ingest-grounding --all` returned `written: 0` but {kept-grounded} tests were marked grounded in the critic loop. This means the grounding blocks were not written. Likely causes: `_index.json` file paths are suite-prefixed (path-doubling bug) or the suite directory could not be resolved. **STOP — do not proceed to 8b, do not edit `.md` files by hand, do not rewrite verdict fields by hand.** Run `spectra ai audit-grounding --suite {suite}` to diagnose the current grounding state, then report the anomaly to the user and halt.

        - **If `written != kept-grounded`** (partial write — `written > 0` but less than expected): Surface a warning before continuing:
          > GROUNDING-INGEST WARNING: expected {kept-grounded} grounding blocks written, got {written}. Continuing to 8b — run `spectra ai audit-grounding --suite {suite}` to inspect which tests are missing their grounding block.

        - **If `written == kept-grounded`** (or `kept-grounded == 0`): Proceed to 8b normally.
        """;

    private const string GenerateNonStopRestated6a4c41a = """
        **NON-STOP CONTRACT (restated):** A broken or zero-result CLI verb is a STOP signal — it is NEVER a license to do the work by hand. Manual editing of `.md` files, rewriting of `grounding:` frontmatter, and synthesizing verdict fields are PROHIBITED regardless of how blocked you are. If a CLI verb returns an anomaly, stop and report. Do not improvise. Exit `8` from `ingest-update --all` is neither broken nor zero-result: the batch completed and `results` lists the failed files — retry them as 8b describes.
        """;

    private const string UpdateNonStop6a4c41a = """
        ## NON-STOP CONTRACT

        Every step below is a single `Bash(spectra *)` call, a Task call to the `spectra-updater`,
        `spectra-repairer` or `spectra-critic` subagent, or a `Write` only to `.spectra/progress.json`. If no
        single `spectra` call covers a step, **STOP and report a missing affordance** — never work around it.
        Prohibited: shell loops over per-test verbs, piping spectra output to `python` / `jq` / any
        interpreter, reading docs, prompts, tests, staging or verdict files, editing a test's `.md` file
        yourself, and rewriting a baseline file (`_source-baseline.json`) by hand.
        """;

    /// <summary>Every user question and STOP of the 6a4c41a generate skill (whitespace-normalised substrings).</summary>
    public static TheoryData<string> GenerateDecisionPoints => new()
    {
        "Present the recommendation and STOP",
        "Shall I proceed?",
        "**STOP. Wait for user approval.**",
        "If `recommended` is 0, tell the user every identified behavior is already covered and do not generate.",
        "**STOP and report a missing affordance**",
        "Do NOT ask the user how many test cases they want",
        "offer once to re-run the analysis",
    };

    /// <summary>Every user question and STOP of the 6a4c41a update skill (whitespace-normalised substrings).</summary>
    public static TheoryData<string> UpdateDecisionPoints => new()
    {
        "**STOP and report a missing affordance**",
        "say \"Tests are up to date\" and STOP.",
        "Ask the user whether to relink these tests. Relink only after the user says yes.",
        "Ask the user which buckets to process",
        "**report only** — no edits; STOP after the report.",
        "ask the user, per group, whether to **acknowledge** it",
        "Never acknowledge without the user's answer.",
        "\"Re-verify the edited tests with the critic now? (recommended)\"",
        "**still hallucinated** → stop for that test and ask the user to choose",
    };

    private static string Generate => SkillContent.Generate.Replace("\r\n", "\n");

    private static string Update => SkillContent.Update.Replace("\r\n", "\n");

    private static string Normalised(string text) => Regex.Replace(text, @"\s+", " ");

    private static string Copy(string text) => text.Replace("\r\n", "\n").Trim();

    [Fact]
    public void TheGenerateSkill_FitsTheCap()
    {
        Assert.True(Generate.Length <= Cap, $"spectra-generate SKILL.md is {Generate.Length} characters (cap {Cap})");
    }

    [Fact]
    public void TheUpdateSkill_FitsTheCap()
    {
        Assert.True(Update.Length <= Cap, $"spectra-update SKILL.md is {Update.Length} characters (cap {Cap})");
    }

    [Theory]
    [InlineData("spectra-generate")]
    [InlineData("spectra-update")]
    public void EachSkill_LinksItsReferenceFile_AndSaysWhenToReadIt(string skill)
    {
        var text = SkillContent.All[skill].Replace("\r\n", "\n");
        Assert.Contains("[reference.md](reference.md)", text);
        Assert.Contains("## Additional resources", text);
        var resources = text[text.IndexOf("## Additional resources", StringComparison.Ordinal)..];
        Assert.Contains("read", resources, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheGenerateSkill_KeepsTheNonStopContract_Verbatim()
    {
        Assert.Contains(Copy(GenerateNonStop6a4c41a), Generate);
        Assert.Contains(Copy(GenerateNonStopRestated6a4c41a), Generate);
    }

    [Fact]
    public void TheGenerateSkill_KeepsThe075Guard_Verbatim()
    {
        Assert.Contains(Copy(GenerateGuard6a4c41a), Generate);
    }

    [Fact]
    public void TheUpdateSkill_KeepsTheNonStopContract_Verbatim()
    {
        Assert.Contains(Copy(UpdateNonStop6a4c41a), Update);
    }

    [Theory]
    [MemberData(nameof(GenerateDecisionPoints))]
    public void TheGenerateSkill_KeepsEveryDecisionPoint(string point)
    {
        Assert.Contains(Normalised(point), Normalised(Generate));
    }

    [Theory]
    [MemberData(nameof(UpdateDecisionPoints))]
    public void TheUpdateSkill_KeepsEveryDecisionPoint(string point)
    {
        Assert.Contains(Normalised(point), Normalised(Update));
    }
}
