# Contract: `TestPersistenceService`

**Type**: Internal CLI orchestration service (not exposed via MCP or CLI surface).
**Scope of this contract**: How callers inside `Spectra.CLI` use the service. There is no over-the-wire format; this contract governs source-level invariants and unit-test obligations.

## Surface

```csharp
namespace Spectra.CLI.IO;

public sealed class TestPersistenceService
{
    public TestPersistenceService(
        TestFileWriter writer,
        IndexGenerator indexGenerator,
        IndexWriter indexWriter);

    public Task PersistAsync(
        string testsPath,
        string suite,
        IReadOnlyList<TestCase> testsToWrite,
        IReadOnlyList<TestCase> allTestsForIndex,
        CancellationToken ct = default);
}
```

## Preconditions

| # | Condition | Enforcement |
|---|-----------|-------------|
| P1 | `testsPath` is non-empty. | `ArgumentException.ThrowIfNullOrWhiteSpace(testsPath)` |
| P2 | `suite` is non-empty. | `ArgumentException.ThrowIfNullOrWhiteSpace(suite)` |
| P3 | `testsToWrite` is non-null (may be empty). | `ArgumentNullException.ThrowIfNull(testsToWrite)` |
| P4 | `allTestsForIndex` is non-null (may be empty if no tests exist yet). | `ArgumentNullException.ThrowIfNull(allTestsForIndex)` |
| P5 | Caller has deduped `allTestsForIndex` by `Id` (last write wins per id). | Caller responsibility. See Postcondition Q4. |
| P6 | Every `TestCase` in both lists has a non-empty `Id` matching `^[A-Za-z]+-\d+$` (Spectra id grammar). | Upstream `TestIdAllocator` invariant. |

## Postconditions (on successful return)

| # | Guarantee |
|---|-----------|
| Q1 | For each `t ∈ testsToWrite`: a file exists at `Path.Combine(testsPath, suite, t.Id + ".md")` and its content equals `TestFileWriter.FormatTestCase(t)`. |
| Q2 | The file at `Path.Combine(testsPath, suite, "_index.json")` exists and deserializes to a `MetadataIndex` whose `Suite == suite`. |
| Q3 | That `MetadataIndex.Tests` contains exactly one entry per distinct `Id` present in `allTestsForIndex`. |
| Q4 | If `allTestsForIndex` contains duplicate ids, behavior is "last entry in the list wins" — driven by `IndexGenerator.Generate` semantics (id-sorted, single pass). Callers SHOULD dedupe upstream to avoid relying on this. |
| Q5 | Each `TestIndexEntry`'s `Priority` is the lowercase string form of the corresponding `TestCase.Priority`. (Inherited from `IndexGenerator.CreateEntry`.) |
| Q6 | Entries in `MetadataIndex.Tests` are ordered by `Id` ascending. (Inherited from `IndexGenerator.Generate`.) |
| Q7 | `MetadataIndex.GeneratedAt` is set to `DateTime.UtcNow` at index-generation time. |

## Failure modes

| Origin | Exception type | Effect on disk |
|--------|----------------|----------------|
| `testsToWrite[i]` write fails (e.g., `IOException`) | Propagated as-is | Files written for indices `< i` remain; later files and index are not written. |
| `IndexWriter.WriteAsync` fails | Propagated as-is | All test files for `testsToWrite` are written; `_index.json` may be missing or in pre-call state. |
| `ct` is cancelled | `OperationCanceledException` | Partial state per the cancellation point; same recovery story. |

**Recovery for partial-state failures**: `spectra index --rebuild` reconstructs the index from `.md` files of record. This is the only guarantee the spec makes — it does not promise transactional atomicity (see research.md Decision 4).

## Caller obligations

| Caller | Obligation |
|--------|------------|
| `GenerateHandler.ExecuteDirectModeAsync` (batch) | Pass the per-batch slice as `testsToWrite` and the running cumulative set `existingTests ∪ allWrittenTests` as `allTestsForIndex`. Identical to today's inline pattern; behavior must be byte-equivalent for the same inputs. |
| `GenerateHandler.ExecuteFromDescriptionAsync` (from-description) | Load existing suite tests via the existing private `LoadExistingTestsAsync` helper. Build `allTestsForIndex = existing.Where(t => t.Id != generated.Id).Append(generated).ToList()`. Pass `[generated]` as `testsToWrite`. |
| *Any future generation flow* | Must use `TestPersistenceService.PersistAsync` exclusively; must not call `TestFileWriter.WriteAsync` directly for generation output. (Code review enforcement; static search by reviewers.) |

## Invariants enforced by this contract

| ID | Invariant |
|----|-----------|
| INV-1 | Every successful `PersistAsync` call updates `_index.json` for `suite`. (Eliminates the Spec 049 bug class.) |
| INV-2 | No public method of `TestPersistenceService` writes a test file without also writing the suite index. |
| INV-3 | `TestPersistenceService` does not modify the on-wire shape of `MetadataIndex` or `_index.json`. (Spec 049 FR-010.) |

## Negative space — what this service does NOT do

- Does not load existing suite tests from disk.
- Does not parse `.md` files.
- Does not validate id uniqueness (relies on caller / upstream allocator).
- Does not enforce locks or concurrency control.
- Does not write `_manifest.yaml`, `_checksums.json`, or `_criteria_index.yaml` (those belong to docs, not test indexes).
- Does not emit progress events (caller's responsibility via `IProgressReporter`).
- Does not return a result type (success = normal return, failure = exception).

## Test obligations (for `TestPersistenceServiceTests`)

| Test | Asserts (mapped to Postconditions) |
|------|------------------------------------|
| `PersistAsync_WritesAllTestsToWrite_AsMdFiles` | Q1 |
| `PersistAsync_WritesIndexJson_WithFullSet` | Q2, Q3, Q6 |
| `PersistAsync_LowercasesPriorityInIndex` | Q5 |
| `PersistAsync_OverwritesPreExistingIndex` | Q2 — old entries not orphaned in if `allTestsForIndex` still contains them. |
| `PersistAsync_EmptyTestsToWrite_StillRegeneratesIndex` | Q2 — with no files written, index reflects `allTestsForIndex`. |
| `PersistAsync_WhenWriterThrows_PropagatesException` | Failure mode row 1. |
| `PersistAsync_WhenIndexWriterThrows_PropagatesException` | Failure mode row 2. |
| `PersistAsync_CreatesSuiteDirectoryIfMissing` | Inherits `TestFileWriter.WriteAsync` behavior (creates parent dir). |

These map to spec FRs FR-001, FR-002, FR-003, FR-009, FR-011, and underwrite the `PersistenceService_NeverWritesFileWithoutIndex` row in the spec's test plan.
