# Feature Specification: Non-Stop Seam Coverage — Verdict & Update Batch Verbs, Manifest Consumption, General Contract Preamble

**Feature Branch**: `077-nonstop-batch-verbs`  
**Created**: 2026-06-22  
**Status**: Draft  
**Spec**: 077  

> **Investigation basis:** `INVESTIGATION-nonstop-verdict-loop-and-repair-manifest.md` + all OQ answers (all file+line CONFIRMED, no open questions remain). This spec fixes the class, not two instances.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Verdict Batch Ingest (Priority: P1)

The generation skill ingests all critic verdicts for a suite in a single deterministic call. Before this spec, the only way to ingest N verdicts was `ingest-verdict --from <file>` called N times; the skill prescribed a per-test loop, and the agent shell-looped the verb — producing an allowlist prompt per chunk and breaking unattended runs.

**Why this priority**: The verdict seam is on the critical path of every generate run. Every run that produces grounded tests hits this seam. A per-test shell loop breaks unattended operation immediately.

**Independent Test**: Run `spectra ai ingest-verdict --suite unit-converter --all` against a workspace with N verdict files. Verify it completes in one call, emits `{"written": N, "skipped": 0, ...}`, and no shell loop is needed.

**Acceptance Scenarios**:

1. **Given** N verdict files in `.spectra/verdicts/critic-verdict-{id}.json` for suite `unit-converter`, **When** `spectra ai ingest-verdict --suite unit-converter --all --output-format json` is run, **Then** a single call ingests all N verdicts and `written == N` in the summary.
2. **Given** the same workspace, **When** the per-test mode `spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-TC-100.json` is run, **Then** it continues to work exactly as before — backward compatibility preserved.
3. **Given** a workspace with no verdict files for the suite, **When** `--all` is run, **Then** it exits 0 with `written: 0, skipped: 0`.

---

### User Story 2 — Update Batch Ingest (Priority: P2)

The repair skill ingests all repaired tests for a suite in a single deterministic call. Before this spec, `ingest-update` was per-test-only; the skill drove it per-entry in the manifest loop — a latent shell-loop breach identified by the OQ3.1 coverage matrix before it surfaced in production.

**Why this priority**: The update seam is on the hot repair path. Repair-heavy suites (many partials) will surface this breach on the next run without this fix.

**Independent Test**: Run `spectra ai ingest-update --suite checkout --all` against a workspace with staged update files. Verify one call, correct count, backward-compatible per-entry mode unchanged.

**Acceptance Scenarios**:

1. **Given** N updated test files staged for suite `checkout`, **When** `spectra ai ingest-update --suite checkout --all --output-format json` is run, **Then** all N are ingested in a single call with `written == N`.
2. **Given** the same workspace, **When** the per-entry mode `spectra ai ingest-update checkout --test-id TC-100 --from updated-TC-100.json` is run, **Then** it continues to work exactly as before.
3. **Given** a workspace with no staged updates for the suite, **When** `--all` is run, **Then** it exits 0 with `written: 0`.

---

### User Story 3 — Manifest Consumption Without Interpreter (Priority: P2)

When `compile-repair-batch` emits a large manifest (≈198.9KB for 17 partials), the skill explicitly prescribes reading it via the Read tool and iterating in-context — never piping to `python`, `jq`, or any interpreter. Before this spec, the skill was silent on HOW to consume the spilled manifest; the agent improvised `python -c` to extract IDs, and the allowlist offered "allow python for all projects" — the inverse of the non-stop contract.

**Why this priority**: The Python-parse is the most dangerous class of breach — computing on spectra output with an external interpreter. Accepting the "for all projects" allowlist option would permanently normalize arbitrary scripting.

**Independent Test**: Read `spectra-generate.md` Step 8b — it contains an explicit manifest-consumption instruction naming the Read tool and prohibiting interpreter pipes. Verified by inspection; no code change.

**Acceptance Scenarios**:

1. **Given** a repair run where `compile-repair-batch` emits >50KB, **When** the agent drives Step 8b, **Then** it uses the Read tool on the spilled manifest file and iterates over JSON entries in-context — zero `python -c` / `jq` / interpreter pipes.
2. **Given** the skill instruction is in place, **When** the agent encounters the manifest step, **Then** it never accepts or offers a "scripting for all projects" allowlist option.

---

### User Story 4 — General Non-Stop Contract Preamble (Priority: P3)

The non-stop contract is stated once as a property of every seam in Steps 7–9 — not as a per-seam guard. Before this spec, the contract appeared only at the grounding-ingest guard, making it look seam-specific. Two seams breached because the rule wasn't stated as a general property of the entire step flow.

**Why this priority**: The preamble prevents future breaches from seams not yet surfaced. Without it, every new per-test verb is a latent breach discovered by the next live run.

**Independent Test**: Read `spectra-generate.md` — a top-of-section preamble before Step 7 names all four prohibited behaviors and the STOP+report response. The per-seam guards remain as specific instances under the general rule.

**Acceptance Scenarios**:

1. **Given** any step in Steps 7–9 where no single spectra call exists, **When** the agent reads the preamble, **Then** it stops and reports a missing affordance rather than looping or piping.
2. **Given** the preamble is in place, **When** future steps are added, **Then** they are covered by the general rule without requiring a per-seam guard for each.

---

### Edge Cases

- `ingest-verdict --all` with verdict files belonging to a different suite present in the same verdicts directory: only the specified suite's verdicts are processed; others are silently skipped (filtered by `_index.json` lookup).
- `ingest-update --all` when a staged update file is malformed (parse error): fail-loud on that entry, continue with the rest, report error count in summary.
- `compile-repair-batch` output that fits inline (small partial count, no harness spill): the manifest-consumption instruction works identically — the agent reads the inline JSON result in-context.
- Allowlist prompt for `python -c` or `jq`: the preamble names this explicitly as "never accept"; the agent must always deny.
- `ingest-verdict --all` when `.spectra/verdicts/` does not exist: exit 0 with `written: 0`.
- Both `--from` and `--all` passed simultaneously: command fails with a clear error — the two modes are mutually exclusive.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `spectra ai ingest-verdict` MUST accept `--suite <s> --all` batch mode that enumerates all `.spectra/verdicts/critic-verdict-*.json` files for the workspace, filters to the named suite via `_index.json` lookup, classifies each verdict, and emits a summary JSON with `written`, `skipped_already_classified`, and `errors` counts — mirroring the shape of `ingest-grounding --all`.

- **FR-002**: `spectra ai ingest-update` MUST accept `--suite <s> --all` batch mode that enumerates all staged update files for the suite, ingests each via the shared write-back service, and emits a summary JSON with `written`, `skipped`, and `errors` counts — same pattern as FR-001.

- **FR-003**: Both FR-001 and FR-002 MUST reuse the existing shared write-back service with no logic duplication. Per-test/per-entry `--from` modes MUST remain unchanged and backward compatible. `--from` and `--all` MUST be mutually exclusive.

- **FR-004**: `spectra-generate.md` MUST be updated so: (a) Steps 8a and 8b replace per-test `ingest-verdict --from` loops with a single `ingest-verdict --suite {s} --all` call after the critic loop; (b) per-entry `ingest-update` invocations are replaced with a single `ingest-update --suite {s} --all` call after the repair loop. The existing `ingest-grounding --all` call and guard (US3) MUST remain unchanged.

- **FR-005**: `spectra-generate.md` Step 8b MUST include an explicit manifest-consumption instruction: use the Read tool on the spilled manifest file if stdout exceeded inline capacity; iterate over JSON entries in-context for per-test repair; do NOT pipe `compile-repair-batch` output to `python`, `jq`, or any interpreter; do NOT accept a "scripting for all projects" allowlist option. The full prompt per entry is needed and must be read, not transformed.

- **FR-006**: `spectra-generate.md` MUST add a top-of-section preamble before Step 7 stating the non-stop contract as a property of EVERY seam: each step is a single `Bash(spectra *)` call or a Write to a spectra-authored path; if no single spectra call covers a step, STOP and report a missing affordance. The preamble MUST explicitly name and prohibit: (a) shell loops over per-test verbs, (b) piping spectra output to any interpreter (`python`/`jq`/etc.), (c) manual `.md` file editing, (d) verdict field rewriting.

- **FR-007**: `cli-reference.md` MUST document `ingest-verdict --suite <s> --all` and `ingest-update --suite <s> --all`, each mirroring the existing `ingest-grounding --all` entry in format.

- **FR-008**: `ingest-grounding --all` (Spec 073), the three 073 consumer fixes, and all Spec 075 changes MUST remain unchanged — hard regression gate. `Spectra.Core` + `TestPersistenceService` tests MUST pass unmodified.

### Key Entities

- **Verdict batch**: the set of `.spectra/verdicts/critic-verdict-{id}.json` files belonging to a given suite, consumed in one `ingest-verdict --all` call.
- **Update batch**: the set of staged updated test files for a suite's repair pass, consumed in one `ingest-update --all` call.
- **Non-stop contract**: the rule that every seam step is a single `Bash(spectra *)` call or a Write to a spectra-authored path — stated as a general skill-level preamble, not per-seam.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A full generate+repair run on a clean suite completes with zero allowlist prompts on the verdict, update, and manifest seams (down from 2+ prompts per chunk pre-fix).
- **SC-002**: `ingest-verdict --suite {s} --all` ingests all N verdicts in one call; `written` count in the JSON summary equals the number of verdict files for that suite.
- **SC-003**: `ingest-update --suite {s} --all` ingests all M updates in one call; `written` count matches the number of staged update files.
- **SC-004**: A repair run over a manifest with 17+ partial entries produces zero interpreter-pipe Bash calls (`python -c`, `jq`, etc.) — the agent reads the manifest in-context via the Read tool.
- **SC-005**: All existing tests (Core 568 + CLI 1266 + Execution 228) continue to pass unmodified after the new batch verbs are added.
- **SC-006**: `spectra-generate.md` contains a top-of-section non-stop preamble covering all four prohibited behaviors before Step 7, confirmed by inspection.

---

## Assumptions

- The staging path for `ingest-update --all` (where per-test repair output files are written before batch ingest) follows the same convention as verdict files (`.spectra/updates/{suite}/updated-{id}.json` or equivalent) — to be confirmed from `IngestUpdateCommand.cs` and the repair flow before implementing FR-002.
- `ingest-verdict --all` idempotency: re-running on already-classified verdicts should be safe (skip already-done entries), consistent with `ingest-grounding --all` behavior.
- `record-drop` and `delete` per-test loops are explicitly deferred — low-frequency (hallucinated tests only), not in the unattended hot path.

---

## Out of Scope

- `record-drop --all` / `delete --all` (low-frequency, deferred by frequency — not dismissed)
- `compile-repair-batch --ids-only` (would break repair — full prompt required per entry)
- Tool-results spill changes (Claude Code harness feature, not Spectra CLI)
- A runtime/code block on interpreter use
- `compile-critic-prompt` per-test invocation (subagent-driven, irreducibly per-test — not the same breach class)
- MCP, critic model-family changes, test-content changes
