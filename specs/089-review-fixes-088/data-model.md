# Data Model: Review Fixes for Spec 088

No persisted format changes. The baseline, test files, triage detail file and prompt structure are all unchanged. These
are the in-memory and wire shapes that change.

## Staging folder (F1)

| Mode | Folder | File pattern | `<id>` |
|---|---|---|---|
| `Rebuild` | `.spectra/updates/<suite>/` | `updated-<id>.json` | after `updated-` |
| `KeepBaseline` | `.spectra/repairs/<suite>/` | `repaired-<id>.json` | after `repaired-` |

Rules:
- A batch enumerates only its own folder, in ordinal order.
- Files not matching the pattern are ignored.
- The content is a JSON array with exactly one test object.

## StagedOutcome (F1–F3, F5)

`internal sealed record StagedOutcome(TestId, Path, File, Outcome, Test?)` with `ErrorCode`, `Errors`, and the new
`Bucket` and `Why`.

| Outcome | Written | Baseline | Staging file | Counts toward |
|---|---|---|---|---|
| `written` | test + index | `Rebuild`: rebuilt; `KeepBaseline`: kept (085) | deleted | `written` |
| `unchanged` | nothing | `Rebuild`: rebuilt; `KeepBaseline`: nothing | deleted | `unchanged` |
| `unchanged_pending` (new) | nothing | nothing | deleted | `unchanged_pending` |
| `failed` | nothing | — | kept | `errors`, or `skipped_no_original` for `NO_ORIGINAL`; triggers exit 8 |

- `unchanged_pending` occurs only in `Rebuild` mode, for a test with a baseline entry whose bucket is in
  `PendingChangeCheck.NeedsEdit`.
- `failed` codes: `EMPTY_CONTENT`, `MALFORMED_JSON`, `TRUNCATED`, `NO_TESTS`, `SCHEMA_INVALID`, `DRIFT_DETECTED`,
  `MULTIPLE_TESTS` (new), `NO_ORIGINAL`, `UNREADABLE`, `ID_MISMATCH`, `PERSIST_FAILED`.

## PendingChange (F3)

`public sealed record PendingChange(Bucket Bucket, IReadOnlyList<string> Why)`, from
`PendingChangeCheck.ClassifyAsync(suite, original, baseline, context, threshold, ct)`.

- The method returns `null` when the test has no baseline entry.
- `NeedsEdit = { Load, CriteriaChanged, Removed, CriteriaRemoved, Relink }`.
- `Unchanged`, `Cosmetic` and `Light` are reviewed: recorded as `unchanged`.
- `Why` is triage's `TestVerdict.Why`, for example `numbers: 30gb→45gb` or `criterion changed: AC-1`.

## IngestResult (F2)

There is no new member. `UpdatedTestIngestor.MultipleTests = "MULTIPLE_TESTS"` is a failure code like
`DRIFT_DETECTED`. Its message is `The edit holds N test objects (ID1, ID2, …); a staged file holds exactly one test.`
An object without an id is listed as `(no id)`.

## LinkedCriterionState (F4)

`Unchanged | ChangedSinceBaseline | NotBaselined (new) | Removed`

The per-test value for a linked criterion that exists:
- `ChangedSinceBaseline`: the test's entry has the id with a different fingerprint;
- `NotBaselined`: the test has no entry, or the entry lacks the id;
- `Unchanged`: the fingerprints are equal.

Across a group, the precedence is `Removed` > `ChangedSinceBaseline` > `NotBaselined` > `Unchanged`.

| State | Rendered line |
|---|---|
| `Unchanged` | `- AC-1: <text>` |
| `ChangedSinceBaseline` | `- AC-1 (changed since baseline): <text>` |
| `NotBaselined` | `- AC-1 (not baselined — compare the test with the current text): <text>` |
| `Removed` | `- AC-1 (removed)` |

## SpectraGitIgnore (F6)

- `Patterns`: init's previous list, plus `.spectra/updates/` and `.spectra/repairs/`.
- `EnsureAsync(root, ct)` returns the added patterns. A missing pattern is appended under `# SPECTRA`, and the file is
  created when absent. Existing content is kept.
- Callers are `InitHandler` and `UpdateSkillsHandler`.
