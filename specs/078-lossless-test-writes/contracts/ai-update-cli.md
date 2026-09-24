# Contract: `spectra ai update` apply semantics (Spec 078)

## Flag matrix

The flags `--dry-run`, `--no-review` and `--no-interaction` are global. `--diff` and `--delete-orphaned` are local to this
command.

| `--dry-run` | `--no-interaction` | `--no-review` | Behaviour | Writes test files / `_index.json` |
|---|---|---|---|---|
| yes | any | any | Classify, print tables (and diff when `--diff` is set), emit result | **No** |
| no | yes | no | **Classify-only** (NEW, breaking): classify, print tables, emit result, print hint | **No** |
| no | yes | yes | Auto-review and apply: mark orphans, delete with `--delete-orphaned`, regenerate index | Yes |
| no | no | yes | Auto-review and apply (unchanged) | Yes |
| no | no | no | Interactive review, then apply (unchanged) | Yes, if approved |

Auto-detected non-interactive terminals (redirected I/O, no flag) behave as the matching "no flag" row, which is
unchanged.

## Outputs in every row

- **Human tables**: classification summary, OUTDATED / ORPHANED / REDUNDANT lists, and the diff tables when `--diff` is
  set. They are printed in every output mode and are never suppressed.
- **Result**: `ProgressManager.Complete(UpdateResult)` writes `.spectra-result.json` and the terminal
  `.spectra-progress.html`. A dry run no longer returns before this step.
- **`UpdateResult`**: adds `applied: bool`. It is `false` in the first two rows and `true` when the apply step ran. In
  classify-only runs, `testsUpdated` and `testsRemoved` are `0`.
- **Classify-only hint** (row 2 only, human info line): `Classification only — no changes written. Pass --no-review to apply.`

## Exit codes (unchanged set)

| Outcome | Code |
|---|---|
| Success (classify-only, dry run, or applied) | 0 |
| Error: config or suite missing, read failure, `--no-interaction` without `--suite` | 1 |
| Cancelled / review aborted | 130 |

For the same classification outcome, a classify-only run and an applying run return the same code (FR-010).

## Files that are not "writes"

`.spectra-result.json`, `.spectra-progress.html`, and debug/error logs (when enabled) are run-status artifacts. `spectra
init` adds the two root files to its gitignore section.

## Skill call sites (all pass `--dry-run`)

```text
spectra-update.md Step 1:
  spectra ai update --suite <suite> --diff --dry-run --no-interaction --output-format json --verbosity quiet
spectra-generate.md / spectra-execute.md delegation rows:
  spectra ai update --suite {suite} --dry-run --no-interaction --output-format json --verbosity quiet
```

## Breaking change (CHANGELOG)

CI that relies on `spectra ai update <suite> --no-interaction` to apply changes must add `--no-review`.
