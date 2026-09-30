# Contract: user-visible behaviour changes in Spec 080

## Configuration (A5)

| Situation | Read commands¹ | Write commands² |
|---|---|---|
| `spectra.config.json` missing | Run with built-in defaults | Exit 1: `No spectra.config.json found. Run 'spectra init' first.` |
| Malformed JSON, missing required property, or missing `source` / `tests` / `ai` section | Exit 1: `spectra.config.json is invalid: {first error}` | Exit 1: same message |

¹ Read commands: `ai compile-extraction-prompt`, `ai compile-critic-prompt`, `ai compile-repair-prompt`,
`ai compile-repair-batch`, `docs changed`.
² Write commands: `ai ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`,
`ai analyze --import-criteria`.

Where each command reports the config error in JSON mode:

| Command(s) | Stream | Shape |
|---|---|---|
| `docs retire`, `docs apply-move`, `docs migrate-criteria`, `ai ingest-criteria` | stderr | `CriteriaCommandIO.WriteFailure`: `{ "command", "success": false, "error_code": null, "errors": ["<message>"] }` |
| `ai analyze --import-criteria` | stdout (like the rest of `ai analyze`) | `ErrorResult`: `{ "command": "import-criteria", "status": "failed", "timestamp": "<ISO-8601>", "error": "<message>" }`, plus the base class's optional `message` when it is set |
| `ai compile-extraction-prompt` | stderr | the B7 failure JSON: `{ "error_code": null, "message": "<message>", "warnings": [...] }` |
| `compile-critic-prompt`, `compile-repair-prompt`, `compile-repair-batch`, `docs changed` | stderr | plain text line (these report every error as text today; no JSON error format is added) |

`error_code` is `null` for config errors, as it already is in the three 079 docs commands, and the message tells a
missing config from an invalid one. **Change:** `ai ingest-criteria` used to print its early errors (config, and
`--from` file not found) as plain text even in JSON mode. They now go through `WriteFailure`, so every JSON-mode failure
of the criteria commands is one JSON document (SC-006). These early failures carry no `outcome`, because no response
was classified. `Rejected` stays reserved for classified-but-refused ingests. **Change:** `docs changed` with no config used to exit 1; it now runs on the defaults. No command continues
on defaults after failing to parse a config.

## Source refs outside the repository (A2)

A ref resolves to a file under the repository **or** under `source.local_dir`, even when the latter is outside the
repository (e.g. `../product-docs/docs`). A ref that resolves under neither is refused. The critic and repair compilers
report the reason in the message:

```text
Source ref not found: docs/gone.md
Source ref is outside the repository and the configured source directory: ../../elsewhere/x.md
```

## ingest-criteria failure outcome (B6)

```json
{ "success": false, "outcome": "Rejected", "error_code": "INVALID_CRITERION_ID", "errors": ["…"] }
```

`Rejected` is used for every classified-but-refused ingest: `INVALID_CRITERION_ID`, `CRITERIA_FILE_UNREADABLE`,
`CRITERIA_FILE_COLLISION`, `INVALID_DOC_PATH` and `LOCK_TIMEOUT`. `EmptyResponse` and `ParseFailure` are unchanged.
Exit codes are unchanged.

## compile-extraction-prompt failure output (B7)

In JSON mode, stderr on failure is exactly one JSON document, always with a `warnings` array:

| Failure | Exit | JSON on stderr |
|---|---|---|
| Invalid config | 1 | `{ "error_code": null, "message": "spectra.config.json is invalid: …", "warnings": [] }` |
| `--doc` missing | 4 | `{ "refused": true, "missing_input": "document_path", "message": "…", "warnings": [] }` |
| Document not found | 1 | `{ "error_code": "DOCUMENT_NOT_FOUND", "message": "Document not found: docs/x.md", "warnings": [] }` |
| Document unreadable | 1 | `{ "error_code": "DOCUMENT_UNREADABLE", "message": "Error reading document: …", "warnings": [] }` |
| Compiler refusal (defensive; not reachable from the CLI today) | 4 | `{ "refused": true, "missing_input": "…", "message": "…", "warnings": [ … ] }` |

Order of work: load config, then check the document, then collect the warnings, then compile. So every failure that is
reachable today happens before any warning exists, and carries `"warnings": []`. The field is still always present, so
that a failure added after the warnings later carries them.

Unchanged:
- An empty document is a **success** short-circuit: exit 0, with `{ "short_circuit": true, "outcome": "Extracted",
  "criteria": [] }` on stdout.
- On success, warnings are printed to stderr as text lines, and stdout is the prompt.

In human mode, a failure prints the warnings first, then the error line.

## Shared (pre-079) criteria files (B1–B3)

Each operation considers only the criteria whose `source_doc` is the document in question:

- `compile-extraction-prompt` offers only that document's criteria as `existing_criteria`.
- `ingest-criteria` treats only those as the previous criteria (unchanged from 079).
- `docs apply-move` copies only those to the new file.
- `docs retire` removes only those.

A document leaving the shared file (by re-ingest, apply-move or retire) takes its criteria out of it. The file is
deleted when it becomes empty and no index entry references it.

## Tags (B4)

Tags that differ only in order or case are not a change: the changeset is empty and nothing is rewritten.
