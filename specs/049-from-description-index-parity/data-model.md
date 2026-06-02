# Phase 1 Data Model: From-Description Write & Index Parity

**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This spec adds **no new persisted entities** and **does not modify any existing serialized shape**. The on-disk JSON of `_index.json` is byte-for-byte equivalent to today's batch-generated output for the same inputs (regression-guarded by `BatchIndexEquivalenceTests`). The data-model work is limited to introducing one in-memory orchestration type.

## Touched Existing Types (no change)

| Type | Location | Role in this spec |
|------|----------|-------------------|
| `TestCase` | `src/Spectra.Core/Models/TestCase.cs` | Input to `PersistAsync`. Read by `LoadExistingTestsAsync` (existing private helper). **Unchanged**. |
| `MetadataIndex` | `src/Spectra.Core/Models/MetadataIndex.cs` | Output of `IndexGenerator.Generate`. Persisted as `_index.json`. **Unchanged**. |
| `TestIndexEntry` | `src/Spectra.Core/Models/TestIndexEntry.cs` | Member type of `MetadataIndex.Tests`. Field set and lowercase-priority semantics already correct. **Unchanged**. |
| `TestFileWriter` | `src/Spectra.CLI/IO/TestFileWriter.cs` | Composed by `TestPersistenceService`. **Unchanged**. |
| `IndexGenerator` | `src/Spectra.Core/Index/IndexGenerator.cs` | Composed by `TestPersistenceService`. **Unchanged**. |
| `IndexWriter` | `src/Spectra.Core/Index/IndexWriter.cs` | Composed by `TestPersistenceService`. **Unchanged**. |

## New Type

### `TestPersistenceService`

**Location**: `src/Spectra.CLI/IO/TestPersistenceService.cs`
**Namespace**: `Spectra.CLI.IO`
**Visibility**: `public sealed class`

**Responsibilities**:
- Compose `TestFileWriter` + `IndexGenerator` + `IndexWriter` into a single "write tests, regenerate suite index" operation.
- Own the write-then-index invariant: any successful return guarantees both the test files exist on disk and the suite's `_index.json` reflects them.
- Surface failures (do not swallow): exceptions from either dependency propagate to the caller.

**Dependencies (constructor-injected)**:
| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `_writer` | `TestFileWriter` | Existing | One instance shared per service instance. |
| `_indexGenerator` | `IndexGenerator` | Existing | Stateless. |
| `_indexWriter` | `IndexWriter` | Existing | Stateless. |

**Public surface (single method)**:

```csharp
public Task PersistAsync(
    string testsPath,
    string suite,
    IReadOnlyList<TestCase> testsToWrite,
    IReadOnlyList<TestCase> allTestsForIndex,
    CancellationToken ct = default);
```

**Parameter contract**:
| Parameter | Meaning | Validation |
|-----------|---------|------------|
| `testsPath` | Absolute path to the workspace's `test-cases/` directory. | `ArgumentException.ThrowIfNullOrWhiteSpace`. |
| `suite` | Suite name (last path segment of suite directory). | `ArgumentException.ThrowIfNullOrWhiteSpace`. |
| `testsToWrite` | The tests to materialize as `.md` files. May be a single test (from-description) or many (batch). | `ArgumentNullException.ThrowIfNull`. May be empty (no-op for files, but index is still regenerated from `allTestsForIndex`). |
| `allTestsForIndex` | Full set of tests the suite should contain after this operation = existing-on-disk + new, deduplicated by id. | `ArgumentNullException.ThrowIfNull`. Caller responsibility to dedupe. |
| `ct` | Standard cancellation token. | Forwarded to all I/O. |

**Behavioral contract (mirrors spec FRs)**:

| FR | How `TestPersistenceService` satisfies it |
|----|-------------------------------------------|
| FR-001 | Both flows now call this service; index is always updated. |
| FR-002 | Delegates to `IndexGenerator.CreateEntry`, which already lowercases priority. |
| FR-003 | Caller passes the full set in `allTestsForIndex`; service does not partially update. |
| FR-004 | Caller-side dedup invariant (see Decision 3 in research.md). Service writes whatever set it receives. |
| FR-005 | This service is the only call site for "write a test file as part of generation" after refactor. |
| FR-009 | The index produced is identical to today's batch behavior for the same inputs (`IndexGenerator` unchanged). |
| FR-011 | No `try`/`catch` around dependencies; exceptions propagate. |

**Non-responsibilities** (deliberately):
- Does not load existing tests from disk (caller does).
- Does not parse test files (caller has them as `TestCase` already).
- Does not validate cross-test invariants like id uniqueness (handled upstream by `TestIdAllocator` / `PersistentTestIdAllocator`).
- Does not enforce concurrency locks (Decision 8 in research.md).

## State Transitions

`TestPersistenceService` is stateless. The only state transition is on disk:

```text
Before PersistAsync:
  test-cases/{suite}/_index.json  → contains N entries (or absent)
  test-cases/{suite}/{old-ids}.md → N files

After PersistAsync (success):
  test-cases/{suite}/_index.json  → contains |allTestsForIndex| entries (deduped by id)
  test-cases/{suite}/{old-ids}.md → unchanged
  test-cases/{suite}/{new-ids}.md → present, one per testsToWrite

After PersistAsync (mid-operation failure):
  test-cases/{suite}/_index.json  → either pre-existing state (if file-write phase failed) or new state (if index-write phase failed)
  test-cases/{suite}/{written-so-far}.md → present
  Recovery: `spectra index --rebuild`
```

## Validation Rules (carried over, not added)

- Test file shape: `TestFileWriter.FormatTestCase` (unchanged) — YAML frontmatter + markdown body.
- Index entry shape: `IndexGenerator.CreateEntry` (unchanged) — lowercase priority, id-sorted entries, lowercase JSON property names via `SnakeCaseLower` policy.
- Schema validation (`spectra validate`) is run separately by the user/CI per constitution; this spec does not invoke it inline.

## Glossary alignment with spec.md

| Spec entity | Code embodiment |
|-------------|-----------------|
| Suite Index | `MetadataIndex` serialized as `_index.json` (existing). |
| Test File | `.md` written by `TestFileWriter` (existing). |
| Test Persistence Operation | `TestPersistenceService.PersistAsync` (new — this spec). |
