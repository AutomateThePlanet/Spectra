# Phase 0 Research: Targeted test updates (inverted update seam)

All "NEEDS CLARIFICATION" from Technical Context resolved below. Each decision is grounded in the existing seam code (053/059) that this feature mirrors.

## R1 — Where the new commands live and how they register

**Decision**: Add `CompileUpdatePromptCommand` and `IngestUpdateCommand` in `src/Spectra.CLI/Commands/Generate/`, registered in `AiCommand` (`src/Spectra.CLI/Commands/Ai/AiCommand.cs:17-37`) alongside the other compile/ingest pairs.

**Rationale**: All four existing seams (generation 053, extraction 054, critic 055, analysis 059) register their `Compile*Command`/`Ingest*Command` pair in `AiCommand`'s constructor and physically live in `Commands/Generate/`. Mirroring keeps the seams visually parallel and lets the new commands share the same helpers (`TestPersistenceService`, `TestValidator`, `PromptTemplateLoader`, `TestCaseParser`). The existing `UpdateCommand` stays registered as-is — the heuristic classifier flow it drives is reused as the **selector**.

**Alternatives considered**: A separate `Commands/Update/` location — rejected; it would split the seam from the helpers it mirrors and obscure the parallelism.

## R2 — Compile command shape (deterministic, model-free)

**Decision**: `spectra ai compile-update-prompt` takes a suite plus identifies the OUTDATED test(s) and emits — to stdout, writing nothing — a prompt containing: the existing test artifact(s), the changed source/criteria, and explicit "edit, don't regenerate; preserve id/structure/manual fields" instructions. It loads grounding via the same `CriteriaContextLoader` + `ProfileFormatLoader` + `PromptTemplateLoader` used by `CompilePromptCommand` (`CompilePromptCommand.cs:96-99`). Exit codes mirror compile: `0` success, `4` refused (missing required input), `1` error.

**Rationale**: `CompilePromptCommand` is the proven template — deterministic, model-free, stdout-only, refuse-on-missing-input. The update compile differs only in *what* it assembles (original test + changed source, not a fresh generation request) and *which* template it loads.

**Open mechanics resolved**:
- **How does it learn which tests are OUTDATED?** Two viable inputs: (a) the skill passes specific test ids via an option (e.g. `--test-ids TC-101,TC-104`), having already run the classifier-backed `ai update --diff`; or (b) the command itself runs `TestClassifier` to select OUTDATED tests for the suite. **Chosen: the command accepts explicit test id(s)** (`--test-id` / positional), keeping it deterministic and single-responsibility; the skill is responsible for obtaining the OUTDATED set from the classifier (reuse, FR-005). This avoids duplicating classifier invocation and keeps compile a pure assembler.
- **One prompt per test or batched?** **One test per compile invocation** (the skill loops), mirroring the per-test critic seam (055) and keeping the edit prompt small and the drift comparison 1:1. Batching is a later optimization (YAGNI).

**Alternatives considered**: Embedding classification inside compile — rejected (duplicates `TestClassifier` invocation, couples compile to classification, violates single-responsibility). Whole-suite batched prompt — rejected for the first cut (larger blast radius, harder drift attribution).

## R3 — The update prompt template

**Decision**: Add a new template `test-update` (e.g. `prompts/test-update.md`) loaded by `PromptTemplateLoader`, alongside `test-generation`. Placeholders: the original test (serialized), the changed source/criteria block, the suite name, and the explicit edit-don't-regenerate / preserve-invariants directives.

**Rationale**: Generation uses a named template (`"test-generation"`, `PromptCompiler.cs:105`) resolved through `PromptTemplateLoader.Resolve(template, dict)`. A dedicated `test-update` template keeps the update instructions (edit semantics, invariant preservation) separate and independently maintainable, and lets `prompts reset`/`prompts show` surface it like the others.

**Alternatives considered**: Reusing `test-generation` with extra context — rejected; generation framing ("create N new tests") contradicts edit semantics and would confuse the model.

## R4 — Ingest command + ingestor (fail-loud, invariant-protecting)

**Decision**: `spectra ai ingest-update` mirrors `IngestTestsCommand` (reads `--from` file or stdin; loads config; builds `TestPersistenceService`; emits JSON/human; exit `0/1/5/6`). It delegates to a new `UpdatedTestIngestor` that mirrors `GeneratedTestIngestor` (same `ExtractJson`/`TryParseArray`/`ParseTestCase`/`TestValidator.ValidateAll` pipeline, same `IngestResult`/`IngestErrorCode`), then adds three deterministic invariant-protection steps **before persist**:

1. **Id from original**: the persisted test's id is the original on-disk test's id; the model's id is ignored (no `PersistentTestIdAllocator` call — this is an edit, not a create).
2. **Manual-field re-assertion**: if the original test carries a `Manual` verdict (`GroundingMetadata.Verdict == VerificationVerdict.Manual`) and/or human-authored note(s), those are copied from the original onto the edited candidate, overriding whatever the model emitted.
3. **Drift guard**: compare the edited candidate against the original; any change to a field **not implicated** by the update is collected into a drift report. A non-empty drift report is a fail-loud `IngestResult.Failure` (new error code `DRIFT_DETECTED`) — nothing persisted.

Persist via `TestPersistenceService.PersistAsync(testsPath, suite, [edited], allForIndex, ct)` where `allForIndex` is the existing suite set with the original replaced by the edited test (the same merge `GeneratedTestIngestor.MergeForIndex` does, where incoming wins on id).

**Rationale**: Reusing the parse/validate pipeline and `IngestResult` contract keeps the fail-loud, no-salvage, specific-error guarantees identical to generation, so the skill's bounded-retry logic is the same. The three extra steps are pure deterministic functions over (original, candidate) — the model is never trusted with the invariants (FR-003).

**Alternatives considered**: Per-field surgical merge (model returns only changed fields) — rejected as primary per the spec; documented fallback if the drift guard proves noisy. Trusting the model to keep id/manual fields — rejected (no determinism guarantee).

## R5 — What "manual fields" and "structure" mean concretely

**Decision**: "Manual fields" = (a) a pre-existing `Manual` `GroundingMetadata` (verdict + its `source`/`created_by`/`note` frontmatter), and (b) any human-authored note carried on the original test. "Structure" preserved by edit semantics = the test's identity and shape (id, file path, the frontmatter/section layout) — the model edits content within that shape rather than re-emitting a differently-shaped artifact. The exact field list is finalized at implementation time by reading `TestCase` (`src/Spectra.Core/Models/TestCase.cs`) and `GroundingMetadata`/`GroundingFrontmatter` (`src/Spectra.Core/Models/Grounding/`); no model change is permitted (those types are reused unchanged).

**Rationale**: The generation flow already honors `Manual` (the critic is skipped and grounding written with `Generator="user"`, `Critic="none"`, `Score=1.0` — `GroundingFrontmatter`/`GroundingMetadata`). Update must re-assert that same metadata from the original so an edit never silently "un-manuals" a test or strips its note.

## R6 — Drift-guard scope ("fields not implicated by the doc change")

**Decision**: The drift guard compares the edited candidate to the original field-by-field and classifies each changed field as *expected* (content fields the edit is permitted to touch: title, steps, expected result, test data, scenario-from-doc, criteria links, source refs) or *protected/out-of-scope* (id, priority unless the change implicates it, component, dependencies, tags, and the manual/grounding fields). Any change to a protected field → drift report entry → fail-loud. If precise attribution is impractical for a field, the guard **errs toward surfacing** (fail-loud) rather than silently accepting (spec Assumptions).

**Rationale**: Whole-test edit is simpler than surgical merge but risks silent collateral edits; the drift guard converts that risk into a visible, blocking event (FR-003c, US3). Erring toward fail-loud matches the seam's no-salvage philosophy.

**Alternatives considered**: A semantic/diff-similarity threshold — rejected for the first cut as non-deterministic; exact field comparison is deterministic and testable. The threshold approach is a possible future refinement if exact comparison proves too strict.

## R7 — Skill rewrite + bounded retry

**Decision**: Rewrite `spectra-update.md` to drive: obtain OUTDATED set (classifier, via `ai update --diff` or equivalent) → per OUTDATED test, `compile-update-prompt` → edit in-session → write candidate JSON → `ingest-update` → on exit `5/6` (or new `DRIFT_DETECTED`), bounded retry (max 2 attempts) feeding the specific error back. Remove the false "rewrites affected test cases" sentences (`spectra-update.md:9-12`, and the "rewritten" framing at lines 31, 64). Mirror `spectra-generate.md`'s compile→generate→ingest retry structure.

**Rationale**: The skill is the orchestrator that closes the seam (FR-004). The generate skill already encodes the exact bounded-retry-on-fail-loud pattern to copy.

## R8 — Tests to add vs. protect

**Decision**:
- **Net-new**: `UpdatePromptCompilerTests`, `UpdatedTestIngestorTests` (invariant protection, drift guard, bounded-retry error surfacing), `IngestUpdateBoundaryTests` (end-to-end seam + exit codes).
- **Rewrite**: `UpdateCommandTests` assertions that pin the "no AI / heuristic-only rewrite" behavior, to reflect that targeted rewrite now routes through the seam (the `ai update` classifier command itself is unchanged; only stale assertions about "rewriting" are corrected).
- **DO NOT TOUCH (regression net)**: `TestClassifierTests` (`tests/Spectra.Core.Tests/Update/`), `TestPersistenceServiceTests` (`tests/Spectra.CLI.Tests/IO/`). Breakage there = regression in a reused component, investigate rather than edit.

**Rationale**: Directly from the spec's Tests section and the constitution's Test-Required discipline. The classifier and persistence path are load-bearing reuse.

## Summary of decisions

| # | Decision |
|---|----------|
| R1 | New compile/ingest pair in `Commands/Generate/`, registered in `AiCommand`. |
| R2 | `compile-update-prompt`: deterministic, stdout-only, per-test, accepts explicit test id(s); skill supplies the OUTDATED set from the classifier. |
| R3 | New `test-update` prompt template alongside `test-generation`. |
| R4 | `ingest-update` → `UpdatedTestIngestor`: reuse generation parse/validate, add id-from-original + manual re-assertion + drift guard, persist via `TestPersistenceService`. |
| R5 | "Manual fields" = pre-existing `Manual` grounding + human note; "structure" = id/shape preserved by edit semantics. |
| R6 | Drift guard = deterministic field-by-field comparison; protected-field change ⇒ fail-loud `DRIFT_DETECTED`; err toward surfacing. |
| R7 | Rewrite `spectra-update.md` to drive compile→edit→ingest with bounded retry; delete false "rewrites" text. |
| R8 | Net-new compiler/ingestor/boundary tests; rewrite stale UpdateCommand assertions; never touch classifier/persistence tests. |
