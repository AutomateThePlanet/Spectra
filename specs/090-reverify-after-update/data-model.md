# Data Model: Re-verify After Update (Phase 4)

There are no test-file, baseline or index format changes. New scratch files and a manifest field are listed below.

## Verdict fingerprint record (FR-A2)

The file is `.spectra/verdicts/critic-input-<id>.json`. `compile-critic-prompt` writes it for each test it compiles; it
is gitignored with `.spectra/verdicts/`.

```json
{"id":"TC-101","suite":"storage","file":"test-cases/storage/TC-101.md","fingerprint":"sha256:…"}
```

- `fingerprint` = `TestContentFingerprint.Of(test)`: SHA-256 over `UpdatePromptCompiler.SerializeTest(test)`, normalised
  to LF. The grounding block is excluded.
- `suite` is null in legacy path mode.
- An ingest-time check gives one of three results:

| Result | When | Outcome |
|---|---|---|
| `Fresh` | the record's fingerprint equals the current test's | ingested as before |
| `Stale` | they differ | refused (`VERDICT_STALE` / `skipped_stale`) |
| `NoRecord` | no record, or stdin | accepted as before (pre-090) |

- The record is deleted together with the verdict file by a written edit (FR-A1) and by delete (FR-C2).

## Grounding on a written edit (FR-A1)

| Original grounding | After a written edit | Verdict file and record |
|---|---|---|
| none | none | deleted if present |
| critic verdict (grounded / partial / flagged) | removed | deleted |
| `manual` | kept byte-for-byte; listed in `manual_verdict_kept` | kept |

`unchanged` and `unchanged_pending` never write, so they touch neither.

## Staging file states (FR-B1)

| Name | Read by `--all` | Created | Removed |
|---|---|---|---|
| `updated-<id>.json` (updates) / `repaired-<id>.json` (repairs) | yes | by the skills (Write tool) | on any outcome (renamed when failed) |
| `failed-<id>.json` | never | by `--all`, renaming a failed file (replacing an older one) | by a later written / unchanged / unchanged_pending outcome for the id, or by delete |

## Deletion plan (FR-C1, FR-C2)

`TestDeletionService.PlanAsync(workspace, ids)` returns:

- `Tests`: id, suite, file, title, automated_by (as today).
- `ScalarDependents`: dependent id (from the parsed frontmatter), file, removed id. Each is applied as
  `test with { DependsOn = null }` through `TestFileWriter`.
- `ListDependentWarnings`: file and removed id. These are unparseable files with a block or flow `depends_on`
  containing the id, and are never written.
- `BaselineEntries`: suite and id.
- `StagingFiles`: paths in both folders (`updated-`, `repaired-`, `failed-`).
- `VerdictFiles`: `critic-verdict-<id>.json` and `critic-input-<id>.json`.

## Skills manifest (FR-D1)

`.spectra/skills-manifest.json` gains:

```json
{"version":"1.0","files":{…},"gitignore_offered":[".execution/*.db", "…", ".spectra/updates/", ".spectra/repairs/"]}
```

- Absent or null means the workspace predates 090, and the offered set defaults to the 089 list minus
  `.spectra/updates/` and `.spectra/repairs/`.
- A pattern is recorded when it is appended, already present (whole line), or negated (`!pattern`, the user's choice).
- With no `.gitignore`, nothing is recorded.
