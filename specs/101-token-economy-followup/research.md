# Research: Token Economy Follow-up

All facts verified on `6a4c41a`, unless marked as from Claude Code's docs (code.claude.com/docs/en/sub-agents.md and
skills.md, fetched 2026-10-19).

## R0 — What the critic sees today (corrects the spec's first draft)

**Finding**:
- `CompileCriticPromptCommand.RunSuiteAsync` resolves `source_refs` through `SourceRefDocumentLoader` (whole files, one
  per distinct file, first-reference order; a missing file is exit 1).
- `CriticPromptBuilder.BuildUserPrompt` then keeps at most 5 documents (`MaxDocuments`, referenced ones first) and cuts
  each at 8,000 characters (`MaxDocumentChars`, at a paragraph break past half, plus `[... content truncated ...]`).
- `CriteriaContextLoader.LoadCriteriaContextAsync` runs (line 202), and its result goes to `CriticPromptCompiler.Compile`.
  But no `PromptTemplateLoader` is passed, so `BuildSystemPrompt` returns the built-in text and ignores the criteria.
  The `critic-verification` template is never loaded by this command.
- Probe (built CLI, scratch workspace): a 14 KB document with 40 `##` sections, and a test citing `#section-30`. The
  prompt was 9,662 characters, held the document up to Section 23 and then the cut marker, and held no criterion text.

**Consequences** (resolved with the user, spec Clarifications):
- Section mode changes only the documents; the dead criteria load goes (FR-003). `CriteriaContextLoader` has no
  try/catch, so a malformed criteria file could fail the command today; after the change it can't.
- The cut stays in both modes. `--full-docs` stays byte-identical (the cut and the 5-document limit included).
- The gate is split into "seen" and "cut" tests (spec US2), because the cut can hide the cited section in full-docs mode.

## R1 — Assembling the cited sections

**Decision**: A new `CriticSourceLoader` (in `src/Spectra.CLI/Source/`, beside `SourceRefDocumentLoader`) returns the
same `SourceDocument` list shape, one item per document, so `CriticPromptBuilder` (the cut, the 5-document limit, the
rendering) is unchanged.
- Resolution: the same `SourceRefResolver(repoRoot, source.local_dir ?? "docs")` and the same missing-ref refusal
  (`SourceRefDocumentLoader.MissingMessage`, `FileNotFoundException`, exit 1) as today.
- Grouping: refs grouped by resolved file, documents in first-reference order (as today).
- Per document:
  - any anchor-less ref → the whole file, read exactly as today (`File.ReadAllTextAsync`, no normalisation), so a
    whole-file ref gives the same bytes in both modes;
  - otherwise, for each anchor: `SectionExtractor.Headings` and `SectionExtractor.Match` over the normalised text (the
    085 matcher, ambiguous → first heading);
  - any anchor with no match → the whole file, with the note as the first line of the content:
    `> Note: the anchor "#<anchor>" was not found in this document, so the whole document is given.` One line per
    unmatched anchor, in ref order;
  - all anchors matched → each section's raw text (`SectionExtractor.Extract`, heading included, subsections included),
    in first-reference order, joined by one blank line. A section that another cited section of the same document
    covers (`SectionExtractor.CoveredHeadings`) is absorbed (094's rule). Two refs matching the same heading count once.
- `Title` and `Path` are as today (file name without extension; relative path), so `SelectRelevantDocuments` still puts
  referenced documents first.
- An ambiguous anchor writes the same note text `BaselineContext` uses to stderr (`<ref>: more than one heading shares
  the anchor key '<key>'; the first was used`). The prompt is unchanged by it.

**Rationale**: Reusing the builder keeps one rendering and one cut. Using `SectionExtractor` directly, not
`BaselineContext`, avoids `BaselineContext.CreateAsync`'s criteria scan, which the critic doesn't need.

**Alternatives considered**:
- One `SourceDocument` per section: the 5-item limit would then drop sections of a test citing six. Rejected.
- `BaselineContext.GetRawSectionAsync`: correct, but it scans every criteria file per compile. Rejected for cost.
- Extracting 094's absorption code from `CompileBatchPrompt` into a shared helper: its loop is keyed to batch plan
  positions. The rule is small and `SectionExtractor.CoveredHeadings` already holds the logic. Rejected (YAGNI); a row
  checks that both give the same section set for the same refs.

## R2 — `--full-docs` and the other paths

**Decision**:
- `compile-critic-prompt` gains `--full-docs` (bool).
- Suite mode without `--docs`: the default is `CriticSourceLoader`; with `--full-docs` it is `SourceRefDocumentLoader`
  (today's code path, unchanged).
- `--docs <path>`: unchanged in both modes; `--full-docs` is accepted and changes nothing.
- Legacy path form (`--test <path>` without `--suite`): unchanged; `--full-docs` is accepted and changes nothing.
- Freshness record (`RecordAsync`): unchanged and written in both modes (the fingerprint covers the test only).
- JSON shape (`{prompts:[{id,prompt}]}` / `{prompt}`), refusals, exit codes: unchanged.

**Rationale**: The flag selects one of two loaders; everything around them stays.

## R3 — The critic agent's `full_docs` input

**Decision**: `spectra-critic.agent.md` says: when the caller passes `full_docs: true`, add `--full-docs` to the compile
call. The skills never pass it. The quickstart's gate uses it for the comparison run.

## R4 — Nested subagents in Claude Code (from the docs)

**Findings**:
- "By default, a subagent can spawn subagents of its own, up to three layers below the main conversation."
  `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1` turns nesting off.
- At the limit, a (non-fork) subagent doesn't get the `Agent` tool; a fork keeps it and gets an error.
- The tool is `Agent` since 2.1.63; `Task` still works as an alias.
- `Agent(type, …)` in a subagent's `tools` is ignored ("any type list inside the parentheses is ignored"); it only
  restricts `claude --agent` main threads.
- The depth used: main session → `spectra-generator` / `spectra-updater` / `spectra-repairer` (layer 1) → `spectra-critic`
  (layer 2). Within the default.

**Decision**:
- The three work agents get `tools: [Bash, Read, Write, Agent]`.
- They detect "can't start the critic" as: no `Agent` tool in their tool list, or the Agent call returns an error
  before the critic ran. Then they return `critic: not run` (contract C3).
- A critic that started but failed for one test (refused compile, damaged verdict) goes through the one retry and
  comes back `unverified: <reason>`.

**Alternatives considered**: `Agent(spectra-critic)` to pin the spawnable type. Ignored in subagents, so it would claim
a restriction that doesn't exist. Rejected.

## R5 — Routing contract between the work agents and the skills

**Decision**: Contract C3. The work agent runs, per test it wrote:
1. the critic (fresh context, suite + id, no hand-built path);
2. after the critic loop, one `spectra ai ingest-verdict --suite S --all --ids <ids> --output-format json`;
3. today's retry for ids with no `results` entry (the critic once more, the ingest for those ids again);
4. one verdict line per id.

`ingest-verdict` only reads and classifies (097), so moving it into the subagent moves no write. `record-drop`,
`spectra delete` and `ingest-grounding` stay in the main session. `ingest-grounding` reads the verdict files itself and
writes blocks only for fresh grounded verdicts, so a wrongly relayed line can't create a false block.

**The update flow's re-verify question** (spec decision): asked once before the first updater call; `verify: yes|no` goes
to every updater and repairer call. With `no`, they run no critic and return no verdict lines, and the skill reports the
`written` tests as unverified (as today after "no").

**Generation's progress page**: the phases stay. During Steps 5–7 the main session shows the batch loop, as today. Step
8 becomes the routing of the returned lines (no per-test loop, except on the fallback path).

## R6 — Skill reference files: packaging and install

**Findings**:
- `Spectra.CLI.csproj:55-57` embeds `Skills\Content\Skills\*.md`, `Agents\*.md`, `Profiles\*.yaml` (not recursive).
- `SkillResourceLoader` turns every resource under `Spectra.CLI.Skills.Content.Skills.` into a skill. A file like
  `spectra-generate.reference.md` there would install as a 17th skill; a subfolder isn't embedded.
- `SkillInstallLayout` knows `SKILL.md` and agent paths only.
- `ManifestKey.InstallTail` maps foreign (moved-workspace) keys only for `.claude/skills/<n>/SKILL.md` and five other
  shapes. A `reference.md` key would be dropped and trigger the "entries from another location were dropped" warning
  (`UpdateSkillsReportTests.AMovedWorkspace_ReportsNoWarning`).
- `UpdateSkillsHandler.ProcessFileAsync` (096 rules): a missing file is written and listed under "Updated" (there is no
  "Added" bucket); no record + same hash → unchanged; no record + different → "skipped (no record…)"; record but no
  candidate match → "Skipped (customized by user)"; record matches and bundled changed → overwritten, "Updated".
- `InitHandler.CreateBundledSkillFilesAsync` installs skills and agents, records hashes, and is skipped by
  `--skip-skills`.

**Decision**:
- Source: `src/Spectra.CLI/Skills/Content/References/<skill>.md` (one reference file per skill that needs one), embedded
  by a new glob line, loaded by `SkillResourceLoader.GetAllReferences()` under a new prefix
  `Spectra.CLI.Skills.Content.References.`, with the same `{{…TOOLS}}` replacement. Exposed as
  `SkillReferenceContent.All` (skill name → content).
- Install path: `SkillInstallLayout.ReferencePath(root, skill)` = `.claude/skills/<skill>/reference.md`.
- `init` (unless `--skip-skills`) and `update-skills` process them right after the skills, with the same
  `ProcessFileAsync` / record rules. The report wording is unchanged.
- `ManifestKey.InstallTail` learns `.claude/skills/<n>/reference.md`.
- SKILL.md links it as `[reference.md](reference.md)` (the docs' convention), with a line naming each part and when to
  read it.

**Alternatives considered**:
- A recursive glob with `LogicalName`: more MSBuild machinery for the same result. Rejected.
- Several reference files per skill: one is enough for both skills now. Rejected (YAGNI).

## R7 — What moves out of each SKILL.md

Measured section sizes on `6a4c41a` (characters, heading to next `##`):

| spectra-generate (37,176 total) | chars | Plan |
|---|---|---|
| Frontmatter + intro (to "IMPORTANT RULES") | ~3,590 | keep; trim the ISTQB / boundary-gap paragraphs to one line each (detail → reference) |
| IMPORTANT RULES; Analyze first | 1,603 | keep |
| CLI flags reference (+ pre-flight check) | 6,195 | → reference (only subagents run these commands) |
| Context budget contract | 824 | keep |
| Main generation flow; After user approves | 5,579 | keep (decision points, STOP at Step 4) |
| NON-STOP CONTRACT — Steps 7–9 | 1,212 | keep verbatim |
| Critic + Step 8 + Step 9 | 11,717 | keep routing, 075 guard verbatim, repair routing, report; per-test critic procedure → work agents (F3); the main-session fallback procedure → reference |
| From-description flow | 1,691 | keep a short version of Steps 1–5; the detail → reference |
| Intent routing; choosing flows | 2,352 | keep a short routing table (the decision); examples → reference |
| Other tasks (delegation); Notes | 2,413 | → reference |

Target: ≤ 16,000.

| spectra-update (22,914 total) | chars | Plan |
|---|---|---|
| Frontmatter + intro, context budget, NON-STOP | ~2,700 | keep (NON-STOP verbatim) |
| Update test cases for a suite | 15,942 | keep every step, question and STOP; Step 5a per-test critic procedure → work agents (F3); fallback procedure → reference |
| Update all suites; Stop | 831 | keep |
| Exit codes; Buckets; Suggest next steps; Example requests | 2,517 | → reference |
| Step-by-step detail that repeats CLI output fields or long examples | ~2,000 | → reference (needed for the 16,000 cap) |

Target: ≤ 16,000 (was 18,000; analysis remediation H1).

The guard counts characters of the embedded text (CRLF normalised). At 3.5 characters per token, 16,000 characters is
≈ 4,600 tokens. The token count of each SKILL.md is also measured once by hand (`/context` right after invoking the
skill in a fresh session, or the API's token count) and must be under 5,000; if either is over, more reference
material moves out.

## R8 — Tests that pin moved text (FR-013 inventory, to retarget)

| Test | What it pins | Retarget to |
|---|---|---|
| `GenerateSkillContentTests` :28, :59, :67, :75, :83, :93-94, :104-105 | intent routing / choosing flows text | the reference content, or the kept short routing table |
| `SkillsManifestTests` :329, :393-401 | `spectra-update` / `update-triage` delegation row | reference |
| `SkillsManifestTests` :575-578 | CLI-flags row for `ingest-update … --keep-baseline` | reference |
| `GenerateOrchestratorTests` :143 | "## Test Creation Intent Routing" as a slice end marker | the new next heading |
| `SkillsManifestTests` :415-428 | `CONFIG_MISSING`, `CONFIG_INVALID`, `LOCK_TIMEOUT` in the update skill | update reference |
| `SkillsManifestTests` :776 | `## Buckets` slice holding `- **RELINK**` | update reference |
| `OrchestratorSkillTests` :187 | "## Exit codes" as a slice end marker | the new next heading |
| `ManifestNewestTests` :54-56 | `IsSkipped` = "spectra-generate" after the Skipped header | match the SKILL.md path, not the bare name |
| `GenerateAgentTests` :33-39, `OrchestratorAgentTests` :42-51 | tools line `[Bash, Read, Write]`, `disable-model-invocation`, `context: fork` | new tools lines; the allowed-field guard (F4) |
| `CriticSubagentSkillTests` :25, :32 | `context: fork`, `disable-model-invocation: true` | allowed-field guard; body text reworded |
| `NoCopilotIsmsTests` :290-291 | the same in the critic | allowed-field guard |
| `GenerationSkillContractTests` :129 | "never auto-invoked" in the generate skill | the reworded rule (FR-023) |
| `SkillsManifestTests` :65 | 16 skills | unchanged (references aren't skills) |

Unchanged: the verbatim copies of the NON-STOP and 075 text (`GroundingIdsSkillTests`, `GenerateOrchestratorTests`
:127-138, `SkillsManifestTests` :598-606, `BatchGenerationAgentTests`, `CoveredLinesTextTests`) stay in SKILL.md and
keep passing. That is the regression gate. `SourceStringsGuardTests` already scans `Skills/Content/**/*.md`
recursively, so it covers the reference files without a change.

## R9 — Agent frontmatter (from the docs)

**Findings**:
- Documented subagent fields: `name`, `description`, `tools`, `disallowedTools`, `model`, `permissionMode`, `maxTurns`,
  `autoCompactWindow`, `skills`, `mcpServers`, `hooks`, `memory`, `background`, `omitClaudeMd`, `effort`, `isolation`,
  `color`, `initialPrompt`, `experimental`. Unknown fields are ignored without an error.
- `claude --debug` logs "does not recognize or apply" for them (v2.1.296 or later), in
  `~/.claude/debug/<session-id>.txt`.
- No subagent field stops auto-delegation. The docs say to write clear descriptions; the only hard block is a settings
  `deny: ["Agent(name)"]`, which would also stop the skills from starting the agent.

**Decision**:
- The bundled agents keep `name`, `description`, `tools`, `model`. The guard allows exactly these four.
- Descriptions (contract C4) start with "Only for use by the <skill> skill(s) —" and end with "Do not delegate to it
  directly; use the <skill> skill." "never auto-invoked" is dropped.
- The critic's description names the skills and their work subagents.

**Alternatives considered**: a settings `deny` rule written by `init`. It blocks the skills' own calls. Rejected.

## R10 — Measuring the gate and the budgets

**Decision** (quickstart):
- Gate: a suite of ≥ 30 tests with section refs. Class each test as "seen" or "cut": compile with `--full-docs` and
  check that every cited section's heading line and last line are in the prompt.
- Run the critic per test twice (default skill path; then `full_docs: true`), saving verdicts aside after each run, and
  ingest each run with `ingest-verdict --all`.
- Prompt sizes come from `compile-critic-prompt --output-format json` in both modes, measured in characters.
- `/context` before and after a generation of ≥ 24 tests and an update of ≥ 3 groups, on 101 and on `6a4c41a`.
- The compaction run uses `/compact` after Step 7 and checks that Steps 8–9 complete.
- SKILL.md token counts: `/context` right after invoking each skill in a fresh session (or the API's token count),
  each under 5,000.

## Baseline

`dotnet test` at `6a4c41a` (this branch, docs-only changes): see plan.md Technical Context.
