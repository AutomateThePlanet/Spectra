# Research: Docs and Guidance (Phase 6, Part 5)

Verified on `71b1bea` (branch `100-docs-and-guidance`). Baseline counts (099): Core 1228 / CLI 2280 / Execution 228 /
Integration 8, 0 build warnings.

## R1 — The removed-concept list (one list for every guard)

**Decision**: one shared list in the test project (`RemovedConcepts`), each entry = pattern + the spec that removed it +
the replacement wording (data-model.md). Entries:

| Pattern | Removed in | Replacement |
|---|---|---|
| `spectra ai generate` (and its flags `--auto-complete`, `--from-suggestions`, `--analyze-only` as `ai generate` flags) | 059 | the `spectra-generate` skill ("ask your assistant to generate tests"); CLI context names the seam commands |
| `spectra auth` | 069 | nothing to authenticate |
| `ai.providers` / `"providers"` block | 069 | — |
| in-process critic keys `ai.critic.provider`, `api_key_env`, `base_url`, `enabled`, `timeout_seconds`, `max_concurrent` | 069 / 098 | `ai.critic.model` |
| `ai analyze --extract-criteria` | 069 | the `spectra-criteria` skill |
| `--skip-splitting` | 069 | — (import no longer splits) |
| SPECTRA's MCP server: `spectra-mcp`, `.vscode/mcp.json` written by init, `mcp__spectra__`, "Spectra MCP Server", "Execute … via MCP" | 070 | `spectra run` |
| GitHub Copilot SDK as runtime, Copilot Chat as the client, Copilot Spaces | 059/069/070 | Claude Code session |
| `ai.generation_batch_size` | 096 | `generation.batch_size` |
| `ai.defaultProvider` | (never a key) | `source.local_dir`, `tests.dir` |
| `ai.analysis.max_prompt_tokens` | (wrong path) | `analysis.max_prompt_tokens` |

**Rationale**: the doc guard, the source-string guard and the reviewer must agree on what "removed" means. The two
"wrong key" rows are not removals, but they are the same defect class (a key the user cannot set) and appear in the
same files.

**Alternatives**: separate lists per guard — rejected, they drift.

## R2 — When a mention is allowed (the allow-list)

**Decision**: a mention passes the guard only when one of these holds:
1. the file is historical (explicit list below);
2. the mention's paragraph (blank-line delimited block, a blockquote counts as one) contains a removal marker from a
   fixed list: `removed`, `no longer`, `there is no`, `is gone`, `are gone`, `retired`, `legacy`, `pre-v2`, `deprecated`
   (case-insensitive);
3. for MCP: the paragraph names the separate BELLATRIX/Nova system-under-test driver or the Azure DevOps bug-log MCP
   (`BELLATRIX`, `Nova`, `Azure DevOps`) — these are legitimate (FR-004). README:55 names Azure DevOps, so it passes.

Historical files: `CHANGELOG.md`, `specs/**`, `docs/specs/**`, `docs/investigation/**`, `docs/analysis/**`,
`docs/claude-code-v2-migration.md`, `docs/ai-models-cost-guide.md`, `docs/migration-040.md`, `docs/architecture/
ARCHITECTURE-v2.md` (the v2 design document), root `FINDINGS-*.md`, `INVESTIGATION-*.md`, `VERIFICATION-*.md`,
`architecture-v5.md`, `PROJECT-KNOWLEDGE.md` (see R9). This is the FR-003 list. `docs/architecture/**` is otherwise
user-facing.

**Rationale**: many current docs correctly say "there is no `spectra auth` command anymore" — a pure per-file allow-list
would either forbid those useful notes or exempt whole files. A paragraph rule keeps the allowance next to the text,
visible to reviewers, and the marker list is explicit in the test (FR-004). Paragraph scope (not line) because
blockquote notes wrap.

**Risk**: a paragraph that says "removed" for some other reason and still tells the user to run a removed command.
Mitigation: the README quick start and the new guide are additionally checked by the command-tree guard (R4), which
does not honour markers.

**Alternatives**: exact (file, text) allow-list — brittle, every doc edit breaks it; rejected. Line-level markers —
fail on wrapped blockquotes; rejected.

## R3 — Inventory: user-facing docs

Counts of removed-concept matches (R1 patterns) on `71b1bea`; "fix" = rewrite as current flow, "mark" = already a
removal note (passes R2), "SUT" = legitimate other MCP.

| File | Hits | Action |
|---|---|---|
| README.md | 16 | fix: quick start (`ai generate` ×6 → skill), "Copilot Chat Integration" / "Using SPECTRA from VS Code Copilot Chat" → Claude Code, architecture diagram ("GitHub Copilot SDK (sole AI runtime)", "any MCP client"), docs table labels ("Usage (Copilot Chat)"); keep line 55 (SUT/tracker MCP) and 99 ("no MCP server required" — reword to plain statement); add the guide link |
| docs/cli-reference.md | 15 | mostly mark (auth/MCP removal notes); fix `ai.analysis.max_prompt_tokens` (920), the `--extract-criteria` lines 216/545/929, critic `max_concurrent` note (930) |
| docs/customization.md (embedded → `CUSTOMIZATION.md`) | 12 | fix lines 24, 44, 90–92 (template table "used by"), 242/249 (`generation_batch_size` example → `generation.batch_size`), 299 (`.vscode/mcp.json` list item); 230–237 is a removal note (mark) |
| docs/configuration.md | 11 | fix 45 (`space_name` Copilot Space row — check the `source.mode: spaces` model, R10), 152 (`ai generate` runs index refresh); 63–69/88/129–136 are removal notes |
| docs/execution-agents.md | 6 | mark / SUT — no change expected |
| docs/document-index.md | 6 | fix 77, 230, 240–242 (pre-flight message example — follows the R6 text) |
| docs/getting-started.md | 4 | fix 126 (`generation_batch_size` example); 20/35–36 are removal notes |
| docs/architecture/overview.md | 4 | mark — no change expected |
| docs/usage.md (embedded → `USAGE.md`) | 3 | fix 74/80 (`ai generate`, `--extract-criteria`); new guide (R5) |
| docs/skills-integration.md | 3 | 111 already says "there is no single `spectra ai generate`" (mark); 22–24 removal note |
| docs/coverage.md | 3 | fix 141, 178 (`--extract-criteria` → skill) |
| docs/grounding-verification.md | 2 | mark |
| docs/DEVELOPMENT.md | 2 | mark |
| docs/testimize-integration.md | 1 | "no MCP server" in a description of Testimize — passes as a negative statement (`no` is not a marker; reword to "no separate server" so the guard needs no exception) |
| docs/index.md | 1 | fix 33 (the pipeline line) + guide link |
| docs/deployment.md | 1 | fix 10 ("Copilot Spaces" in the guide list — R10) |
| CONTRIBUTING.md | 1 | fix |
| AGENTS.md | 4 | rewrite (R9) |

docs/user-guide.md: 0 hits (checked; still reviewed for current flows in passing — no rewrite).

## R4 — Command-tree guard

**Decision**: test helper `CommandLineCheck` builds `Program.CreateRootCommand()` (public) and walks the
System.CommandLine tree. Two checks:
- **Structural** (CLAUDE.md Commands block, README, docs synopsis lines): a line starting with `spectra ` is tokenised
  (quotes respected); the command path is the leading tokens that name subcommands; `a|b|c` at a command position
  expands to each alternative; every token starting with `-` (after stripping `[`, `]`, `(`, `)` and splitting on `|`)
  must be an alias of an option on the resolved command or any ancestor (global options are on the root). `<x>`,
  `{x}`, `…`, values and `#` comments are ignored. Failure message names the line and the missing command/option.
- **Parse** (the worked example only; lines starting with `spectra ` — others such as `git mv` are ignored): each command line is concrete (no placeholders) and is parsed with the real
  parser; `ParseResult.Errors` must be empty.

**Rationale**: synopsis lines (`spectra run start <suite> [--priorities …]`) can never parse; concrete example lines
can and should. Walking the parser's own tree means the guard needs no hand-kept command list.

**Alternatives**: running `--help` for each command and scraping — slow (process per command) and format-dependent;
rejected.

**Extraction**: CLAUDE.md — the fenced block under `## Commands`; usage.md — the section between `## When documentation
changes` and the next `## ` heading, fenced `bash` blocks and inline code spans starting with `spectra `; README quick
start — fenced blocks under `## Quick Start`.

## R5 — The guide: "When documentation changes"

**Decision**: replace usage.md's "Updating Test Cases After Doc Changes" (lines 131–269, incl. "Legacy: `spectra ai
update`") with "When documentation changes": an intro, a numbered 10-step flow (spec FR-005 order), one worked example,
and a "Committed vs scratch" table and one refresh line (an edited `USAGE.md` / `CUSTOMIZATION.md`: delete it and run `spectra
update-skills` — UpdateSkillsHandler's `DeleteAdvice`; a prompt template: `spectra prompts reset <id>`). The legacy `ai update` subsection is kept, shortened, under the guide as "Legacy
word-overlap classifier". Content from the old section that is still right (bucket definitions, relink, group edit,
unchanged/unchanged_pending, re-verify) is moved, not rewritten from scratch.

Worked example (concrete, offline-runnable command lines; model turns described in prose): a `checkout` suite;
`docs/payments/checkout.md` changes a limit (`5000 eur` → `3000 eur`, a LOAD), renames `refunds.md` →
`refund-policy.md` in git (RELINK), and drops a section (REMOVED). Commands shown:
`spectra docs changed`, (criteria via the skill: `spectra docs apply-move --from … --to … --dry-run`, `spectra docs
retire --doc … --dry-run`, re-extraction turn), `spectra ai update-triage --suite checkout`, `spectra ai
relink-sources --suite checkout --dry-run`, `spectra ai compile-update-prompt --suite checkout --group G1`, `spectra ai
ingest-update checkout --all`, `spectra ai ingest-verdict --suite checkout --all --ids TC-104`, `spectra ai
ingest-grounding --suite checkout --all --ids TC-104`, `spectra ai update-ack --suite checkout --group G2`.

Committed vs scratch (from `SpectraGitIgnore.Patterns` and the 084–097 file list): committed — `test-cases/**` incl.
`_index.json`, `_source-baseline.json`; `docs/criteria/**` incl. `_criteria_index.yaml`; `docs/_index/**`. Scratch
(gitignored) — `.spectra/update-triage.json`, `.spectra/updates/`, `.spectra/repairs/`, `.spectra/verdicts/`,
`.spectra/generation-plan/`, `.spectra/*.lock`, `.spectra/dropped-tests.json`. The table is generated from — and a test
compares it with — `SpectraGitIgnore.Patterns` so it cannot drift (FR-008 extension; cheap).

**Heading test**: `ProfileFormatLoaderTests` asserts the old heading; it is retargeted to `When documentation changes`
(deliberate assertion change, listed in tasks Notes). Anchors: README/index.md link `docs/usage.md#when-documentation-changes`.

## R6 — Runtime strings (user decision: in scope, text only)

| Location | Today | New text |
|---|---|---|
| `Output/NextStepHints.cs:80,122` (init and docs-index hints) | `spectra ai analyze --extract-criteria  # Extract acceptance criteria from docs` | `Ask Claude Code to extract acceptance criteria  # runs the spectra-criteria skill` (same column style as line 78) |
| `NextStepHints.cs:82–84` | "Optional: Create a Copilot Space … See docs/copilot-spaces-setup.md" | removed (3 lines + the preceding blank line) |
| `Commands/Init/InitHandler.cs:190` (`_criteria_index.yaml` header) | `Run 'spectra ai analyze --extract-criteria' to populate.` | `Ask your assistant to extract criteria (the spectra-criteria skill) to populate.` |
| `InitHandler.cs:207–208` (sample criteria) | `1. Run 'spectra ai analyze --extract-criteria' to auto-extract from your docs` | `1. Ask your assistant to extract criteria from your docs (the spectra-criteria skill)` |
| `Coverage/CoverageReportWriter.cs:108` | `Run \`spectra ai analyze --extract-criteria\` …` | names the `spectra-criteria` skill |
| `Generation/PromptCompiler.cs:60`, `Generation/UpdatePromptCompiler.cs:105` | `… Run 'spectra ai analyze --extract-criteria' …` | `… Extract criteria first (the spectra-criteria skill) …` |
| `Dashboard/Templates/scripts/app.js:1399` and `dashboard-site/scripts/app.js:1399` | `run <code>spectra ai analyze --extract-criteria</code>` | `ask your assistant to extract criteria (<code>spectra-criteria</code> skill)` |
| `Commands/Update/UpdateHandler.cs:185,342` | `Run 'spectra ai generate' …` | `Ask your assistant to generate tests (the spectra-generate skill) …` |
| `Analysis/AnalyzerInputBuilder.cs:159` + `Index/PreFlightTokenChecker.cs` message | `spectra ai generate --suite X`, `… --analyze-only`, `ai.analysis.max_prompt_tokens` | hint `spectra ai compile-analysis-prompt --suite <suite>`; narrowing line `… --doc-suite {largest}`; the `--analyze-only` line removed; `Or raise analysis.max_prompt_tokens …` |
| `Spectra.Execution/Reports/ReportWriter.cs:261,815` | "Generated by Spectra MCP Server" | "Generated by SPECTRA" |
| `Commands/Config/ConfigCommand.cs:17` | `'source.dir', 'ai.defaultProvider'` | `'source.local_dir', 'tests.dir'` |
| `Commands/Run/RunCommand.cs:13` | "(no MCP server required)" | "Drive deterministic test execution from the CLI" |
| `Commands/Update/UpdateCommand.cs:12` | "Update test cases based on documentation changes" | "Legacy word-overlap classifier — use update-triage for documentation changes" |

Not changed (internal, never shown): XML doc comments (`PreFlightTokenChecker` param docs are updated to the right key
only because they name it — comment, no output), `DebugLogger` provider fields, `TokenUsageReport.provider` JSON key,
`CostEstimator` ("Included in Copilot plan": reached only through `RunSummaryPresenter`'s token table — verify at
implementation whether any command still builds a `TokenUsageReport`; if one does, the text is fixed as R6, else it is
left for a dead-code follow-up and listed in Notes), `ConfigLoader` legacy-key detection (needed), `CriticConfig` retired-key list (needed), `ProgressPageWriter`
`extract-criteria` phase id (internal key).

**Rationale**: each change is a string literal; no branch, exit code or JSON key changes. The pre-flight message is a
user-facing exception message whose structure (lines) changes — still text; its two pinning tests are retargeted.

**Tests retargeted (deliberate)**: `PreFlightTokenCheckerTests` (line 66 `--analyze-only`), `AnalyzerInputBuilderTests`
(line 215), any NextStepHints/Init/Coverage/Report test that pins the old text (found by the build+test run; listed in
tasks Notes).

## R7 — Bundled skill/agent text

| File | Change |
|---|---|
| `spectra-docs.md:≈48–62` | describes a `criteria_warning` from `docs index` naming `--extract-criteria`; nothing sets `DocsIndexResult.CriteriaWarning` since 069 (index-only), so the paragraph and its example are replaced by "`docs index` does not extract criteria; use the `spectra-criteria` skill" (the dead JSON property itself stays — removing it is out of scope) |
| `spectra-help.md:116` | replace the `--extract-criteria` line with the criteria skill / seam commands |
| `spectra-generate.md:389` (delegation row "Extract criteria") | `spectra-criteria` skill, no command |
| `spectra-quickstart.md:288` "Workflow 9: Execute Test Cases via MCP" | "Workflow 9: Execute Test Cases" (body already CLI-only) |
| `spectra-generate.md:20`, `spectra-criteria.md:24`, `spectra-execute.md:9`, `spectra-dashboard.md:9` | negative statements to the agent — kept; allow-listed by an explicit list of these exact statements in `SourceStringsGuardTests` (T027). The R2 removal-marker rule applies to docs paragraphs only |

Changing bundled content changes its hashes; `update-skills` refreshes unedited installed copies (096 rule). The 099
golden run uses `init --skip-skills`, so skill text is not in its file tree.

## R8 — Golden check under deliberate text changes

The 099 test runs the same sequence twice: on a fresh `init` workspace (`Fresh…`) and on the captured c24d23e workspace
(`Pre099…`, init step skipped), comparing both against one golden folder. After 100:
- help steps (00–04) change identically in both runs (build-dependent);
- the **fresh** file tree changes where init writes corrected text (`CUSTOMIZATION.md`, the two criteria files);
- the **pre-099** file tree keeps the old init-written files (they are inputs) — so one `files.txt` can no longer
  serve both runs.

**Decision**: before re-capture, copy `Golden/099/files.txt` to `Golden/099/files-pre099.txt` (byte copy). The
pre-099 run compares its tree with `files-pre099.txt`; the fresh run with the re-captured `files.txt`. The pre-099 run
therefore proves the strongest claim available: on an existing workspace every command's files are byte-identical to
the 099 capture. Step outputs 05–26 must not change at all (05-init is JSON and prints no hints — verified). The
re-capture diff (`git diff --stat` + full diff of `Golden/099/`) is recorded in tasks Notes and reviewed line by line:
allowed lines are exactly the R6/F1 strings in `00-help`/`02-ai-help` (run, update, config descriptions — config is
under 00) and `files.txt` (`CUSTOMIZATION.md` body, two criteria-file comment lines).

**Recording must not touch the frozen workspace.** Today the record mode (`SPECTRA_GOLDEN_RECORD`) also deletes
and re-copies `Golden/099/pre099-workspace/` from the fresh init (GoldenOutputTests lines 64–73). A 100 re-capture would
silently replace the c24d23e workspace with 100's init output and the pre-099 run would stop testing an old workspace.
**Decision**: the record mode no longer writes `pre099-workspace/` (it is a fixture captured once on c24d23e, like
`files-pre099.txt`); a guard asserts `pre099-workspace/CUSTOMIZATION.md` still contains the 099 text (e.g. ``Re-run
`spectra ai generate` ``), so an accidental overwrite fails.

**Alternatives**: a new `Golden/100/` folder — duplicates ~1,600 lines and loses the visible diff against 099; rejected.
Masking init-written files in the pre-099 run — weaker (hides them); rejected.

## R9 — CLAUDE.md, AGENTS.md, PROJECT-KNOWLEDGE.md

**CLAUDE.md** (≤ 150 lines) sections: header (one line: last updated + "history in CHANGELOG.md / specs/"); Tech Stack
(≈ 8 lines: C# 12/.NET 8, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet, FileSystemGlobbing,
CsvHelper, Microsoft.Data.Sqlite; all inference in the user's Claude Code session via `.claude/skills` + `.claude/agents`;
critic = `spectra-critic` subagent, `ai.critic.model`); Project Structure (existing folders only, one-line purpose each;
`src/Spectra.CLI` 28 folders, `src/Spectra.Core` 17, `src/Spectra.Execution` 6, `dashboard-site/`, `tests/` 4 projects + `TestFixtures/`; `Spectra.GitHub` and CLI `Config/` no longer exist);
Commands (generated from the command tree's help, grouped: build/test, global options, seam (generate/criteria/
update/verify), baseline/update, docs, coverage, run, other — no `ai generate`, no `--extract-criteria`/
`--skip-splitting`); Code Style (as today); Execution (3 lines); Active Technologies (kept as a heading with one line —
see below); Recent Changes (one line per spec 078–100 → `CHANGELOG.md` + `specs/NNN-*/`).

**Agent-context script**: `.specify/scripts/bash/update-agent-context.sh` keeps only the **first two** `- ` lines under
`## Recent Changes` and prepends one for the current branch when the plan names new technology (lines 455–475). A
one-line-per-spec list in `- ` form would be truncated to 3 entries the next time a spec runs it. **Decision**: Recent
Changes is a Markdown table (`| Spec | Summary |`) — the script only touches `- ` lines, so the table survives; the
script's own inserted `- ` line (if any) lands above the table and is folded into the table when the spec is merged.
`## Active Technologies` stays with one `- ` line so the script appends there instead of creating a new section at the
end. The guard test (FR-017) also asserts both headings exist (keeps the script's behaviour predictable). This spec does
not run the script (it would only re-add the stale line removed by F5; plan Technical Context names no new technology).

**AGENTS.md** (26 lines; constitution Governance points to it): describes `Spectra.MCP`, the Copilot SDK and
`Spectra.MCP.Tests`. Rewrite to the current projects/test projects and point to CLAUDE.md, with no command list of its own (FR-014);
same guard.

**PROJECT-KNOWLEDGE.md** (413 lines, v1 architecture: Copilot SDK, MCP execution) and `architecture-v5.md`: marked
historical with a top banner ("Describes SPECTRA v1 (before specs 058–070). Current guidance: CLAUDE.md, docs/") and
allow-listed. A rewrite duplicates CLAUDE.md + docs; rejected.

## R10 — `source.mode: spaces` / Copilot Spaces

`SourceConfig.Mode` and `SpaceName` (`space_name`) still exist and still load; nothing reads `SpaceName` (only
`spectra config` prints `Mode`). **Decision**: configuration.md:45 keeps the row, reworded as "accepted and ignored —
the Copilot Spaces lookup was removed in 070" (a removal note under R2); the `mode` row drops `spaces` as a working
value. deployment.md line 10 drops "Copilot Spaces". No code change (removing the key is a config-model change, out of
scope).

## R11 — Constitution 2.0.0 → 2.1.0

- Test-Required Discipline: "MCP tool contracts" → "CLI command contracts (JSON output and exit codes)".
- Code Review: "new MCP tools" → "new CLI commands".
- Principle III, new bullet after "minimal and structured": "In JSON mode stdout is only the JSON document; the human
  summary is never suppressed — it goes to stderr, so the user always sees what happened."
- Principle IV, the batch bullet gains: the batch ingest commands (`ingest-update --all`, `ingest-verdict --all`,
  `ingest-grounding --all`, `ingest-analysis`) support `--dry-run` (090/097); `--no-review` is a global option and a
  no-op for commands that never prompt.
- Principle IV: "The AI agent MUST never write to the filesystem directly — all output goes through validated tool
  handlers" → "The AI agent never writes tests, criteria, indexes, baselines or grounding directly. It writes only staging inputs under `.spectra/` (e.g. `.spectra/updates|repairs/<suite>/*.json`, `.spectra/generated.json`, `.spectra/analysis.json`, `.spectra/criteria.json`, critic verdict files) that a validated CLI ingest command reads; every persisted artefact is written by a CLI command." Recorded in the Sync Impact Report as a clarification of existing behaviour (since
  077/088), not a change.
- Governance: "Runtime development guidance is in `AGENTS.md`" → "`CLAUDE.md` (AGENTS.md points to it)".
- Footer: Version 2.1.0, Last Amended 2026-10-16. Sync Impact Report replaced (2.0.0 → 2.1.0, MINOR: Principle III
  expanded; II/IV wording clarified); templates: plan/spec/tasks templates checked — no MCP/provider wording
  (`grep` clean on `71b1bea`), compatible.

MINOR (not PATCH) because Principle III gains a normative rule.

## R12 — CHANGELOG

- 100 entry under `[2.6.0]` ("### Changed (Spec 100)": docs, guide, help/runtime text, CLAUDE.md, constitution).
- "Highlights" at the top of `[2.6.0]`: ≤ 12 bullets for users covering 078–100 (lossless writes; stable criterion
  IDs; source-change baseline + triage; git relink; update prompts with diffs; re-verification; subagent
  orchestration; section-scoped generation batches; per-behaviour coverage; workspace safety; verification truth;
  performance; the guide). The section header date/title is left as is (unreleased per CLAUDE.md; renaming the release
  is the release step, not 100).

## R13 — depends_on

test-format.md:66 row → `string` "One test ID that must pass first (a single id; a list is not supported and makes the
file unparseable — `spectra validate` reports it, `spectra delete` warns)". Check the rest of test-format.md and
cli-reference for list examples (`depends_on: [..]` / `- TC-`); none expected beyond the row. Verify `spectra validate`
reports a list form as a parse error (096 F4) before writing that clause.
