# Result JSON Contract: Test Lifecycle & Process Control

**Phase**: 1 (Design & Contracts)
**Spec**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md) | **Data Model**: [../data-model.md](../data-model.md)

This document is the source of truth for the JSON shapes that SKILLs, CI pipelines, and other consumers parse out of `.spectra-result.json` for the four new commands and the cancellation extensions to existing commands.

All shapes inherit the existing `CommandResult` base:

```jsonc
{
  "command": "<command-name>",
  "status": "completed" | "failed" | "cancelled" | "no_active_run" | "<command-specific>",
  "timestamp": "<ISO 8601 UTC>",
  "message": "<optional human-readable summary>"
}
```

Field names use `snake_case` (the existing convention is `camelCase` per `JsonResultWriter`'s naming policy; `[JsonPropertyName]` overrides set the `snake_case` keys explicitly where required).

---

## 1. Status enumeration

| Value | Used by | Meaning |
|---|---|---|
| `completed` | every command | Operation finished successfully. |
| `failed` | every command | Operation hit a fatal error. Result includes `error` field. |
| `cancelled` | long-running commands + `cancel` | Operation halted by user request. Partial results may be present. |
| `no_active_run` | `cancel` only | `cancel` invoked when nothing was running — not an error. |
| `analyzing` / `analyzed` / `analysis_failed` / `generating` / `verifying` | existing commands | Existing intermediate values; preserved unchanged. |

---

## 2. `delete` result (`DeleteResult`)

```jsonc
{
  "command": "delete",
  "status": "completed",
  "timestamp": "2026-04-30T14:30:00Z",
  "dry_run": false,
  "deleted": [
    {
      "id": "TC-142",
      "suite": "checkout",
      "file": "test-cases/checkout/TC-142.md",
      "title": "Checkout with expired card",
      "automated_by": ["tests/e2e/CheckoutTests.cs"],
      "stranded_automation": ["tests/e2e/CheckoutTests.cs"]
    }
  ],
  "dependency_cleanup": [
    { "test_id": "TC-150", "removed_dep": "TC-142" }
  ],
  "skipped": [
    { "id": "TC-999", "reason": "TEST_NOT_FOUND" }
  ],
  "errors": []
}
```

| Field | Type | Notes |
|---|---|---|
| `dry_run` | bool | `true` when invoked with `--dry-run`. |
| `deleted[].automated_by` | string[] | `automated_by` paths from the test's frontmatter at the time of deletion. |
| `deleted[].stranded_automation` | string[] | Subset of `automated_by` confirmed to exist on disk. Reported but never modified. |
| `dependency_cleanup[]` | object[] | Every `(test_id, removed_dep)` rewrite that was applied to other tests' `depends_on`. |
| `skipped[].reason` | string | Stable error code: `TEST_NOT_FOUND`, `AUTOMATION_LINKED`. |
| `errors[]` | object[] | `{ id, message }` for any per-test failure that did not stop the whole operation. |

Failure shapes (status=`failed`): `errors[]` is populated; `deleted[]` may be partial.

---

## 3. `suite list` result (`SuiteListResult`)

```jsonc
{
  "command": "suite list",
  "status": "completed",
  "timestamp": "2026-04-30T14:30:00Z",
  "suites": [
    {
      "name": "checkout",
      "test_count": 42,
      "directory": "test-cases/checkout",
      "automated_count": 12
    }
  ]
}
```

| Field | Type | Notes |
|---|---|---|
| `suites[].test_count` | int | Count of `*.md` files under the suite directory (excludes `_index.json`). |
| `suites[].automated_count` | int | Count of tests whose `automated_by` is non-empty. Convenient for scripts. |

---

## 4. `suite rename` result (`SuiteRenameResult`)

```jsonc
{
  "command": "suite rename",
  "status": "completed",
  "timestamp": "2026-04-30T14:30:00Z",
  "dry_run": false,
  "old_name": "checkout",
  "new_name": "payments",
  "directory_renamed": true,
  "index_updated": true,
  "selections_updated": ["smoke", "regression"],
  "config_block_renamed": true
}
```

| Field | Type | Notes |
|---|---|---|
| `selections_updated[]` | string[] | Names of saved selections whose `suites` array was rewritten. Empty when no selection referenced the old name. |
| `config_block_renamed` | bool | `true` if `suites.<old>` existed in config and was re-keyed to `suites.<new>`. |

Failure shapes:

- `INVALID_SUITE_NAME` (exit 7) → `status: "failed"`, `error: "INVALID_SUITE_NAME"`, `message` describes the rule.
- `SUITE_ALREADY_EXISTS` (exit 6) → `status: "failed"`, `error: "SUITE_ALREADY_EXISTS"`.
- `SUITE_NOT_FOUND` (exit 4) → `status: "failed"`, `error: "SUITE_NOT_FOUND"`.

---

## 5. `suite delete` result (`SuiteDeleteResult`)

```jsonc
{
  "command": "suite delete",
  "status": "completed",
  "timestamp": "2026-04-30T14:30:00Z",
  "dry_run": false,
  "suite": "checkout",
  "tests_removed": 42,
  "stranded_automation_count": 12,
  "stranded_automation_files": ["tests/e2e/CheckoutTests.cs"],
  "external_dependency_cleanup": [
    {
      "test_id": "TC-300",
      "suite": "orders",
      "removed_deps": ["TC-142", "TC-143"]
    }
  ],
  "selections_updated": ["smoke"],
  "config_block_removed": true
}
```

Failure shapes:

- `AUTOMATION_LINKED` (exit 5, no `--force`) → `status: "failed"`, includes `stranded_automation_count` and `stranded_automation_files` for the user to inspect; no filesystem changes.
- `EXTERNAL_DEPENDENCIES` (exit 8, no `--force`) → `status: "failed"`, includes `external_dependency_cleanup` (planned, not applied).

---

## 6. `cancel` result (`CancelResult`)

```jsonc
{
  "command": "cancel",
  "status": "completed",
  "timestamp": "2026-04-30T14:30:00Z",
  "target_pid": 12345,
  "target_command": "ai generate",
  "shutdown_path": "cooperative",
  "elapsed_seconds": 1.4,
  "force": false
}
```

| Field | Type | Notes |
|---|---|---|
| `target_pid` | int? | The PID that was signaled. `null` when `status: "no_active_run"`. |
| `target_command` | string? | The command that was running. `null` when no active run. |
| `shutdown_path` | string | `"cooperative"` (process exited within 5 s of sentinel write) / `"forced"` (kill required) / `"none"` (no active run). |
| `elapsed_seconds` | number | Wall-clock from `cancel` start to process exit. |
| `force` | bool | `true` if invoked with `--force`. |

`no_active_run` shape:

```jsonc
{
  "command": "cancel",
  "status": "no_active_run",
  "timestamp": "...",
  "target_pid": null,
  "target_command": null,
  "shutdown_path": "none",
  "elapsed_seconds": 0.0,
  "force": false,
  "message": "No SPECTRA operation is currently running in this workspace."
}
```

---

## 7. `doctor ids` result (`DoctorIdsResult`)

Read-only (no `--fix`):

```jsonc
{
  "command": "doctor ids",
  "status": "completed",
  "timestamp": "2026-04-30T14:30:00Z",
  "fix_applied": false,
  "total_tests": 247,
  "unique_ids": 245,
  "duplicates": [
    {
      "id": "TC-142",
      "occurrences": [
        {
          "file": "test-cases/checkout/TC-142.md",
          "title": "...",
          "mtime": "2026-03-15T10:15:00Z"
        },
        {
          "file": "test-cases/auth/TC-142.md",
          "title": "...",
          "mtime": "2026-04-20T09:42:00Z"
        }
      ]
    }
  ],
  "index_mismatches": [
    { "suite": "checkout", "id": "TC-200", "in_index": false, "on_disk": true }
  ],
  "high_water_mark": 247,
  "next_id": "TC-248",
  "renumbered": [],
  "unfixable_references": []
}
```

With `--fix`:

```jsonc
{
  "command": "doctor ids",
  "status": "completed",
  "fix_applied": true,
  "total_tests": 247,
  "unique_ids": 247,
  "duplicates": [],
  "index_mismatches": [],
  "high_water_mark": 248,
  "next_id": "TC-249",
  "renumbered": [
    { "from": "TC-142", "to": "TC-248", "file": "test-cases/auth/TC-142.md", "now_at": "test-cases/auth/TC-248.md" }
  ],
  "unfixable_references": [
    {
      "file": "tests/e2e/CheckoutTests.cs",
      "reference": "[TestCase(constants.TC_142)]",
      "reason": "non-literal reference"
    }
  ]
}
```

---

## 8. Cancelled long-running commands

When a long-running command is cancelled, its existing result class is used with these additional fields (added uniformly):

```jsonc
{
  "command": "ai generate",
  "status": "cancelled",
  "timestamp": "2026-04-30T14:30:00Z",
  "cancelled_at": "2026-04-30T14:30:00Z",
  "phase": "generation",
  "phase_progress": { "current": 7, "total": 15 },
  "tests_written": 7,
  "files": [
    "test-cases/checkout/TC-201.md",
    "test-cases/checkout/TC-202.md"
  ],
  "message": "Generation cancelled by user. 7 of 15 tests written."
}
```

| Field | Type | Notes |
|---|---|---|
| `cancelled_at` | string (ISO 8601 UTC) | When cancellation was detected by the running command (not when the cancel command was issued). |
| `phase` | string | The phase the command was in (e.g., `analysis`, `generation`, `verification`, `extraction`, `coverage`, `index`). |
| `phase_progress` | object | `{ current, total }` within the phase. |
| `tests_written` / `docs_processed` / `suites_analyzed` | int | Per-command count of artifacts that survived. Naming is per-command (existing field names preserved). |
| `files` | string[] | Paths of artifacts that survived. May be a sample for very large runs. |
| `message` | string | Human-readable summary, suitable for direct display. |

---

## 9. Stability guarantee

These shapes are part of the public CLI contract. Future changes:

- **Additive**: new fields may be added at any time. Consumers must ignore unknown fields.
- **Breaking** (renames, type changes, semantic redefinitions): require a major version bump and migration documentation.
- **Status enum**: new values may be added; existing values (especially `completed`, `failed`, `cancelled`, `no_active_run`) are stable.
