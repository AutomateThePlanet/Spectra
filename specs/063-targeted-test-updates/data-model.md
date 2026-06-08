# Phase 1 Data Model: Targeted test updates (inverted update seam)

This feature adds **no new persisted data shapes** — updated tests remain `TestCase` Markdown+YAML files and per-suite `_index.json`. The new types are in-process seam contracts (parse/validate results and the drift report). Reused types are listed for reference and are **not modified**.

## Reused entities (UNCHANGED — do not modify)

### TestCase — `src/Spectra.Core/Models/TestCase.cs`
The artifact being edited. Source of truth for invariants on update.

| Field | Role in update |
|-------|----------------|
| `Id` (required) | **Invariant** — taken from the original on edit; never reallocated. |
| `FilePath` (required) | Preserved (id-derived path). |
| `Title` (required) | Editable content. |
| `Steps` | Editable content. |
| `ExpectedResult` (required) | Editable content. |
| `TestData` | Editable content. |
| `ScenarioFromDoc` | Editable content (the quote/paraphrase from changed source). |
| `Criteria`, `SourceRefs` | Editable links (the changed source/criteria the edit reconciles to). |
| `Priority` | **Protected** unless the doc change implicates it — drift-guarded. |
| `Component`, `Tags`, `DependsOn` | **Protected** — drift-guarded. |
| `Status`, `OrphanedReason`, `OrphanedDate` | Out of scope (orphan path, not the edit path). |
| `Grounding` (`GroundingMetadata`) | **Manual invariant** — see below. |

### GroundingMetadata / VerificationVerdict — `src/Spectra.Core/Models/Grounding/`
`VerificationVerdict.Manual` marks a human-curated test (Generator `"user"`, Critic `"none"`, Score `1.0`). When the original test's `Grounding.Verdict == Manual`, that grounding (and any `note`/`source`/`created_by` frontmatter) is re-asserted from the original onto the edited candidate, regardless of model output (FR-003b, FR-007, US2).

### TestClassifier / ClassificationResult / UpdateClassification — `src/Spectra.Core/Update/`
Reused **as selector only** (FR-005). `ClassifyBatch(tests, sourceContents, criteria)` → `IReadOnlyList<ClassificationResult>`; the seam consumes the `Outdated` subset. Not modified by this feature.

### TestPersistenceService — `src/Spectra.CLI/IO/TestPersistenceService.cs`
The only persist path (FR-002, FR-008). `PersistAsync(testsPath, suite, testsToWrite, allTestsForIndex, ct)` writes the edited test file(s) and regenerates `_index.json`. Reused unchanged.

### TestValidator / IngestResult / IngestErrorCode — `src/Spectra.CLI/...` / `Spectra.Core.Validation`
Reused: same whole-batch validation and the same fail-loud result contract the generation ingest uses.

## New in-process types

### UpdatePromptCompiler — `src/Spectra.CLI/Generation/UpdatePromptCompiler.cs`
Mirror of `PromptCompiler`. Pure function; no I/O, no model call.

- **Input**: the original `TestCase` (serialized), the changed source/criteria context block, suite name, profile format, `PromptTemplateLoader`.
- **Output**: the compiled update prompt string (or a `MissingRequired` refusal mirroring `PromptCompileResult`).
- **Template**: `test-update`.
- **Validation**: refuse (exit 4 at the command) when the original test or the changed source/criteria context is missing.

### UpdatedTestIngestor — `src/Spectra.CLI/Generation/UpdatedTestIngestor.cs`
Mirror of `GeneratedTestIngestor`, plus invariant protection. Reuses the same parse pipeline and `TestValidator`.

**Flow** (`IngestAsync(content, testsPath, suite, originalTest, existingTests, ct)`):
1. `ParseAndValidate(content)` → candidate `TestCase` (fail-loud reuse: `EMPTY_CONTENT`/`MALFORMED_JSON`/`TRUNCATED`/`NO_TESTS`/`SCHEMA_INVALID`).
2. **Id from original**: `candidate.Id ← originalTest.Id`; `candidate.FilePath ← originalTest.FilePath`.
3. **Manual re-assertion**: if `originalTest.Grounding?.Verdict == Manual` (and/or human note present), copy that grounding/note onto the candidate.
4. **Drift guard**: `DriftReport = CompareForDrift(originalTest, candidate)`. If non-empty → `IngestResult.Failure(DRIFT_DETECTED, …)` — nothing persisted.
5. **Persist**: `allForIndex = existingTests` with `originalTest` replaced by candidate (incoming-wins-by-id merge); `TestPersistenceService.PersistAsync(...)`.
6. Return `IngestResult.Success([candidate])`.

`ParseAndValidate` exposed as a pure static method for token-free unit tests, mirroring `GeneratedTestIngestor.ParseAndValidate`.

### DriftReport — value type returned by the drift guard
Deterministic comparison output.

| Field | Meaning |
|-------|---------|
| `HasDrift : bool` | True when any protected/out-of-scope field changed. |
| `Entries : IReadOnlyList<DriftEntry>` | One per unexpected change. |

`DriftEntry`: `{ FieldName : string, OriginalValue : string?, EditedValue : string? }` — surfaced in the fail-loud message so the user (and a retry) sees exactly what unexpectedly changed.

### New ingest error code: `DRIFT_DETECTED`
Added to the `IngestErrorCode` set used by the update ingestor (the generation set is unchanged; the update ingestor recognizes the existing codes plus this one). Maps to the content-invalid exit class (`5`) at `IngestUpdateCommand`, so the skill's bounded retry triggers on it like any other fail-loud outcome.

## Field classification for the drift guard (initial)

| Class | Fields | Behavior |
|-------|--------|----------|
| **Editable** (expected to change) | `Title`, `Steps`, `ExpectedResult`, `TestData`, `ScenarioFromDoc`, `Criteria`, `SourceRefs` | Changes accepted silently. |
| **Protected** (out of scope) | `Priority`, `Component`, `Tags`, `DependsOn` | Any change ⇒ drift entry ⇒ fail-loud. |
| **Invariant** (forced, never model-driven) | `Id`, `FilePath`, `Grounding` when `Manual`, human note | Re-asserted from original before comparison; a model attempt to change them is overridden, not reported as drift. |

> The exact editable/protected partition is finalized when reading `TestCase` during implementation; the guard errs toward **protected** (fail-loud) for any field whose class is ambiguous (spec Assumptions, R6).

## State / lifecycle

No new persistent state. The transient lifecycle of one update is: **OUTDATED (classifier) → compiled prompt → edited candidate (untrusted) → invariant-protected + drift-checked → persisted (id preserved) | rejected (fail-loud, nothing written)**.
