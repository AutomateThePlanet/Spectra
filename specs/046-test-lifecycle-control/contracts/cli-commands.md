# CLI Contract: New Lifecycle Commands

**Phase**: 1 (Design & Contracts)
**Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md)

This document defines the CLI surface (commands, flags, exit codes, examples) that Spec 040 introduces. All commands honor the existing global flags: `--output-format json|human` (default `human`), `--no-interaction`, `--verbosity quiet|normal|verbose` (default `normal`).

---

## 1. `spectra delete`

Delete one or more test cases.

### Synopsis

```
spectra delete <test-id> [<test-id> …] [options]
spectra delete --suite <name> [options]                    # alias for `spectra suite delete <name>`
```

### Arguments

| Arg | Required | Description |
|---|---|---|
| `<test-id>` | yes (when not using `--suite`) | One or more test IDs (e.g., `TC-142`). Multiple positional IDs are accepted. |

### Options

| Option | Default | Description |
|---|---|---|
| `--suite <name>` | — | Delete every test in this suite. Aliases `spectra suite delete <name>`. |
| `--dry-run` | off | Preview only — no filesystem changes. Result reports planned actions. |
| `--force` | off | Skip interactive confirmation; override the automation guard. |
| `--no-automation-check` | off | Override the automation guard without forcing past the confirmation. |

### Behavior summary

1. Resolve each test ID via per-suite `_index.json`, filesystem fallback if stale.
2. Pre-flight: not-found → exit 4; automation-linked without override → exit 5; gather dependents.
3. Confirm interactively (unless `--force` or `--no-interaction`).
4. Atomically delete each test file, update affected `_index.json`, cascade `depends_on` cleanup across the workspace.
5. Emit `.spectra-result.json` (`DeleteResult`).

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Generic error |
| 3 | Missing required args with `--no-interaction` |
| 4 | `TEST_NOT_FOUND` — at least one requested ID was not found |
| 5 | `AUTOMATION_LINKED` — at least one test has `automated_by` and no override given |
| 130 | Interrupted by Ctrl+C |

### Examples

```bash
spectra delete TC-142
spectra delete TC-142 TC-143 TC-150 --dry-run
spectra delete TC-142 --force --no-interaction --output-format json
spectra delete --suite legacy   # alias path
```

---

## 2. `spectra suite`

Suite management. Parent command with three subcommands.

### 2a. `spectra suite list`

```
spectra suite list [--output-format json]
```

Lists every suite with its test count. No flags beyond the globals. Always exit 0 unless the workspace is invalid.

### 2b. `spectra suite rename <old> <new>`

```
spectra suite rename <old> <new> [--dry-run] [--force]
```

| Arg | Required | Description |
|---|---|---|
| `<old>` | yes | Existing suite name. |
| `<new>` | yes | New name. Must match `^[a-z0-9][a-z0-9_-]*$`. |

| Option | Default | Description |
|---|---|---|
| `--dry-run` | off | Preview only. |
| `--force` | off | Skip interactive confirmation. |

#### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Generic error |
| 4 | `SUITE_NOT_FOUND` — `<old>` does not exist |
| 6 | `SUITE_ALREADY_EXISTS` — `<new>` already exists |
| 7 | `INVALID_SUITE_NAME` — `<new>` violates the naming rule |

#### Examples

```bash
spectra suite rename checkout payments
spectra suite rename checkout payments --dry-run --output-format json
spectra suite rename checkout payments --force --no-interaction
```

### 2c. `spectra suite delete <name>`

```
spectra suite delete <name> [--dry-run] [--force]
```

| Option | Default | Description |
|---|---|---|
| `--dry-run` | off | Preview only. |
| `--force` | off | Skip interactive confirmation; override the automation and external-dependency guards. |

#### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Generic error |
| 4 | `SUITE_NOT_FOUND` |
| 5 | `AUTOMATION_LINKED` — at least one test in the suite has `automated_by` |
| 8 | `EXTERNAL_DEPENDENCIES` — tests in other suites depend on this one |

#### Examples

```bash
spectra suite delete legacy --dry-run
spectra suite delete legacy --force --no-interaction --output-format json
```

---

## 3. `spectra cancel`

Cancel an in-progress SPECTRA operation in the workspace.

### Synopsis

```
spectra cancel [--force] [--output-format json] [--no-interaction]
```

### Options

| Option | Default | Description |
|---|---|---|
| `--force` | off | Skip the 5 s cooperative grace; kill the running process immediately. |

### Behavior summary

1. Read `.spectra/.pid`. If absent → exit 0 with `{ status: "no_active_run" }`.
2. Validate liveness + process-name (defense against PID reuse). If stale → delete `.spectra/.pid`, exit 0 with `no_active_run`.
3. Write `.spectra/.cancel` sentinel.
4. If `--force` → `Process.Kill(entireProcessTree: true)` immediately.
5. Otherwise poll `.spectra/.pid` every 200 ms for up to 5 s.
6. If still alive after 5 s → `Process.Kill(entireProcessTree: true)`, wait 2 s.
7. Clean up `.spectra/.pid` and `.spectra/.cancel`. Emit `.spectra-result.json` (`CancelResult`).

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success — operation cancelled, OR no active run |
| 1 | Generic error (e.g., kill failed twice) |

### Examples

```bash
spectra cancel
spectra cancel --force
spectra cancel --no-interaction --output-format json --verbosity quiet
```

---

## 4. `spectra doctor ids`

Diagnose and (with `--fix`) repair test ID issues across the workspace.

### Synopsis

```
spectra doctor ids [--fix] [--output-format json] [--no-interaction]
```

### Options

| Option | Default | Description |
|---|---|---|
| `--fix` | off | Resolve duplicates: keep the oldest occurrence, renumber later occurrences at `HWM + 1`. Update `depends_on` references and best-effort hardcoded automation IDs. |

### Behavior summary

Without `--fix` (read-only):

- Walk every test file under `test-cases/**/*.md`.
- Build the all-IDs inventory; identify duplicates and index mismatches.
- Read the high-water-mark; compute the next ID.
- Emit `.spectra-result.json` (`DoctorIdsResult`).

With `--fix`:

- All of the above, plus:
- For each duplicate group, order by Git history (`git log --diff-filter=A --reverse -- <path>`) with `mtime` as tiebreaker.
- Renumber later occurrences via `PersistentTestIdAllocator.AllocateAsync` (so HWM advances monotonically).
- Update `depends_on` arrays across the workspace where they point at a renumbered ID.
- Best-effort literal-string update of `[TestCase("TC-NNN")]` patterns in linked automation files. Anything not safely updatable goes into `unfixable_references`.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success — including the case where duplicates were *reported* but no fix requested. |
| 1 | Generic error |
| 9 | `DUPLICATES_FOUND` — when `--no-interaction` and duplicates are reported without `--fix`. (CI-friendly: lets pipelines fail noisily on regressions.) |

### Examples

```bash
spectra doctor ids
spectra doctor ids --output-format json --no-interaction
spectra doctor ids --fix
```

---

## 5. Cancellation interleave with existing commands

The following existing commands gain a `Cancelled` terminal state. Their result classes acquire the new common cancellation fields (see `data-model.md` §5g).

| Command | Batch boundary | Result class extended |
|---|---|---|
| `ai generate` | per test | `GenerateResult` |
| `ai update` | per test | `UpdateResult` (or whatever the existing result class is named) |
| `ai analyze` (behavior) | per doc | `AnalyzeResult` |
| `ai analyze --extract-criteria` | per doc | `AnalyzeCriteriaResult` |
| `ai analyze --coverage` | per suite | `AnalyzeCoverageResult` |
| `dashboard` | per step | `DashboardResult` |
| `docs index` | per doc | `DocsIndexResult` |

When cancelled, each writes its existing `.spectra-result.json` with `status: "cancelled"` and the per-command count of survivors (e.g., `tests_written`, `docs_extracted`, `suites_analyzed`).

The progress page (`.spectra-progress.html`) is updated by the same handler with `phase: "Cancelled"` as a terminal phase.

Exit code on cancellation is **130** (standard SIGINT convention) for every long-running command.

---

## 6. Result-artifact location and write semantics

All commands write `.spectra-result.json` at `<workspace-root>/.spectra-result.json` (existing convention). Writes are atomic (temp + rename). The file is overwritten on every command — there is no result history. (Run history is the MCP execution-engine concern, not the CLI result artifact.)

The progress page (`.spectra-progress.html`) is written at `<workspace-root>/.spectra-progress.html` (existing convention).

---

## 7. Global behavior

All new commands honor:

- `--no-interaction` — never prompt; missing required args → exit 3.
- `--output-format json` — emit machine-readable result only; no human-formatted output to stdout.
- `--verbosity quiet` — suppress progress chatter; only the final result line.
- Ctrl+C — translates to `OperationCanceledException` via the `CancellationManager` token; commands exit 130 with the cancelled-result artifact.
