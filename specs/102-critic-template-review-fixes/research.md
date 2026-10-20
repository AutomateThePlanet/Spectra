# Research: Review Fixes for 101 and the Critic Prompt Template

All facts verified on `26bfe88` (2026-10-20), unless marked as from Claude Code's docs (code.claude.com/docs/en/
sub-agents.md, tools-reference.md, permissions.md, skills.md, fetched 2026-10-20).

## R1 — F1: one critic prompt, from the template

**Finding**:
- `CriticPromptCompiler.Compile(test, documents, criteriaContext, templateLoader)` → `Assemble` → `new CriticPromptBuilder()`
  + `SetTemplateLoader(templateLoader)`. Both calls in `CompileCriticPromptCommand` (lines 108 and 255) pass neither, so
  `BuildSystemPrompt` returns the hard-coded raw string.
- The only production construction of `CriticPromptBuilder` is `CriticPromptCompiler.Assemble`. Tests construct it
  directly: `CriticPromptBuilderTests` (2 `BuildSystemPrompt` rows), `VerificationIntegrationTests` (4 rows, 1 on the
  system prompt), `CriticPromptCompilerTests` (via `Compile` / `Assemble`).
- The template body (comments aside) contains every line of the hard-coded string (`diff -w`: 0 lines only in the
  hard-coded text); it adds "Technique Verification" (037) and "Arithmetic Verification" (075).
- `PromptTemplateLoader.LoadTemplate`: the workspace `.spectra/prompts/<id>.md` if it parses and has a non-empty body,
  else the embedded built-in; throws only when the built-in is missing (guarded by `BuiltInTemplatesTests`).
  `PlaceholderResolver.Resolve` removes HTML comments, so the two `<!-- … -->` lines at the top of the body leave
  leading blank lines.

**Decision**:
- `CriticPromptBuilder` takes a `PromptTemplateLoader` in its constructor (required); `SetTemplateLoader` and the
  hard-coded string are removed. With a required loader the fallback is unreachable, so it goes (FR-003).
- `BuildSystemPrompt()` takes no argument: it loads `critic-verification` and resolves it with `test_case`,
  `source_document` and `acceptance_criteria` all empty (the values the loaded-template path passes today), then trims
  leading and trailing whitespace so the removed comments leave no blank head. (F6 lands with this: R6.)
- `CriticPromptCompiler.Compile(test, documents, templateLoader)` / `Assemble(test, documents, templateLoader)`: the
  loader is a required parameter.
- `CompileCriticPromptCommand` builds `new PromptTemplateLoader(currentDir)` once per run and passes it in both forms
  (suite and legacy path), so both doc modes and `--docs` get the same system part. Same rule as `CompilePromptCommand`
  and the other compile commands (workspace copy, else bundled).
- The tests that built the builder without a loader pass `new PromptTemplateLoader(<temp dir with no .spectra>)`, which
  gives the bundled template; their assertions ("verification expert", "grounded", verdict rules, output format) hold
  because the template contains the old text.

**Line endings**: the system part has the embedded resource's line endings, i.e. the checkout's (CRLF in a Windows
`core.autocrlf=true` checkout, LF elsewhere) — the same as today's hard-coded raw string, whose newlines are the source
file's. The user part uses `AppendLine`. No normalisation is added (out of scope); every new row compares through the
existing `CriticSourceWorkspace.Lf`.

**Alternatives considered**:
- Keep the hard-coded string as a fallback when the template can't be loaded: the loader already falls back to the
  embedded built-in, so a second fallback would be a second prompt that drifts (it did, twice). Rejected.
- Optional loader defaulting to `new PromptTemplateLoader(Directory.GetCurrentDirectory())` inside the builder: hides
  I/O in a pure builder and makes tests depend on the process directory. Rejected.

## R2 — F1: the golden and the template rows

**Finding**: `CriticSectionPromptTests` compares `--full-docs` output with `TestFixtures/CriticGolden/full-docs-6a4c41a.txt`
(the pre-101 output, LF). The prompt is `{system}\n\n---\n\n{user}`; the user part starts with `## Test Case to Verify`.
The `.gitattributes` `-text` rule covers only `tests/Spectra.CLI.Tests/Golden/099/**`; the critic golden is compared
after `Replace("\r\n", "\n")`.

**Decision**:
- Keep `full-docs-6a4c41a.txt` unchanged: it is the record of the pre-101 user part.
- Record `TestFixtures/CriticGolden/full-docs-102.txt` from the 102 build on the same golden workspace (human mode, CRLF
  → LF), a new golden beside the pre-101 record.
- Rows: (a) `--full-docs` output (LF) equals `full-docs-102.txt`; (b) the user part of the output — the text from
  `"\n\n---\n\n## Test Case to Verify"` on — equals the same slice of `full-docs-6a4c41a.txt` byte for byte;
  (c) the system part contains `## Technique Verification` and `## Arithmetic Verification`, in section mode,
  `--full-docs`, and the legacy `--test <path> --docs` form; (d) a workspace `.spectra/prompts/critic-verification.md`
  with valid frontmatter and a body holding `UNIQUECRITICMARKER` (and no "Technique Verification") → the marker is in
  the prompt and the bundled section is not, in both forms; (e) the same in `--output-format json`: stdout parses as
  one JSON document whose `prompts[0].prompt` holds the template text; (f) `spectra prompts validate` still passes on
  the bundled template (existing `PromptsCommandTests` row).
- Rows (a), (c), (d), (e) fail on `26bfe88`; (b) passes on both (it is the invariant).

**Alternative considered**: overwrite `full-docs-6a4c41a.txt` in place. Rejected: the name would lie, and the
"user part unchanged" assertion would have nothing to compare with.

## R3 — F2: one path comparer

**Finding**: `SourceRefResolver` has a private `PathComparison` (`Ordinal` on Linux, else `OrdinalIgnoreCase`) and builds
the matching `StringComparer` inside `Resolve` for `DistinctFullPaths`. `ResolvedSourceRef.FullPath` keeps the ref's
casing. `CriticSourceLoader` groups with `r.FullPath == fullPath`. Spec 087 tests the case-insensitive rule elsewhere
with a comparer seam plus `CaseInsensitiveGitFactAttribute` (skipped on Linux).

**Decision**:
- `SourceRefResolver` exposes `public static StringComparer PathComparer` (derived from `PathComparison`); `Resolve` uses
  it for `DistinctFullPaths`.
- `CriticSourceLoader.LoadAsync(refs, repoRoot, sourceDir, note, ct)` resolves, refuses as today, then calls an internal
  `LoadAsync(SourceRefResolution resolution, StringComparer comparer, Action<string>? note, CancellationToken ct)` that
  groups `resolution.Resolved` by `FullPath` with `comparer`, in first-reference order, and reads each document from its
  first ref's full path (the `DistinctFullPaths` entry, as today). The public overload passes
  `SourceRefResolver.PathComparer`.
- Rows: the internal overload with a hand-built resolution (two refs whose full paths differ only in case; the first
  spelling is the file on disk) and `StringComparer.OrdinalIgnoreCase` → both sections, once each, in order; with an
  anchor-less second ref → the whole file once. These rows run on every OS (the comparer is injected; only the first
  spelling is read). The case-sensitive (Linux) behaviour is unchanged and stays covered by the existing
  `CriticSourceLoaderTests`. One end-to-end `compile-critic-prompt` row on a case-insensitive file system
  (`docs/Auth.md#login` + `docs/auth.md#logout` → both sections), skipped on Linux by a new
  `CaseInsensitiveFsFactAttribute` (the 087 attribute without the git requirement).

**Alternative considered**: normalise `FullPath` to the on-disk casing in the resolver. Changes every resolver caller
(baseline, triage, coverage) for one consumer. Rejected.

## R4 — F3: unchanged repairs keep their verdict

**Finding**:
- `IngestUpdateCommand` removes verdict files only for `Written` outcomes (line 414; single form line 205).
- `IngestVerdictCommand --all --ids` reads `.spectra/verdicts/critic-verdict-<id>.json` and never deletes it; it lists a
  verdict whose test changed since its compile as `stale`.
- The repairer compiles a repair prompt (`compile-repair-prompt`), which checks freshness but writes no compile record.
  An unchanged repair writes no test file. So the verdict that sent the test to repair is still on disk and fresh.
- The shared verify block says: no critic final line → the `results` entry counts as agreeing.

**Decision**:
- `spectra-repairer` step 5: start the critic only for the final `written` ids. Pass `written` and `unchanged` ids to the
  one `ingest-verdict --suite <s> --all --ids …` call. An unchanged id has no critic line, so its line is
  `<id> | verdict <v> <score>` from `results`. No `results` entry → the existing retry ("start the critic once more",
  here its first run) → else `unverified: <reason>`. `verify: no` → unchanged (no step 5).
- `critic: not run` (no Agent tool): the skills' fallback applies the same rule — the critic only for `written` ids, the
  `unchanged` ids to the same ingest.
- `spectra-generate` 8b, the re-critic routing and the `--repaired --ids` list: unchanged ids are routed by their verdict
  lines like written ones (`verdict partial` → flagged-partial; `verdict hallucinated` → `record-drop` + `delete` as
  row 3). `{ids}` stays "written or unchanged, minus dropped, never failed"; the "re-critiqued" wording becomes
  "re-verified".
- `spectra-update` repair section: same; "Spec 091: `--ids` names exactly the re-critiqued tests" becomes "the
  re-verified tests". Still partial → flagged for review; still hallucinated → the user's choice (unchanged).
- Size (analysis C4, superseding C1–C3): the routing stays in SKILL.md, because reference.md is never read during Steps
  7–9 (SKILL.md lines 19 and 37; Spec 101's design: Steps 7–9 must survive compaction whole). The cap stays: it comes
  from the 5,000 tokens re-attached after compaction.
  - Numbers, measured on an in-memory copy (`SkillContent.Generate`, rendered, LF):

    | Point | Characters |
    |---|---|
    | Base | 15,850 |
    | After freeing first (−128 Key rule, −86 annotations) | 15,636 |
    | After T018 (+140) | 15,776 |
    | After T022 (+9) | 15,785 |
    | After T029 (+6) | 15,791 |
    | Target | ≤ 15,900 (≈ 209 characters of headroom) |

    The first wording (T018 ≈ +165) would have reached ≈ 16,030, over the cap.
  - Freeing comes first, inside T018, so the skill never goes over 16,000 at T020, T023 or T031:
    - (a) the Key rule becomes one line, "a single test case title → `--from-description`; a topic to explore → the main
      flow. Never ask about count or scope to decide.", and its explanation moves to reference.md's "Test Creation
      Intent Routing" (the table stays);
    - (b) the five "(Spec …)" annotations go. They are not instructions; this is an explicit exception to "never
      remove anything from Steps 7–9".
  - "MANDATORY: Analyze first" is not touched: every line left in it is a decision point.
  - T018's routing edits:
    - 8b: "verify each `written` repair (an `unchanged` one keeps its verdict)";
    - the re-verify bullet: "one without a verdict line, or `critic: not run`, goes through the fallback — the critic
      for `written` ids only, `unchanged` ids straight to its ingest call";
    - "re-critiqued" → "re-verified".

    No separate sentence: the repairer still returns a verdict line for an `unchanged` repair.
  - T020, T023 and T031 measure and record the size. T031 applies no freeing step; over 15,900 there means the
    estimate was wrong, so stop and report.
- 101 FR-026 is changed for unchanged repairs only (spec Decisions; CHANGELOG).

**Alternative considered**: drop unchanged ids from the verdict ingest and route them in the skill as "unchanged
partial". Needs a new line kind and a new routing row. Rejected; the existing verdict line form already covers it.

## R5 — F4: a keyword on every line kind

**Finding**: `spectra-generator` returns `<id> | <title>` (line 106; example lines 113–114), pinned by
`GenerateAgentTests` (`"`<id> | <title>`"`). `spectra-generate` Step 7 lists it (line 76) and Step 9 reports by id and
title. The from-description flow ingests in the main session (`ingest-tests`) and reads no generator line: unaffected.
The updater and repairer have no title line.

**Decision**: `<id> | created: <title>` in the generator's contract and example. `spectra-generate` Step 7 says "one
`<id> | created: <title>` line per created test" (≈ +9 characters), and nothing else changes in the skill (analysis C3).
The next bullet already lists every verdict-line form, and with `created:` no title line can be read as one. Step 9
needs no change, and nothing goes to reference.md, which is never read during Steps 7–9. The updater's
`<id> | written (manual verdict kept)` and `<id> | unchanged_pending <BUCKET>: <why>` already start with a keyword and
stay. Verdict lines unchanged. Rows: the generator contract and
example use `created:`; no line form in any work agent is `<id> | <free text>` without a keyword; the skill's Step 7 list
names `created:`.

## R6 — F6: the criteria parameter

**Decision**: lands with R1 — `criteriaContext` leaves `Compile`, `Assemble` and `BuildSystemPrompt`; the
`acceptance_criteria` placeholder resolves to empty inside `BuildSystemPrompt`. `using Spectra.CLI.Criteria;` leaves
`CompileCriticPromptCommand.cs` (0-warning build confirms nothing else uses it). The type-level check is the compiler;
no extra row.

## R7 — F5 and F7: `CriticSourceLoader.Content`

**Finding**: per document with all anchors matched, `Content` calls `Match` n times, then for each ref i
`CoveredHeadings(headings, anchors[j])` for every j (each calls `Match` again: n² calls), then for each kept section
`SectionExtractor.Extract(text, headings, anchor)` (another `Match` and a fresh `LineStarts` scan each). The ambiguity
warning is emitted before the absorption test.

**Decision**:
- Match once per anchor: `matches[i]` (n calls). Covered range per match: `start = matches[i].Index`,
  `end` = the first heading index after `start` whose level is ≤ `headings[start].Level`, else `headings.Count`.
  Ref i is absorbed iff some j has `start_j < start_i < end_j` — the same set `CoveredHeadings` gives (its list is
  `start_j` plus every k with `start_j < k < end_j`).
- Slice once: `lineStarts` computed once per document; section text =
  `text[lineStarts[headings[start].StartLine] .. (end < headings.Count ? lineStarts[headings[end].StartLine] : text.Length)]`,
  the same bounds `Extract` uses, then `TrimEnd('\n')` as today.
- `SectionExtractor.LineStarts` becomes `internal` (Core already has `InternalsVisibleTo` Spectra.CLI and the test
  projects); no other Core change.
- Counting seam: `Content` becomes `internal static string Content(string raw, IReadOnlyList<string> refs,
  Action<string>? note, Func<IReadOnlyList<MarkdownHeading>, string?, HeadingMatch?>? match = null,
  Func<string, IReadOnlyList<int>>? lineStarts = null)`, defaulting to `SectionExtractor.Match` / `LineStarts`. A row
  with 6 anchored refs to one document asserts `match` was called 6 times and `lineStarts` once (26bfe88: 6 + 36 + kept).
- F5: the warning is emitted after the kept set is known, for each ref (once per ref text, in ref order) whose match
  is ambiguous and whose section is kept. Rows: an ambiguous anchor whose section is kept → one warning; an ambiguous
  anchor inside another cited section → no warning.
- The existing `CriticSourceLoaderTests` and the golden rows stay green unchanged (same output).

## R8 — F8: the shared critic block

**Finding**: the three work agents carry the same text from "`` `spectra-critic` subagent with the Agent tool, passing
only `` …" through "routes each test by your verdict line." Before it, a step heading paragraph differs: the step number
(generator 4, repairer 5, updater 6), which ids are verified, and the `verify:` rule (generator: none; repairer: "Skip it
with `verify: no`"; updater: "Only with `verify: yes`").

**Decision**: a content row extracts, from each agent, the text from the `` **<n>. Verify with the critic** `` heading to
"routes each test by your verdict line.", splits it at "`spectra-critic` subagent with the Agent tool" into a prefix and
the shared block, and asserts:
- the shared blocks are identical (after CRLF → LF) in all three;
- each prefix is the listed difference for its agent: its step number, its id selection (generator: "each id in
  `ids`"; repairer: "your final `written` lines" plus the R4 sentence on `unchanged` ids; updater: "your final `written`
  lines", with its exclusions) and its `verify:` rule.
The list lives in the test as a table, so a new difference has to be added on purpose.

## R9 — F9: the subagent tool's name

**Finding (Claude Code docs)**: the sub-agents page says the Task tool was renamed to Agent in v2.1.63 and that existing
`Task(...)` references in settings and agent definitions still work as aliases; the tools reference lists only `Agent`;
the permissions page documents `Agent(name)` rules; no page announces removal of `Task`. The skills page doesn't name
either tool for `allowed-tools`/`tools`.

**Finding (repo)**: `SkillResourceLoader.GenerateToolsList = "Read, Write, Edit, Bash, Glob, Grep, Task"` renders
`{{GENERATE_TOOLS}}` in `spectra-generate`, `spectra-update`, `spectra-criteria` and `spectra-review-flagged`; its comment
says "Task (to invoke the context:fork critic subagent)". Skill text says "with the Task tool" (generate ×4, update ×3,
criteria ×1) and "Task calls" (generate line 37, update lines 41 and 49). Agents say "(from the Task prompt)" (5) and
"no Task calls" (analyst, extractor). `CompileCriticPromptCommand`'s doc comment still says "the `spectra-critic`
`context: fork` subagent". The `| Task | SKILL | CLI command |` table headers in `spectra-execute` and the generate
reference mean a user task: not the tool.

**Decision**: `Agent` everywhere the tool is meant: `GenerateToolsList` ends in `Agent`; "with the Agent tool", "Agent
calls", "(from the Agent call's prompt)", "no Agent calls"; the loader comment says the authoring skills use Agent to
start SPECTRA's subagents (the critic among them); the command's doc comment drops `context: fork`. Pinning rows move
from "Task" to "Agent": `GenerationSkillContractTests` (62–63), `GenerateOrchestratorTests` (43), `OrchestratorSkillTests`
(39), `SkillsManifestTests` (668). A new guard: no bundled skill, reference or agent says "Task tool", "Task call" or
"Task prompt", and the four skills' rendered `tools:` contain `Agent` and not `Task`.

**Installed files**: the four rendered SKILL.md files and the five agents change, so `update-skills` refreshes unedited
copies and keeps edited ones (096). The existing `UpdateSkillsKeepsEditsTests` / `UpdateSkillsReportTests` rows are
content-agnostic (they compare against whatever is bundled); they are run, not changed, and the tasks' Notes list them.
`spectra-review-flagged` changes only through the rendered `tools:` line — no behaviour change (its out-of-scope rule
stands).

## R10 — Docs

- `docs/customization.md` line 93: unchanged (already says `compile-critic-prompt` uses the template — now true); line
  353: the template edit is used by `compile-critic-prompt`, and the critic model is `ai.critic.model`
  (`spectra config ai.critic.model <model>`, then `spectra update-skills`), not the agent's `model:`.
- `docs/cli-reference.md` (`compile-critic-prompt`): the system part comes from the `critic-verification` template
  (workspace copy, else bundled), in every form and mode.
- `docs/grounding-verification.md` "How It Works": the critic's instructions come from the template; after a repair,
  only a changed test is judged again.
- `CHANGELOG.md`: a "Spec 102" section after 101's, including the FR-026 change; CLAUDE.md Recent Changes row.

## Baseline

Measured at tasks time (T001) on this branch (= `26bfe88` plus spec docs). The last recorded totals (101 Notes): Core
1228 / CLI 2438 / Execution 228 / Integration 8, 0 failures.
