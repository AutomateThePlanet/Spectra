# Tasks: Docs and Guidance (Phase 6, Part 5)

**Input**: Design documents from `/specs/100-docs-and-guidance/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R13), data-model.md, contracts/contracts.md (C1–C3), quickstart.md

**Tests**: requested by the spec (FR-004, FR-008, FR-013, FR-017) — guard tests are written first and fail on `71b1bea`;
their failure lists are the work lists.

**Golden note**: from the first text change (Phase 3 on) the 099 golden check fails until T046 re-captures it; that is
expected. The pre-099 run must never need a re-capture (C3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1 docs, US2 guide, US3 CLI text, US4 CLAUDE.md/AGENTS.md, US5 constitution, US6 depends_on

---

## Phase 1: Setup

- [X] T001 Record the baseline on `71b1bea`: `dotnet build --no-incremental` (0 warnings) and `dotnet test` counts (Core 1228 / CLI 2280 / Execution 228 / Integration 8) in the Notes section of specs/100-docs-and-guidance/tasks.md
- [X] T002 Create the folder tests/Spectra.CLI.Tests/Docs/ and a `RepoFiles` helper there (repo root via the `RepositoryAttributesTests.RepoRoot()` pattern; `Read(relative)`, `Glob(pattern)`, `Paragraphs(text)` per data-model.md AllowRule scope — fenced blocks are their own paragraph) in tests/Spectra.CLI.Tests/Docs/RepoFiles.cs

---

## Phase 2: Foundational (blocks every story)

- [X] T003 [P] Add the shared removed-concept list (research R1 table: pattern, removed-in, replacement, applies-to) and the allow rule (historical globs, removal markers, legitimate-MCP names — research R2) in tests/Spectra.CLI.Tests/Docs/RemovedConcepts.cs
- [X] T004 [P] Implement `CommandLineCheck` over `Program.CreateRootCommand()`: structural mode (command path with `a|b` alternatives, every `-x`/`--x` token an alias on the command or an ancestor; `<…>`, `{…}`, `…`, quoted values, `#` comments ignored) and parse mode (`Parse(tokens).Errors` empty) per research R4 / data-model.md in tests/Spectra.CLI.Tests/Docs/CommandLineCheck.cs
- [X] T005 Self-test the checker: `spectra ai update-triage --suite X --dry-run`, `spectra run list-suites|list-active`, `spectra docs index [--force]` pass; `spectra ai generate`, `spectra ai analyze --extract-criteria`, `spectra docs index --bogus` fail with the missing token named, in tests/Spectra.CLI.Tests/Docs/CommandLineCheckTests.cs
- [X] T006 Golden prep (research R8, C3): byte-copy tests/Spectra.CLI.Tests/Golden/099/files.txt to tests/Spectra.CLI.Tests/Golden/099/files-pre099.txt (record both SHA-256 in Notes; they must be equal); make `Pre099Workspace_MatchesTheGoldenOutput` compare its tree with `files-pre099.txt`; remove the record-mode block that deletes and re-copies `pre099-workspace/`; add a test that `Golden/099/pre099-workspace/CUSTOMIZATION.md` still contains ``Re-run `spectra ai generate` `` (frozen fixture), in tests/Spectra.CLI.Tests/Golden/GoldenOutputTests.cs
- [X] T007 Run `dotnet test --filter "FullyQualifiedName~Golden|FullyQualifiedName~Spectra.CLI.Tests.Docs.CommandLineCheck"` — both golden runs and the checker self-test green before any text change

**Checkpoint**: the shared list, the checker and the golden split are in place.

---

## Phase 3: User Story 1 — docs describe the product as it is (P1) 🎯 MVP

**Goal**: no user-facing doc presents a removed command/key as current; the README quick start works.
**Independent Test**: `DocsRemovedConceptsGuardTests` and the README quick-start check pass.

- [X] T008 [US1] Write `DocsRemovedConceptsGuardTests`: scan README.md, AGENTS.md, CONTRIBUTING.md, CLAUDE.md and docs/**/*.md minus the historical globs; fail per match whose paragraph has no removal marker (MCP: no legitimate-MCP name); one test per file (Theory) so failures list file + line + concept, in tests/Spectra.CLI.Tests/Docs/DocsRemovedConceptsGuardTests.cs
- [X] T009 [US1] Add `ReadmeQuickStart_CommandsExist`: every `spectra …` line in fenced blocks under README's `## Quick Start` passes `CommandLineCheck` (structural), in tests/Spectra.CLI.Tests/Docs/DocsRemovedConceptsGuardTests.cs
- [X] T010 [US1] Run the two guards; paste the failure list (file, line, concept) into Notes and confirm it matches research R3 (new rows → add to R3 with an action)
- [X] T011 [US1] Rewrite README.md (R3 row): quick start generation via the `spectra-generate` skill ("Ask Claude Code to generate tests for checkout"), CI section lists the deterministic commands (`validate`, `ai update-triage --all-suites --dry-run`, `ai audit-grounding`, `dashboard`) and says generation needs an assistant session; replace "Copilot Chat Integration"/"Using SPECTRA from VS Code Copilot Chat" with Claude Code; fix the architecture diagram (no Copilot SDK runtime, no "any MCP client"); docs table labels; keep line 55 (tracker MCPs); reword line 99 without "MCP"
- [X] T012 [P] [US1] Fix docs/customization.md (embedded as CUSTOMIZATION.md): lines 24, 44, 90–92 ("used by" column → skills/seam commands), 242/249 (`ai.generation_batch_size` example → `generation.batch_size`), 299 (`.vscode/mcp.json` item removed); keep the 230–237 removal note
- [X] T013 [P] [US1] Fix docs/cli-reference.md: `ai.analysis.max_prompt_tokens` → `analysis.max_prompt_tokens` (≈920); `--extract-criteria` mentions at ≈216/545/929 → the `spectra-criteria` skill / `docs index` (index-only since 069); the ≈930 critic `max_concurrent` note → removal wording; check the `ai update` section says legacy and points to update-triage
- [X] T013a [US1] Add `CliReference_CoversEveryCommand` (FR-004a): every command path in `Program.CreateRootCommand()`'s tree is documented in docs/cli-reference.md — a command counts as documented when its full path (`spectra <group> <name>`, or `<group> <name>` for a subcommand) appears in a heading or a code span/block of docs/cli-reference.md — not as a bare word (C2); add the five missing entries to docs/cli-reference.md: `spectra ai init-seam-progress` (writes `.spectra/seam-progress.html`, the throwaway progress poller for seam skills), `spectra config add-automation-dir`, `spectra config remove-automation-dir`, `spectra config list-automation-dirs` (options from their `--help`), `spectra run list-active` (next to the other `spectra run` entries), in tests/Spectra.CLI.Tests/Docs/DocsRemovedConceptsGuardTests.cs
- [X] T014 [P] [US1] Fix docs/configuration.md: line 45 `space_name` row → "accepted and ignored — the Copilot Spaces lookup was removed in 070", `mode` row without `spaces` (research R10); line 152 (`spectra ai generate` runs the index refresh → the generation skill / `compile-analysis-prompt` refreshes it — verify which command refreshes it before writing)
- [X] T015 [P] [US1] Fix docs/document-index.md lines 77, 230, 240–242: the pre-flight example shows the new message text (T029 wording: `spectra ai compile-analysis-prompt --suite <suite> --doc-suite …`, `analysis.max_prompt_tokens`)
- [X] T016 [P] [US1] Fix docs/getting-started.md line 126 (`generation_batch_size` example → `generation.batch_size`), docs/coverage.md lines 141 and 178 (`--extract-criteria` → the `spectra-criteria` skill), docs/index.md line 33 (pipeline: docs → `spectra-generate` skill → analysis → critic → test-cases), docs/deployment.md line 10 (drop "Copilot Spaces"), docs/testimize-integration.md line 61 ("no separate server"), CONTRIBUTING.md (its one hit; FR-001)
- [X] T017 [P] [US1] Fix docs/usage.md lines 74–80 (the 048 "two coverage guards" text: the `docs index` zero-criteria warning is no longer emitted — nothing sets `criteria_warning` since 069 — and `spectra ai generate` is gone; check in src/ which no-criteria warning still exists (e.g. the `compile-prompt` warning in `PromptCompiler`) and describe only that, naming the `spectra-criteria` skill); same check for docs/coverage.md:191; leave the doc-change section to US2
- [X] T018 [P] [US1] Mark removed commands in the historical user guides: every place docs/claude-code-v2-migration.md and docs/ai-models-cost-guide.md show `spectra ai generate`/`spectra auth`/`ai.providers` as a command gets "(removed in 059/069)" or sits under a "v1 (removed)" heading; no other rewrite
- [X] T019 [P] [US1] (FR-003) Add a top banner to PROJECT-KNOWLEDGE.md and architecture-v5.md: "Historical — describes SPECTRA v1 (before specs 058–070). Current guidance: CLAUDE.md and docs/." (research R9)
- [X] T020 [US1] Re-run T008/T009; all green. Review every remaining R3 "mark" paragraph by eye (Notes: one line per file "checked, removal note")

**Checkpoint**: US1 complete — docs guard and README quick start green.

---

## Phase 4: User Story 2 — "When documentation changes" (P1)

**Goal**: one section walks the whole doc-change flow with a worked example.
**Independent Test**: `DocChangeGuideTests` pass.

- [X] T021 [US2] Write `DocChangeGuideTests` (data-model.md "Doc-change guide shape"): section `## When documentation changes` exists in docs/usage.md; its ordered list has the 10 bold step names in order; every `spectra …` line (fenced and inline code) passes the structural check; every line starting with `spectra ` in the `### Worked example` fenced `bash` blocks passes parse mode (other lines, e.g. `git mv` in the RELINK step, are ignored); every scratch row of the committed-vs-scratch table is in `SpectraGitIgnore.Patterns` and every `.spectra/` pattern from the update flow (update-triage.json, updates/, repairs/, verdicts/, generation-plan/, baseline.lock, criteria.lock, dropped-tests.json) is listed; every skill and agent name mentioned in the section (`spectra-criteria`, `spectra-update`, `spectra-generate`, `spectra-updater`, `spectra-critic`, `spectra-repairer`, `spectra-extractor`, …; matched as `spectra-[a-z-]+` outside `spectra <command>` lines) is a key of `SkillContent.All` or `AgentContent.All`; the refresh line (delete + `spectra update-skills`, `spectra prompts reset`) is present; README.md and docs/index.md link `docs/usage.md#when-documentation-changes`, in tests/Spectra.CLI.Tests/Docs/DocChangeGuideTests.cs
- [X] T022 [US2] Replace "Updating Test Cases After Doc Changes" (docs/usage.md ≈131–269) with "When documentation changes" (research R5): intro; steps 1 Docs changed (`spectra docs changed`: new/changed/moved/deleted) → 2 Criteria (`spectra-criteria` skill: `docs apply-move`, `docs retire` after confirmation, re-extraction with stable IDs, changeset summary) → 3 Triage (buckets, groups, `update-triage`) → 4 Relink (`relink-sources --dry-run` → yes → run → re-triage; `--min-similarity`; unchecked-out submodule note) → 5 REMOVED review (user fixes refs or deletes via `spectra delete`) → 6 Group edits (`spectra-update` → `spectra-updater` subagents, `compile-update-prompt --group`, `ingest-update --all`, exit 8 retry) → 7 Unchanged and pending (`unchanged` vs `unchanged_pending`, acknowledge or edit again) → 8 Re-verify (critic per written test, `ingest-verdict --all --ids`, repair once, `ingest-grounding --all --ids`, hallucinated → ask) → 9 Acknowledge (`update-ack --group/--ids`, COSMETIC auto-acked) → 10 Committed vs scratch (table), followed by one line: an edited `USAGE.md` / `CUSTOMIZATION.md` is refreshed by deleting it and running `spectra update-skills` (UpdateSkillsHandler's `DeleteAdvice`), a prompt template by `spectra prompts reset <id>`; move still-correct text from the old section rather than rewriting it
- [X] T023 [US2] Add `### Worked example` (research R5): `checkout` suite; limit change (LOAD), git rename `refunds.md` → `refund-policy.md` (RELINK), dropped section (REMOVED); concrete command lines only (no placeholders), model turns in prose, abbreviated real-shaped JSON output, and abbreviated shapes of the staged files the flow relies on (`.spectra/updates/checkout/updated-<id>.json`, a critic verdict file); keep the legacy `ai update` as a short "Legacy word-overlap classifier" subsection after the guide, in docs/usage.md
- [X] T024 [US2] Add the guide link to README.md (Documentation table + one line under the update feature) and docs/index.md (guides list)
- [X] T025 [US2] Retarget the heading row in tests/Spectra.CLI.Tests/Profile/ProfileFormatLoaderTests.cs (≈128) from "Updating Test Cases After Doc Changes" to "When documentation changes" (deliberate assertion change → Notes)
- [X] T026 [US2] Run `DocChangeGuideTests`, `ProfileFormatLoaderTests` and the US1 guards (usage.md is in both) — green

**Checkpoint**: US2 complete.

---

## Phase 5: User Story 3 — the CLI never points to a removed command (P2)

**Goal**: help, hints, warnings, written comments, report footers and bundled skills name only real commands.
**Independent Test**: `SourceStringsGuardTests` pass; golden diff limited to C3 (T046).

- [X] T027 [US3] Write `SourceStringsGuardTests` (C2): scan string literals in src/**/*.cs (skip `///` and `//` comments), src/Spectra.CLI/Skills/Content/**/*.md, src/Spectra.CLI/Prompts/Content/*.md, both app.js copies, against `RemovedConcepts` (`SourceStrings`/`BundledContent`); explicit literal allow-list: `ConfigLoader` legacy-key table, `CriticConfig` retired keys, the `extract-criteria` progress phase id in `ProgressPageWriter`, the agent negative statements in spectra-generate.md:20 / spectra-criteria.md:24 / spectra-execute.md:9 / spectra-dashboard.md:9, in tests/Spectra.CLI.Tests/Docs/SourceStringsGuardTests.cs
- [X] T028 [P] [US3] Help text (FR-010, FR-011; C1 T1–T3): src/Spectra.CLI/Commands/Config/ConfigCommand.cs:17 (`'source.local_dir', 'tests.dir'`), src/Spectra.CLI/Commands/Run/RunCommand.cs:13 (drop "(no MCP server required)"), src/Spectra.CLI/Commands/Update/UpdateCommand.cs:12 (legacy wording)
- [X] T029 [P] [US3] Pre-flight message (C1 T10): `commandHint` → `spectra ai compile-analysis-prompt` in src/Spectra.CLI/Analysis/AnalyzerInputBuilder.cs:159; in src/Spectra.CLI/Index/PreFlightTokenChecker.cs `BuildMessage` the narrowing line becomes `{hint} --suite <suite> --doc-suite {largest|<id>}`, the `--analyze-only` line is removed, the last line names `analysis.max_prompt_tokens`; XML doc comments name the right key; retarget tests/Spectra.CLI.Tests/Index/PreFlightTokenCheckerTests.cs:66 and tests/Spectra.CLI.Tests/Agent/Copilot/AnalyzerInputBuilderTests.cs:215 (deliberate → Notes)
- [X] T030 [P] [US3] Init hints (C1 T4/T5): src/Spectra.CLI/Output/NextStepHints.cs lines 80 and 122 → `Ask Claude Code to extract acceptance criteria  # runs the spectra-criteria skill` (same alignment as line 78), lines 81–84 (blank + Copilot Space + `docs/copilot-spaces-setup.md`) removed
- [X] T031 [P] [US3] Init-written criteria files (C1 T6): src/Spectra.CLI/Commands/Init/InitHandler.cs:190 and :207 name the `spectra-criteria` skill (YAML `#` comments, same line count)
- [X] T032 [P] [US3] Warnings and reports (C1 T7/T8/T9): src/Spectra.CLI/Coverage/CoverageReportWriter.cs:108, src/Spectra.CLI/Generation/PromptCompiler.cs:60, src/Spectra.CLI/Generation/UpdatePromptCompiler.cs:105, src/Spectra.CLI/Commands/Update/UpdateHandler.cs:185 and :342
- [X] T033 [P] [US3] Report footer (C1 T11): src/Spectra.Execution/Reports/ReportWriter.cs:261 (Markdown) and :815 (HTML) → "Generated by SPECTRA" (keep the link/markup)
- [X] T034 [P] [US3] Dashboard text (C1 T12): src/Spectra.CLI/Dashboard/Templates/scripts/app.js:1399 and dashboard-site/scripts/app.js:1399 (identical change in both)
- [X] T035 [P] [US3] Bundled skills (C1 T13, research R7): src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md:≈48–62 (the `criteria_warning` paragraph and its example describe a warning `docs index` no longer emits — nothing sets `DocsIndexResult.CriteriaWarning` since 069's index-only change; replace with "`docs index` does not extract criteria; use the `spectra-criteria` skill"), spectra-help.md:116, spectra-generate.md:389 (delegation row → `spectra-criteria` skill), spectra-quickstart.md:288 ("Workflow 9: Execute Test Cases"); do not touch the 075/093 NON-STOP text
- [X] T036 [US3] If any command still builds a `TokenUsageReport` (`GenerateResult`/`UpdateResult.TokenUsage` set anywhere in src/), fix `CostEstimator`'s "Included in Copilot plan" text (src/Spectra.CLI/Services/CostEstimator.cs:53); else list it in Notes as dead code for a later cleanup (research R6)
- [X] T037 [US3] `dotnet build --no-incremental` (0 warnings); run the CLI + Execution suites excluding `GoldenOutputTests` (expected to differ until T046 re-captures; `--filter "FullyQualifiedName!~GoldenOutputTests"`); for every failure caused by a replaced string, retarget the assertion to the new text and list it in Notes (deliberate); any other failure is a defect — fix the source. `SourceStringsGuardTests` green

**Checkpoint**: US3 complete except the golden re-capture (T046).

---

## Phase 6: User Story 4 — current agent guidance (P2)

**Goal**: CLAUDE.md and AGENTS.md describe the product as it is.
**Independent Test**: `ClaudeMdGuardTests` pass.

- [X] T038 [US4] Write `ClaudeMdGuardTests` (data-model.md "CLAUDE.md shape"): ≤ 150 lines; the seven required headings; every `spectra …` line in the `## Commands` fenced block passes the structural check; every folder in the `## Project Structure` block exists (indent = nesting, root = repo); `## Recent Changes` is a table with no `- ` list items; AGENTS.md: every path it links exists and it names no removed project (`Spectra.MCP`), in tests/Spectra.CLI.Tests/Docs/ClaudeMdGuardTests.cs
- [X] T039 [US4] Generate the real command list: run `dotnet run --project src/Spectra.CLI -- <group> --help` for the root, `ai`, `docs`, `run`, `suite`, `prompts`, `profile`, `doctor`, `config`, `testimize` and save the outputs under the scratchpad for T040 (not committed)
- [X] T040 [US4] Rewrite CLAUDE.md (research R9, ≤ 150 lines): header (last updated 2026-10-16, history in CHANGELOG.md and specs/); Tech Stack (current, no Copilot SDK / `Microsoft.Extensions.AI.Abstractions`); Project Structure (existing folders only, one-line purpose: Spectra.CLI 28, Spectra.Core 17, Spectra.Execution 6, dashboard-site/, tests/ 4 projects + TestFixtures; no `Spectra.GitHub`, no CLI `Config/`); Commands (from T039, grouped; no `ai generate`, `--extract-criteria`, `--skip-splitting`); Code Style; Execution (3 lines); `## Active Technologies` with one `- ` line; `## Recent Changes` table, one row per spec 078–100 (≤ 2 lines each, link `specs/NNN-*/`)
- [X] T041 [P] [US4] Rewrite AGENTS.md (FR-014: points to CLAUDE.md, no command list of its own): the four projects (`Spectra.CLI`, `Spectra.Core`, `Spectra.Execution`, test projects `Spectra.{Core,CLI,Execution,Integration}.Tests`), all inference in the user's Claude Code session, links to CLAUDE.md, docs/DEVELOPMENT.md, docs/usage.md, docs/customization.md, CONTRIBUTING.md, specs/; drop the USAGE.md/CUSTOMIZATION.md root links (those exist only in a user workspace) and mark PROJECT-KNOWLEDGE.md/architecture-v5.md historical
- [X] T042 [US4] Run `ClaudeMdGuardTests` and `DocsRemovedConceptsGuardTests` (CLAUDE.md and AGENTS.md are in its scope) — green

**Checkpoint**: US4 complete.

---

## Phase 7: User Story 5 — constitution 2.1.0 (P3)

**Independent Test**: read the diff and the Sync Impact Report.

- [X] T043 [US5] Amend .specify/memory/constitution.md per research R11 (FR-018–FR-021): Test-Required Discipline "MCP tool contracts" → "CLI command contracts (JSON output and exit codes)"; Code Review "new MCP tools" → "new CLI commands"; Principle III new bullet (JSON mode: stdout only the JSON document; the human summary is never suppressed and goes to stderr); Principle IV batch bullet (batch ingest commands `ingest-update --all`, `ingest-verdict --all`, `ingest-grounding --all`, `ingest-analysis` support `--dry-run` since 090/097; `--no-review` is global and a no-op where nothing prompts); Principle IV's "The AI agent MUST never write to the filesystem directly — all output goes through validated tool handlers" replaced by the FR-020 text (agent writes only `.spectra/` staging inputs read by a validated ingest command; every persisted artefact is written by a CLI command); Governance "`AGENTS.md`" → "`CLAUDE.md` (AGENTS.md points to it)"; footer Version 2.1.0, Last Amended 2026-10-16; replace the Sync Impact Report (2.0.0 → 2.1.0 MINOR, changed clauses, the Principle IV agent-write clause recorded as a clarification of existing behaviour since 077/088 — not a change, templates checked: .specify/templates/plan-template.md, spec-template.md, tasks-template.md ✅ — `grep -n "MCP\|provider\|Copilot" .specify/templates/` clean)

---

## Phase 8: User Story 6 — depends_on documented as it is (P3)

**Independent Test**: `DependsOnDocTests` pass.

- [X] T044 [US6] Verify with a scratch workspace that a list-form `depends_on` makes `spectra validate` report a parse error (exit 1) and `spectra delete` warn; note the observed messages in Notes (research R13)
- [X] T045 [US6] Write `DependsOnDocTests` (C2) in tests/Spectra.CLI.Tests/Docs/DependsOnDocTests.cs, then fix docs/test-format.md:66: type `string`, "One test ID that must pass first. A list is not supported and makes the file unparseable — `spectra validate` reports it and `spectra delete` warns." Search docs/test-format.md and docs/cli-reference.md for list examples (`depends_on: [` / `depends_on:` followed by `- TC-`) and fix any; test green

---

## Phase 9: Polish & cross-cutting

- [X] T046 Re-capture the golden files on Windows (quickstart.md: `SPECTRA_GOLDEN_RECORD=1`, fresh run only); confirm `pre099-workspace/` and `files-pre099.txt` are untouched (`git status`); review `git diff 71b1bea -- tests/Spectra.CLI.Tests/Golden/099/` line by line against C3 — only these may change (C3, by file and line): `00-help.txt` (`run` description), `02-ai-help.txt` (`update` description), `files.txt` — the `CUSTOMIZATION.md` body, its skills-manifest hash line (`"CUSTOMIZATION.md": "…"`, ≈810), the `### CUSTOMIZATION.md (<n> bytes)` header (≈861), the two criteria-file comment lines and the `### …(<n> bytes)` headers of `docs/criteria/_criteria_index.yaml` and `docs/criteria/sample.criteria.yaml`; every other differing line is a defect; paste the `--stat` and a one-line verdict per changed file into Notes; any other change → fix the source, re-capture
- [X] T047 Add the CHANGELOG.md entries (research R12): `### Changed (Spec 100)` under `[2.6.0]` (docs and guide, help/runtime text table, CLAUDE.md/AGENTS.md, constitution 2.1.0, golden split) and a `### Highlights` block at the top of `[2.6.0]` (≤ 12 user-facing bullets covering 078–100)
- [X] T048 Add the spec-100 row to CLAUDE.md's Recent Changes table (line count still ≤ 150; `ClaudeMdGuardTests` green)
- [X] T049 Full run: `dotnet build --no-incremental` (0 warnings) and `dotnet test` (all projects, 0 failures); record counts in Notes. Then walk through the worked example once in an init-created scratch workspace with the built CLI (model turns replaced by the staged files the example shows, built with the golden fixture helpers — e.g. `BaselineTestWorkspace.WriteVerdict` and the staging writers `GoldenOutputTests` uses) and record each command's exit code and outcome in Notes (SC-007)
- [X] T050 Fresh-checkout check (099 lesson): `git worktree add` of the branch tip into the scratchpad, build + full test there (golden bytes, CRLF in raw literals), then `git worktree remove`; record the result in Notes
- [X] T051 Final review of the whole diff against C1: every `src/` change is a listed string literal (no condition, exit code, JSON key or file-set change); note the review in Notes

---

## Dependencies & execution order

- Phase 1 → Phase 2 → stories. T003/T004 parallel; T005 after T004; T006 independent of T003–T005.
- US1 (Phase 3) and US2 (Phase 4) both edit docs/usage.md and README.md: do US1's T011/T017 before US2's T022–T024.
- US3 (Phase 5) is independent of US1/US2 except T015 quotes T029's wording (do T029 first or write T015 after it) .
- US4 after US3's T028 (CLAUDE.md Commands mirror the help) and after T039.
- T013a after T004 (uses the command tree).
- US5, US6 independent.
- T046 after every text change (US1 customization.md edit, US3 T028/T031); T047–T051 last.

## Parallel examples

- Phase 2: T003 ‖ T004 ‖ T006.
- US1: T012 ‖ T013 ‖ T014 ‖ T015 ‖ T016 ‖ T017 ‖ T018 ‖ T019 (different files) after T011.
- US3: T028 ‖ T029 ‖ T030 ‖ T031 ‖ T032 ‖ T033 ‖ T034 ‖ T035, then T036, T037.
- US5 (T043) and US6 (T044–T045) can run alongside US3/US4.

## Implementation strategy

MVP = Phases 1–3 (docs guard + README/docs fixed): the first thing a new user hits stops failing. Then US2 (the guide —
the plan's original goal), US3 (in-product text), US4 (agent guidance), US5/US6, Polish. Each story's guard is green
before moving on; the golden re-capture happens once, after all text changes, and is reviewed against C3.

## Notes

**T001 baseline (`71b1bea`).** `dotnet build --no-incremental`: 0 errors, 10 warnings — all NU1901–NU1904 package
advisories (ImageSharp 3.1.12 in Spectra.Execution), which 099 deliberately keeps as warnings; 0 compiler warnings.
`dotnet test`: Core 1228 / CLI 2280 / Execution 228 / Integration 8, 0 failures.

**T006/T007 golden split.** `files-pre099.txt` is a byte copy of the 71b1bea `files.txt`: both SHA-256
`df1a3bd06f25ecb10d621d5799b1df61de394fec118d7e48bb59ace8a02c4c6d` (unchanged after the re-capture). With the
embedded `customization.md` edit set aside, both golden runs + the checker self-test passed (21/21) before any text
change. The record mode no longer deletes and re-copies `pre099-workspace/`, and `Write` keeps `files-pre099.txt`.

**T010 docs guard work list** (after the README/cli-reference/configuration/customization/getting-started/coverage
fixes already made): AGENTS.md (Copilot, Spectra.MCP), architecture/overview.md:11, configuration.md:251 (`ai generate
--include-archived`), execution-agents.md:9, getting-started.md:18, grounding-verification.md:155, usage.md:74/78/80 —
all fixed. The first run let `usage.md:74` through because a whole bullet list was one paragraph and item 4 said
"removed": **deviation from R2 (stricter)** — a list item and a table row are now each their own paragraph
(data-model.md updated).

**T013a** cli-reference.md was missing 13 commands, not 5: `ai init-seam-progress`, `list`, `config`, `config
add-automation-dir|remove-automation-dir|list-automation-dirs`, `prompts list|show|validate`, `run resume`, `run cancel`,
`run list-active`, `open`. `run resume|cancel` were documented as `spectra run pause|resume|cancel` — the matcher expands
`a|b|c` alternatives (each alternative counts). Added: `spectra list`, `spectra config`, `spectra open <path>`,
`spectra prompts list|show|validate|reset`, `spectra ai init-seam-progress`, `spectra run list-active`.

**T020 review of "mark" paragraphs.** Every remaining mention in user docs is a removal note or a history link:
cli-reference.md (`spectra auth` (removed), the generation note, MCP execution server (removed)), configuration.md
(provider/critic notes, `generation_batch_size` read by nothing, Copilot Spaces removed), customization.md §5,
execution-agents.md (pre-v2 `.vscode/mcp.json`, BELLATRIX/Nova), getting-started.md:35–36, skills-integration.md:22–24
and :111, architecture/overview.md, grounding-verification.md, DEVELOPMENT.md. Also corrected while there (stale, not
removed-concept matches): `docs index` no longer extracts criteria (cli-reference, coverage, usage), the index is not
refreshed before generation (document-index, usage), `ai.generation_timeout_minutes`/`analysis_timeout_minutes` are
read by nothing, the critic model is `ai.critic.model` (customization, getting-started, grounding-verification),
`spectra init` never touches `.vscode/mcp.json` (execution-agents), init installs 16 skills (cli-reference).

**Deliberate assertion changes (T025, T029, T030, T037).**
- `ProfileFormatLoaderTests` (heading row): "Updating Test Cases After Doc Changes" → "## When documentation changes".
- `PreFlightTokenCheckerTests.EnforceBudget_OverBudget_ThrowsTypedException`: hint `spectra ai compile-analysis-prompt`;
  asserts `--suite <suite> --doc-suite SM_GSG_Topics`, no `--analyze-only`, `Or raise analysis.max_prompt_tokens`, no
  `ai.analysis.`.
- `AnalyzerInputBuilderTests.BuildAsync_BudgetExceeded_ThrowsActionableException`: the same suggestion and key.
- `NextStepHintsTests.GetHints_Init_ReturnsGenerateAndProfile`: "Copilot Space" → the `spectra-criteria skill` hint; no
  Copilot or `--extract-criteria` line.
- `GoldenOutputTests`: the pre-099 run compares with `files-pre099.txt`; the record mode keeps the frozen fixtures; new
  `Pre099Workspace_IsTheFrozenC24d23eCapture`.
T037 (CLI + Execution, golden excluded, before the retargets): CLI 2835 passed / 4 failed — exactly the three rows
above plus the CLAUDE.md docs guard (rewritten in US4); Execution 228 passed. No other test pinned a replaced string.

**Final wording (C1).**
- T1 `config` key argument: `Configuration key (e.g., 'source.local_dir', 'tests.dir')`.
- T2 `run`: `Drive deterministic test execution from the CLI`.
- T3 `ai update`: `Legacy word-overlap classifier — use update-triage for documentation changes`.
- T4 init hint `Ask Claude Code to extract criteria    # runs the spectra-criteria skill`; coverage hint
  `Ask Claude Code to extract criteria         # spectra-criteria skill`. T5: the Copilot Space lines and the blank line
  before them removed.
- T6 `_criteria_index.yaml`: `# Auto-generated by SPECTRA. Ask your assistant to extract criteria (spectra-criteria
  skill) to populate.`; sample: `#   1. Ask your assistant to extract criteria from your docs (spectra-criteria skill)`.
- T7 coverage report: `No acceptance criteria file found. Ask your assistant to extract criteria from the
  documentation (the `spectra-criteria` skill), or create `docs/criteria/_criteria_index.yaml` manually.`
- T8 `PromptCompiler`: `… Refusing to emit a prompt without grounding criteria. Extract criteria first (the
  spectra-criteria skill).`; `UpdatePromptCompiler`: `… Run 'spectra docs index' and extract criteria (the
  spectra-criteria skill) first.`
- T9 legacy `ai update`: `Ask your assistant to generate tests first (the spectra-generate skill).` / `Ask your
  assistant to generate tests for '{suite}' (the spectra-generate skill) to cover these criteria.`
- T10 pre-flight: `  spectra ai compile-analysis-prompt --suite <suite> --doc-suite {largest|<id>}`, the
  `--analyze-only` line removed, `Or raise analysis.max_prompt_tokens in spectra.config.json.`
- T11 reports: Markdown `*Generated by SPECTRA*`; HTML `Generated by <a …>SPECTRA</a>`.
- T12 dashboard (both copies): `…, or ask your assistant to extract them from the documentation
  (<code>spectra-criteria</code> skill).`
- T13 skills: spectra-docs "Criteria are not extracted here" paragraph; spectra-help `spectra docs changed …` line;
  spectra-generate delegation row `spectra docs changed --no-interaction --output-format json --verbosity quiet`;
  spectra-quickstart `## Workflow 9: Execute Test Cases`.
- Found at implementation (contracts.md T14–T16): `docs index --skip-criteria` / `--include-archived` help
  (`Accepted and ignored: docs index no longer extracts criteria (use the spectra-criteria skill)`); legacy run summary
  `(rate limited)` instead of `(consider reducing ai.critic.max_concurrent)`; `analysis.max_prompt_tokens` in
  spectra-generate and spectra-analyst.

**T036.** `CostEstimator` ("Included in Copilot plan", `github-models`) is dead in production: only tests call
`TokenUsageReport.FromTracker`, and both `TokenUsage` result fields are always null. Allow-listed in
`SourceStringsGuardTests` with that reason; deleting it (and `TokenUsageTracker`/`TokenUsageReport`) is a dead-code
follow-up, not text. Also dead: `DocsIndexResult.CriteriaWarning` (never set since 069).

**T038–T042.** `ClaudeMdGuardTests` green: CLAUDE.md 144 lines (was 332), every command line checked (a line with
`spectra a | spectra b` counts as two), every listed folder exists, Recent Changes is a table 078–100. **Deviation:** a Project
Structure line may list several folders (`Analysis/ Generation/  # …`) so the file fits in 150 lines (data-model.md
updated). Two commands were wrong in the first draft and caught by the guard (`compile-repair-prompt` / `record-drop`
take `--test`, not `--test-id`). The agent-context script was not run (R9).

**T044.** In a scratch workspace a `depends_on: [TC-100]` test makes `spectra validate` print `Parse error in
s1\TC-101.md: [INVALID_YAML] …` and exit 1; `spectra delete TC-100 --dry-run` warns `TC-101: depends_on is a list (not
supported) and names TC-100; not changed (test-cases/s1/TC-101.md)`.

**T046 golden diff review** (`git diff 71b1bea -- tests/Spectra.CLI.Tests/Golden/099/`: 3 files, +43/−48):
- `00-help.txt`: 1 line, the `run` description (T2) — allowed.
- `02-ai-help.txt`: 1 line, the `update` description (T3) — allowed.
- `files.txt`: line 810 (skills-manifest hash of `CUSTOMIZATION.md`, `c018776d…` → `a3eedad9…`), 861 (`### CUSTOMIZATION.md
  (13887 bytes)` → `(13925 bytes)`), hunks 879–1164 (inside the `CUSTOMIZATION.md` body — the docs/customization.md
  edits), 1323/1324 (`_criteria_index.yaml` header 129 → 149 bytes, comment line), 1369/1373 (`sample.criteria.yaml`
  header 677 → 681 bytes, comment line) — all allowed by C3.
- `05-init.txt` and every other step file: unchanged. The pre-099 run passes against the untouched `files-pre099.txt`,
  and `pre099-workspace/` is unchanged. 0 unexplained lines.

**T049.** `dotnet build --no-incremental`: 0 errors, 0 compiler warnings (the 10 NU190x advisories as at baseline).
`dotnet test`: Core 1228 / CLI 2345 / Execution 228 / Integration 8, 0 failures (CLI +65: the docs guards). After the
last usage.md edit: rebuild + Docs guards, ProfileFormatLoader and both golden runs 74/74.
**Walkthrough (SC-007)** in an init-created git workspace with the built CLI (script in the session scratchpad; model
turns replaced by staged files): `docs changed`, `docs apply-move` (dry run + real), `compile-extraction-prompt`,
`ingest-criteria` (changeset ~AC-CHECKOUT-001, −AC-CHECKOUT-002), `update-triage` (RELINK 1, REMOVED 1, LOAD 1,
COSMETIC 1 auto-acked), `relink-sources` (dry run + real), `update-triage`, `delete TC-109` (dry run + `--force`),
`update-triage` (G1 LOAD), `compile-update-prompt --group G1`, `ingest-update --all` (TC-101 updated),
`compile-critic-prompt`, `ingest-verdict --all --ids TC-101` (grounded 0.93), `ingest-grounding --all --ids TC-101`
(written 1), `update-triage` (UNCHANGED 3), `update-ack --ids TC-107` — every command exit 0. The guide's example output
was then aligned with the real output (criterion ids `AC-CHECKOUT-…`, group numbering, COSMETIC line).
**Found, out of scope (behaviour):** `docs changed` listed `docs/_index/groups/payments.index.md` as a new document —
`DocumentMapBuilder` excludes neither `docs/_index/` nor `docs/criteria/`, so the index's own files are treated as
documentation. Pre-existing; proposed as a follow-up.

**T050.** Fresh checkout: a `git worktree` of HEAD on a temporary local branch, the 100 changes applied and committed
there, every tracked file deleted and checked out again (so git's line-ending conversion applies); `dotnet build
--no-incremental` 0 errors (NU190x warnings only), `dotnet test` Core 1228 / CLI 2345 / Execution 228 / Integration 8,
0 failures (golden comparison included). The worktree and the temporary branch were removed afterwards.

**T051.** `git diff --stat -- src dashboard-site`: 21 files, +36/−54, every hunk a string literal or bundled-text line
from C1 (T1–T16) or an XML doc comment naming the right key; no condition, exit code, JSON key or written-file set
changed.
