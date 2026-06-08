# Contract: `spectra run` command group ⟷ MCP tools ⟷ engine

The `spectra run` subcommands are thin adapters over `ExecutionEngine` — the **same** engine the MCP tools call. This table is the one-to-one mapping (FR-002/FR-007). "CLI home" = an existing `spectra` command already covers the operation, so `run` does **not** duplicate it (Constitution V).

## Execution lifecycle (new under `spectra run`)

| MCP tool | `spectra run` subcommand | Engine call | Notes |
|---|---|---|---|
| `start_execution_run` | `run start <suite> [--priorities --tags --components --selection --test-ids --environment --name]` | `StartRunAsync` | Builds rich queue, persists snapshot + results. Prints run id + first test. |
| `get_execution_status` | `run status [<run-id>]` | `GetStatusAsync` / `GetStatusCountsAsync` | No run-id → active run for current user. Counts + current/next test. |
| `pause_execution_run` | `run pause <run-id>` | `PauseRunAsync` | |
| `resume_execution_run` | `run resume <run-id>` | `ResumeRunAsync` | |
| `cancel_execution_run` | `run cancel <run-id>` | `CancelRunAsync` | |
| `finalize_execution_run` | `run finalize <run-id> [--force]` | `FinalizeRunAsync` | Pending guard honored cross-process (Spec 064). Generates reports. |
| `list_available_suites` | `run list-suites` | suite loader | |
| `list_active_runs` | `run list-active` | `RunRepository` | |
| `cancel_all_active_runs` | `run cancel-all` | `CancelRunAsync` ×N | |

## Per-test execution (new under `spectra run`)

| MCP tool | `spectra run` subcommand | Engine call | Notes |
|---|---|---|---|
| `get_test_case_details` | `run show <run-id> [--test-id\|--handle]` | `GetQueueAsync` + test-case loader | Auto-resolves in-progress/next-pending handle if none given (handle-recovery parity). |
| `advance_test_case` | `run advance <handle> --status <pass\|fail\|blocked\|skip> [--notes]` | `StartTestAsync`→`AdvanceTestAsync` | Records verdict, propagates blocks, returns next. |
| `skip_test_case` | `run skip <handle> --reason <text> [--blocked]` | `SkipTestAsync` | |
| `bulk_record_results` | `run bulk-record --status <s> [--remaining \| --test-ids a,b] [--reason]` | `BulkRecordResultsAsync` | |
| `add_test_note` | `run note <handle> --note <text>` | `AddNoteAsync` | |
| `retest_test_case` | `run retest <run-id> --test-id <id>` | `RetestAsync` | Cross-process safe (Spec 064). |
| `save_screenshot` | `run screenshot <handle> --file <path>` | `ScreenshotService` + `ResultRepository` | Shared service (research R5). |
| `save_clipboard_screenshot` | `run screenshot-clipboard <handle>` | `ScreenshotService` (OS clipboard) | Local CLI is the host. |

## Execution reporting (new under `spectra run`)

| MCP tool | `spectra run` subcommand | Source |
|---|---|---|
| `get_run_history` | `run history [--suite]` | `RunRepository` + `ResultRepository` |
| `get_execution_summary` | `run summary <run-id>` | `RunRepository` + `ResultRepository` |
| `get_test_execution_history` | `run test-history <test-id>` | `ResultRepository` |
| `list_saved_selections` | `run selections` | selections loader |

## Already a CLI home (NOT duplicated under `run`)

| MCP tool | Existing CLI command |
|---|---|
| `validate_tests` | `spectra validate` |
| `rebuild_indexes` | `spectra index` |
| `find_test_cases` | `spectra list` / `spectra show` |
| `analyze_coverage_gaps` | `spectra ai analyze --coverage` |

## Contract guarantees (assertable)

1. **Parity (FR-007/SC-002)**: for any operation, invoking the `run` subcommand and the matching MCP tool against the same run leaves equivalent `runs`/`test_results`/`queue_snapshot` state, because both call the same `ExecutionEngine` over the same `ExecutionDb`.
2. **Exit codes (Constitution IV)**: `0` success; non-zero on error. A `QueueReconstructionException` surfaces as a distinct non-zero code with a clear message (FR-008), never conflated with a benign "run not found" (which is its own outcome/code).
3. **Output formats**: every subcommand honors the global `--output-format json|human`, `--verbosity`, `--no-interaction` options where applicable (interactive loop subcommands excepted, like the generation seam).
4. **Guardrails (FR-005/SC-006)**: the CLI surface only records a verdict that is explicitly supplied as an argument; it never infers one. Advancing requires an explicit `--status`. The SKILL/agent enforce present→wait→advance on top of this.
