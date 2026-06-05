# Phase 1 — Data Model: Prompt-compiler + generation handoff inversion

No persistent schema changes. `TestCase`, `MetadataIndex`, and the on-disk `.md`/`_index.json` formats are unchanged. The only new types are the two in-process result records that carry the compile/ingest outcomes.

## Entity: `PromptCompileInputs`

The declared inputs the compiler consumes. (May be passed as constructor/method args rather than a formal record — modeled here for clarity.)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `UserPrompt` | `string` | yes | Focus/behaviors text. Whitespace-only ⇒ treated as missing. |
| `RequestedCount` | `int` | yes | MUST be > 0. |
| `CriteriaContext` | `string?` | yes | The Spec 045 criteria context. Null/whitespace ⇒ refuse-to-emit. |
| `ProfileFormat` | `string?` | no | Falls back to embedded default via `ProfileFormatLoader`. |
| `TestimizeData` | `TestimizeDataset?` | no | Optional; collapses its prompt block when null/empty. |
| `TemplateLoader` | `PromptTemplateLoader?` | no | When present, renders `test-generation.md`; else inline fallback. |

**Validation rules** (refuse-to-emit, FR-004):
- `UserPrompt` null/whitespace ⇒ `Failure("user_prompt", …)`
- `RequestedCount <= 0` ⇒ `Failure("count", …)`
- `CriteriaContext` null/whitespace ⇒ `Failure("criteria_context", …)`

## Entity: `PromptCompileResult`

Discriminated outcome of compilation.

| Member | Type | Meaning |
|--------|------|---------|
| `IsSuccess` | `bool` | true ⇒ `Prompt` is populated. |
| `Prompt` | `string?` | The fully-grounded prompt (success only). |
| `MissingInput` | `string?` | Machine-readable name of the failing input (failure only). |
| `Message` | `string?` | Human-readable reason (failure only). |

Factory: `PromptCompileResult.Success(string prompt)`, `PromptCompileResult.MissingRequired(string input, string message)`.

**Determinism invariant**: `Compile(x) == Compile(x)` byte-for-byte. No `DateTime.Now`, no GUIDs, no unordered enumeration in the emitted text.

## Entity: `IngestResult`

Outcome of ingesting agent-generated content at the boundary.

| Member | Type | Meaning |
|--------|------|---------|
| `IsSuccess` | `bool` | true ⇒ content parsed, validated, persisted. |
| `PersistedTests` | `IReadOnlyList<TestCase>` | Tests written (success only; empty on failure). |
| `ErrorCode` | `string?` | Machine-readable failure class (failure only). |
| `Errors` | `IReadOnlyList<string>` | Specific messages a skill re-prompts against. |

**`ErrorCode` enumeration** (the machine-readable contract FR-006/FR-007 depend on):

| Code | Triggered when |
|------|----------------|
| `EMPTY_CONTENT` | Agent content is null/whitespace, or no `[` JSON array found. |
| `MALFORMED_JSON` | Extracted text does not parse as a JSON array. |
| `TRUNCATED` | A `[` opened but the array never closed (token-limit cut-off). No salvage. |
| `NO_TESTS` | Parsed an array but zero valid test objects in it. |
| `SCHEMA_INVALID` | One or more parsed tests fail `TestValidator` (codes echoed in `Errors`). |

**Zero-persistence invariant** (FR-006): any non-success `IngestResult` MUST leave `test-cases/` and every `_index.json` byte-for-byte unchanged — persistence is only reached after parse + validate both pass for the whole batch (batch atomicity).

## Reused unchanged (no modification)

- `TestCase` (`Spectra.Core.Models`) — the parse target and persist payload.
- `ValidationResult` / `ValidationError` (`Spectra.Core.Models`) — boundary validation output; `ValidationError.Code` is the machine-readable schema-error surface.
- `MetadataIndex` + `IndexGenerator`/`IndexWriter` (`Spectra.Core.Index`) — index regeneration inside persistence.
- `TestPersistenceService` (`Spectra.CLI.IO`) — the sole write+index path (FR-008).

## State / flow

```
compile-prompt:  inputs ──▶ PromptCompiler.Compile ──▶ Success(prompt) ▶ stdout (writes nothing)
                                                   └──▶ MissingRequired(input) ▶ stderr + non-zero exit

ingest-tests:    content ─▶ Extract+Parse ─▶ Validate(TestValidator) ─▶ Persist(TestPersistenceService) ▶ exit 0
                              │ fail            │ fail                     (only reached if both pass)
                              ▼                 ▼
                        IngestResult.Failure(code, errors) ▶ stderr + non-zero exit  (nothing persisted)
```
