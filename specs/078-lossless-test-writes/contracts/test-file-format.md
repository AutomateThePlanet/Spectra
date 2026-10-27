# Contract: test-case file write (Spec 078)

Every writer path goes through `TestFileWriter.FormatTestCase(TestCase)`:
- `TestPersistenceService` (ingest-tests, ingest-update)
- `GroundingWriteBackService` (ingest-grounding)
- `UpdateHandler.ApplyChangesAsync` (orphan marking, updates)
- `BatchWriteTestsTool`
- `ReviewFlaggedHandler` (accept)

## Guarantees

- **G1, value round-trip**: for any file `f` that parses, `parse(write(parse(f)))` is equivalent to `parse(f)`. This
  covers every `TestCase` property except `FilePath`, plus `Layout` (same entries and segments; on the first write
  unknown keys move behind the known ones, so entry order is only stable from then on, see G2).
- **G2, idempotence**: `write(parse(write(t))) == write(t)`, byte for byte.
- **G3, minimal diff**: if a writer changes only properties `P` of a parsed test, then:
  - only the frontmatter entries and body sections backing `P` differ from the original
  - key reordering happens only when the original didn't follow the fixed order in data-model.md
- **G4, unknown content**:
  - unknown top-level frontmatter keys are emitted verbatim, after the known keys, in original relative order
  - unknown body segments (`Leading`, `Intro`, `Unknown` sections) are emitted verbatim in their original positions
- **G5, newline**: the original file's newline sequence is kept. New files use `\n`.
- **G6, canonical escaping**:
  - changed or new values are emitted with YAML quoting, so any string round-trips
  - this covers `: `, ` #`, leading indicator characters, backslashes, `\r`, `\n` and quotes
  - `score` and `verified_at` use the invariant culture

## Writer-specific invariants

| Path | Changes only |
|---|---|
| Orphan marking | `status`, `orphaned_reason`, `orphaned_date` (date kept if it was already orphaned) |
| ingest-grounding / review accept | `grounding` |
| ingest-update | `Title`, `Preconditions`, `Steps`, `ExpectedResult`, `TestData`, `ScenarioFromDoc`, `SourceRefs`, `Criteria` (from the edit); everything else from the original, including `EstimatedDuration` and `Layout` |
