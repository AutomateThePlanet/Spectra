# Phase 0 Research: Criteria Coverage Guards (Spec 048)

**Branch**: `048-criteria-coverage-guards`
**Date**: 2026-06-02
**Spec**: [spec.md](./spec.md)

## Source code reconnaissance

Verified line numbers against the merged 047 changes (`main` at the merge commit `0429195`). The user's input had two minor path errors that the recon corrected:

| Symbol | Real path | Confirmed lines | Notes |
|---|---|---|---|
| `CriteriaSource` (model) | `src/Spectra.Core/Models/Coverage/CriteriaSource.cs` | `:8-30` (class), fields `:10-29` | Current fields: `file`, `source_doc`, `source_type`, `doc_hash`, `criteria_count`, `last_extracted`, `imported_at`. No outcome field. |
| `AnalyzeHandler.RunExtractCriteriaAsync` — upsert sites | `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` | extract path `:639-657` (update existing source `:639-645`, add new `:646-657`); import path `:1143-` | Both paths are cacheable — Spec 047 only reaches them on `IsCacheable == true`. Both need the outcome set. |
| `DocsIndexHandler.TryExtractCriteriaAsync` | `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs` | per-doc loop `:281-304`; `criteriaExtracted` captured at `:199`; result built at `:216-234`; `_skipCriteria` gate at `:193`; `documentsIndexed` is `newLayout.Manifest.TotalDocuments` (`:171,221`) | The corpus-zero check sits between `:199` (loop returns) and `:216` (result built), inside the `if (!_skipCriteria)` branch. |
| `DocsIndexResult` | `src/Spectra.CLI/Results/DocsIndexResult.cs` | `CriteriaExtracted` nullable int at `:53-55`, `JsonIgnore WhenWritingNull` already used pattern | Add `CriteriaWarning string?` alongside. |
| `GenerateHandler.LoadCriteriaContextAsync` | `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | `:2427-2503`; matching at `:2452-2462`; file-name fallback `:2467-2482`; **last-resort all-criteria fallback `:2485-2486`** | The last-resort fallback is what makes a naked `string?` return insufficient for the no-match note. |
| `GenerateHandler.ExecuteDirectModeAsync` — criteria load + result | `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | criteria load `:672`; final-result build `:985-1013`; "all behaviors covered" early result `:462-477`; "no tests" early result `:609-624` and `:924-940` | The criteria load happens once before the batch loop; the result is built once after. Both early-exit results also need to consider the note when the underlying suite has zero matches — but those are edge cases (no tests written), so we attach the note only on paths that proceed with generation. The four final-result sites are all candidates; we add the note where it makes sense (any result for which tests were written or attempted). |
| `GenerateHandler.ExecuteFromDescriptionAsync` | `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | criteria load `:1789-1798`; JSON result `:1852-1867` | The from-description path **does not** call `WriteResultFile` — it only emits the JSON to stdout when `--output-format json`. The note thus needs to ride on the JSON-stdout result. Confirmed FR-010 ("present in the structured result regardless of console verbosity") is talking about that JSON output. |
| `GenerateResult` | `src/Spectra.CLI/Results/GenerateResult.cs` | existing fields `:5-57`; pattern `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` used throughout | Add `Notes IReadOnlyList<string>?` matching the same pattern. |
| Bundled SKILLs | `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`, `…/spectra-docs.md` | files exist | The user's input said `.github/skills/` — that path is the install target after `spectra update-skills`, not the source-of-truth bundle. |

## Decisions

### D1. Outcome field encoding: string-typed with documented enum

**Decision**: `CriteriaSource.Outcome` is a `string` property with `[YamlMember(Alias = "outcome")]` and default value `"extracted"`. The valid values are documented in code (XML doc comment) and in the contract doc (`extracted`, reserved for future: `empty_response`, `parse_failure`). Round-trip uses YamlDotNet — missing key on read defaults to the C# property default (`"extracted"`), preserving FR-002 backward-compatibility without a custom converter.

**Rationale**:
- Matches the existing `source_type` precedent (`source_type: "document"` is also a string with one common write-value and others documented).
- YamlDotNet's default deserializer fills missing scalar fields with the property default, so legacy entries automatically deserialize as `extracted` — no custom logic needed.
- A strongly typed enum would be slightly cleaner but introduces YamlDotNet conversion plumbing for one rarely-changing value. Constitution V (YAGNI): one field, one current value, prefer the simpler shape.

**Alternatives considered**:
- C# enum `ExtractionOutcome` with `[YamlMember]`: requires either a converter or all enum names to match YAML lower-snake-case. Real cost for negligible gain.
- Reuse `Spectra.CLI/Agent/Copilot/ExtractionOutcome` (the 047 enum): lives in `Spectra.CLI` (not `Spectra.Core`), creating a wrong-way dependency. Rejected.

### D2. Zero-criteria warning gate — derive from `criteriaExtracted == 0`, not from doc count alone

**Decision**: The warning fires when the docs-index run executed criteria extraction AND the corpus-wide criteria total came back zero. Concretely, inside `TryExtractCriteriaAsync` after the per-doc loop, when `extracted.Count == 0 && documentMap.Documents.Count > 0`. The warning is suppressed in `--dry-run` (the existing dry-run branch returns before this point) and is naturally suppressed when `--skip-criteria` is passed (`TryExtractCriteriaAsync` is not called).

**Rationale**:
- FR-005: condition is "indexed at least one document AND the corpus-wide acceptance-criteria total for the run is zero." `documentMap.Documents.Count > 0` captures the indexed-at-least-one half; `extracted.Count == 0` captures the zero half.
- The warning message is built once (in the handler) and threaded through the existing `_progress.Warning` and the new `criteria_warning` JSON field. Same message string for both surfaces; SKILLs render the JSON field verbatim.
- Returning the warning from `TryExtractCriteriaAsync` (alongside the count/file tuple) keeps the assembly logic at the existing call site at `:199`. Tuple becomes `(int? criteriaCount, string? criteriaFile, string? criteriaWarning)`.

**Alternatives considered**:
- Compute the corpus criteria total after `TryExtractCriteriaAsync` returns. Rejected: the count is internal to that method; promoting it to the caller widens the surface unnecessarily.
- Fire the warning unconditionally whenever `criteriaExtracted == 0`. Rejected: when `_skipCriteria` is set, the warning would falsely accuse the user. The gate must be inside the `!_skipCriteria` branch.

### D3. `LoadCriteriaContextAsync` refactor — return a record, not a tuple

**Decision**: Change the return type from `Task<string?>` to `Task<CriteriaContextResult>` where `CriteriaContextResult` is an internal sealed record:

```csharp
internal sealed record CriteriaContextResult(
    string? Context,            // formatted markdown context, null when no criteria on disk
    int SuiteMatchedCount,      // count after the component/source-doc/file-name match passes (BEFORE last-resort fallback)
    int TotalCriteriaCount);    // total criteria loaded (for diagnostics; informational)
```

The no-match note fires when `SuiteMatchedCount == 0` regardless of `Context` being null or non-null (the latter case means the function fell back to "all criteria" because nothing actually matched the suite).

**Rationale**:
- A named record at the call site is more readable than `(string?, int, int)`.
- Internal-only — no public API surface change.
- The third field (`TotalCriteriaCount`) is cheap to compute and lets the note message optionally distinguish "criteria exist but none match this suite" from "no criteria at all", though FR-009's wording does not require that distinction. We leave the field on the record for future tuning; the initial implementation uses one note message regardless.

**Alternatives considered**:
- Tuple `(string? Context, int SuiteMatchedCount)`: equivalent functionally; rejected on readability grounds (named members beat anonymous positional ones).
- Return `string?` and add a sibling method `CountSuiteMatchedCriteriaAsync`: duplicates the file-reading and filtering logic. Rejected.

### D4. Note text wording and stability

**Decision**: Single fixed message string per surface:

- Docs-index zero-criteria: `"Indexed {N} document(s) but extracted 0 acceptance criteria. Test generation will not be able to link criteria. Run: spectra ai analyze --extract-criteria"`
- Generate no-match: `"No acceptance criteria matched suite '{suite}'. Generated tests have no criteria linkage; acceptance-criteria coverage will not include them. Run 'spectra ai analyze --extract-criteria' if criteria are expected."`

Both messages are formatted at the handler and surfaced verbatim in the JSON field (`criteria_warning` / each entry in `notes`). The SKILLs render them as-is, prefixed with a marker (e.g., `⚠️` for the warning, plain rendering for the note).

**Rationale**:
- FR-014 requires the recovery command to be named. Hard-coding the literal command keeps the message a deterministic projection from inputs — supports SC-007.
- A single string per surface keeps test assertions simple (`Assert.Contains("spectra ai analyze --extract-criteria", warning)`).
- The note vs. warning distinction is intentional: `docs index` blocks no work, but the user just ran an extraction command and got nothing — the surface is a warning. Generation is best-effort with respect to criteria; a "note" framing matches the everyday workflow.

**Alternatives considered**:
- Configurable wording via `spectra.config.json`. Rejected — Constitution V.
- Distinct sub-warnings for "criteria-extracted=0 because all docs were affirmed-empty" vs "because all extractions were inconclusive". Rejected — the user-actionable advice is the same in both cases. The Edge Cases section of the spec accepts this conflation explicitly.

### D5. Where the generate note attaches — direct mode + from-description; not the early-exit results

**Decision**: The no-match note attaches to the final `GenerateResult` built in:

- `ExecuteDirectModeAsync` final result (`:985-1013`) — covers the batch flow's normal completion.
- `ExecuteFromDescriptionAsync` JSON-stdout result (`:1852-1867`) — covers the from-description flow.

It does NOT attach to the early-exit results inside `ExecuteDirectModeAsync` (`:462-477` "all behaviors covered", `:609-624` "no gaps for focus", `:924-940` "no tests generated") even though those technically also complete without criteria linkage. Rationale: those branches return zero tests for unrelated reasons (analysis says "nothing to do" or the count argument was 0); the no-match note in that context would be noise. The note's value is "you wrote tests, but they are not linked to criteria." When no tests are written, the message doesn't add user value.

The `ExecuteInteractiveModeAsync` path (interactive flow, `:1045-1667`) is **out of scope** for the note — the spec only names "batch flow" and "from-description flow." The interactive flow is gated on a TTY and the user is already in a chatty session; quiet structured notes are not its primary surface.

**Rationale**:
- Matches FR-009's wording: "either the batch flow or the from-description flow."
- The interactive flow's eventual final result is built in a different code path that doesn't carry the criteria load result through; touching it would widen the spec's blast radius without proportionate user value.
- The early-exit paths in the batch flow already have their own clearer messages ("All behaviors already covered by existing tests"); adding the no-match note there would be redundant.

**Alternatives considered**:
- Attach to every `GenerateResult` build site. Rejected per above — noise on no-test-written paths.
- Attach to the interactive flow's final result too. Out of scope per the spec's wording; can be added in a follow-up if needed.

### D6. Quiet-mode suppression for the human echo, not the JSON

**Decision**: Both the docs-index warning and the generate note are written into the JSON result unconditionally (the JSON is always produced when `--output-format json`; the `.spectra-result.json` file is always produced via the progress manager on docs-index path; from-description writes JSON to stdout). The human-facing echo via `_progress.Warning` / `_progress.Info` is suppressed only when `_verbosity` is `Quiet` or below — matching the existing verbosity convention in both handlers.

**Rationale**: FR-010 requires the note to be present in the structured result regardless of console verbosity. The existing `_progress` helpers already gate console output on `_verbosity`; the JSON-result write is independent. No new code paths needed — the suppression "just works" by virtue of where each surface is written.

**Verified via tests**: `Generate_Note_PresentInJson_EvenWhenQuiet` exercises this contract by setting `_verbosity = VerbosityLevel.Quiet` and asserting on the JSON payload, not stdout.

## Open questions resolved (no NEEDS CLARIFICATION remaining)

- **Outcome field encoding** → string with documented values, default `"extracted"` (D1).
- **Zero-criteria gate scope** → inside the `!_skipCriteria` branch; both conditions (`documentsIndexed > 0` AND `criteriaExtractedTotal == 0`) must hold (D2).
- **No-match detection in generate** → record-typed return from `LoadCriteriaContextAsync` exposing `SuiteMatchedCount` (D3).
- **Message wording** → fixed strings per surface, names the recovery command (D4).
- **Where the note attaches** → direct mode + from-description; not the early-exits, not interactive (D5).
- **Verbosity behavior** → JSON always carries; console echo suppressed under quiet (D6).
