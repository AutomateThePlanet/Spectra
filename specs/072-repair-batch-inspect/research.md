# Research: Repair-Orchestration Hardening & Inspection Surface

**Date**: 2026-06-19
**Status**: Complete — all decisions grounded in CONFIRMED investigation findings

---

## Decision 1 — Repair intermediate file location

**Decision**: `.spectra/repairs/repaired-{id}.json`

**Rationale**: Keeping repair scratch files in a dedicated `.spectra/repairs/` subdirectory separates them from the authoritative verdict JSONs in `.spectra/verdicts/`. This avoids any confusion between `critic-verdict-TC-105.json` (the critic's output) and `repaired-TC-105.json` (the agent's repair patch). Both directories are gitignored. The per-test naming (`repaired-{id}.json`) prevents overwrite collisions when the agent processes multiple tests in a session.

**Alternatives considered**:
- `.spectra/repaired-{id}.json` (flat in .spectra/) — used by the agent in the Spec 071 run; rejected because it mixes scratch files with other .spectra artifacts
- `.spectra/verdicts/repaired-{id}.json` (in verdicts/) — rejected; the verdicts dir holds critic output, not agent patches

---

## Decision 2 — `compile-repair-batch` output format

**Decision**: Emit a JSON array to stdout; rely on the harness's automatic persisted-output for large payloads.

**Rationale**: The Claude Code harness automatically saves large stdout to a temp file when it exceeds a threshold. `compile-repair-batch` emits to stdout exactly as `compile-repair-prompt` does — the manifest IS the JSON output, read by the agent via the harness. No special in-process large-prompt handling needed (the "persisted-output pattern" is a harness feature, confirmed in investigation). The manifest is a JSON array (not wrapped in an object envelope), so the harness presents it directly as readable JSON without extra shell parsing by the agent.

**Alternatives considered**:
- Writing manifest to a fixed file (e.g. `.spectra/repair-manifest.json`) — rejected; would require the command to be stateful and the agent to know the fixed path; stdout + harness is cleaner
- One-entry-per-line NDJSON — rejected; agents read JSON arrays more naturally with the harness's Read tool

---

## Decision 3 — `audit-grounding` grounding-block detection

**Decision**: Use `TestCaseParser` to parse each test's `.md` file and check `tc.Grounding is not null`.

**Rationale**: The `GroundingMetadata` model maps exactly to the frontmatter `grounding:` block. `TestCaseParser.Parse()` already deserializes this block — CONFIRMED in `ReviewFlaggedHandler.cs:68-72`. The `FlaggedForReview` field is also available on `GroundingMetadata` — no additional parsing needed. This is the same pattern used by `ReviewFlaggedHandler.FindFlaggedAsync()`, which we mirror for `audit-grounding`.

**Alternatives considered**:
- Reading frontmatter directly with string matching — rejected; brittle and duplicates parser logic
- Querying verdict JSON only — rejected; verdict JSONs have no grounding-block presence info; we must read the `.md` files

---

## Decision 4 — `compile-repair-batch` reuse of `RepairPromptCompiler`

**Decision**: Call `RepairPromptCompiler.Compile()` directly for each partial test. No wrapper, no duplication.

**Rationale**: `RepairPromptCompiler.Compile()` is a static, pure, model-free method (CONFIRMED `src/Spectra.CLI/Verification/RepairPromptCompiler.cs:21-125`). The batch command walks the same data-loading path as `CompileRepairPromptCommand.RunAsync()` (index → test file → verdict file → source docs) but does it N times in one pass. The prompt-building call itself is unchanged.

**Alternatives considered**:
- Extracting shared logic into a service class — rejected (YAGNI; three callers not yet; two callers share via static method)

---

## Decision 5 — `show` `file` field value

**Decision**: Emit the path relative to the working directory (e.g. `test-cases/unit-converter/TC-100.md`).

**Rationale**: In `ShowHandler.cs:82`, `testPath = Path.Combine(testsDir, testEntry.File)`. `testEntry.File` contains the suite-relative-to-testsDir path (e.g. `unit-converter/TC-100.md`). Emitting `Path.GetRelativePath(basePath, testPath)` gives a working-directory-relative path the agent can pass directly to `ingest-update --from` or any file-path argument without computing anything. Absolute paths would be machine-specific and unusable across agents.

---

## Decision 6 — `audit-grounding` scope: verdict-file-driven, not index-driven

**Decision**: Scan `.spectra/verdicts/critic-verdict-TC-NNN.json` files to discover which tests had critic runs, then look up each test in the suite index to find the `.md` file.

**Rationale**: The audit's purpose is to report on tests that have gone through the critic cycle. Not all tests in a suite necessarily have verdict files (e.g. if generation is in progress). Driving from verdict files (the critic's output) rather than the full suite index ensures `audit-grounding` only reports on tests that are actually in the verdict pipeline.

**Alternatives considered**:
- Index-driven (all tests in suite, flag those without verdict files too) — rejected; out of scope, would turn audit into a coverage report

---

## Decision 7 — `audit-grounding` action_needed values

**Decision**: Three string values: `repair` / `review` / `none`

- `repair`: has a verdict file, no grounding block — must go through repair loop
- `review`: has a grounding block, but `FlaggedForReview: true` — needs human review
- `none`: has a grounding block, not flagged — done

**Rationale**: These map exactly to the three post-critic states defined in Spec 071. The agent processes the manifest by filtering on `action_needed == "repair"`.

---

## No unknowns

All design decisions above are CONFIRMED from investigation findings. Zero NEEDS CLARIFICATION items. No external research tasks were required — this spec was built on investigation-backed facts.
