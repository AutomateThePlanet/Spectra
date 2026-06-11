# Contract Delta: Retired MCP Tools → Surviving CLI/Engine Successor

This feature **removes** the SPECTRA MCP tool contract (25 JSON-RPC tools registered in
`src/Spectra.MCP/Program.cs:136-167`). There is no new contract; the surviving contract is the existing
`spectra run …` CLI command surface (and the `Spectra.Execution` engine API it wraps). This table records,
for each retired MCP tool, its CLI successor — the public interface a former MCP consumer must migrate to.

> **Accepted risk (FR / spec Risks):** any external/networked consumer of these MCP tools breaks. This is a
> knowing product decision; the consumer population is unknowable and not enumerated.

## Run Management

| Retired MCP tool | CLI successor |
|------------------|---------------|
| `start_execution_run` | `spectra run start <suite> [--priorities --tags --components --test-ids --selection --environment]` |
| `get_execution_status` | `spectra run status [<run-id>]` |
| `pause_execution_run` | `spectra run pause [<run-id>]` |
| `resume_execution_run` | `spectra run resume [<run-id>]` |
| `cancel_execution_run` | `spectra run cancel [<run-id>]` |
| `finalize_execution_run` | `spectra run finalize [<run-id>] [--force]` |
| `list_available_suites` | `spectra run list-suites` |

## Test Execution

| Retired MCP tool | CLI successor |
|------------------|---------------|
| `get_test_case_details` | `spectra run show [<run-id>] [--test-id|--handle]` |
| `advance_test_case` | `spectra run advance [<handle>] --status pass|fail|blocked|skip [--notes]` |
| `skip_test_case` | `spectra run skip [<handle>] --reason "…" [--blocked]` |
| `bulk_record_results` | `spectra run bulk-record [<run-id>] --status <s> [--remaining|--test-ids a,b] [--reason]` |
| `add_test_note` | `spectra run note [<handle>] --note "…"` |
| `retest_test_case` | `spectra run retest [<run-id>] --test-id <id>` |
| `save_screenshot` | `spectra run screenshot [<handle>] --file <path>` |
| `save_clipboard_screenshot` | `spectra run screenshot-clipboard [<handle>]` |

## Discovery

| Retired MCP tool | CLI successor |
|------------------|---------------|
| `list_active_runs` | `spectra run list-active` |
| `cancel_all_active_runs` | `spectra run cancel-all` |

## Data

| Retired MCP tool | CLI successor |
|------------------|---------------|
| `validate_tests` | `spectra validate` |
| `rebuild_indexes` | `spectra index` / `spectra docs index` |
| `analyze_coverage_gaps` | `spectra ai analyze --coverage` |
| `find_test_cases` | `spectra run selections` / index-driven start filters (`spectra run start --test-ids/--tags/…`) |
| `get_test_execution_history` | `spectra run history [--suite]` |
| `list_saved_selections` | `spectra run selections` |

## Reporting

| Retired MCP tool | CLI successor |
|------------------|---------------|
| `get_run_history` | `spectra run history` |
| `get_execution_summary` | `spectra run summary [<run-id>]` |

## Notes

- Every successor drives the **same** `ExecutionEngine` over the **same** `.execution/spectra.db`. Behavior
  parity (not just availability) is asserted by `ParityTests`, `GuardrailTests`, `RunLoopSmokeTests`, and the
  engine tests relocated/ported per `research.md` R4.
- The MCP-specific response envelope (`run_status` / `progress` / `next_expected_action` JSON, error codes
  like `RECONSTRUCTION_FAILED` / `RUN_NOT_FOUND`) is transport surface that disappears with the adapter; the
  CLI exposes equivalent state via `--output-format json` on the `run` subcommands and its own exit codes.
