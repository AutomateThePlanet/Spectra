# Contracts: Workspace Safety and Hygiene

## `spectra update-skills` (changed)

- **Edited files are never overwritten.** A file with no manifest record is recorded when it equals the bundled
  content. Otherwise it is skipped, with:
  - templates: `skipped (no record — it may have been edited); run spectra prompts reset <id> or delete it to get the new
    version`;
  - skills, agents, `profiles/_default.yaml`, `CUSTOMIZATION.md`: `skipped (no record — it may have been edited); delete
    it to get the new version`.
- **Manifest keys:** normalised to repo-relative `/` paths. Keys from another location (a moved workspace) are re-rooted
  by their install-path tail. Foreign keys that match no install path are dropped, with one summary warning,
  "N manifest entries from another location were dropped (was the workspace moved?)".
- **Gitignore:** offers `.spectra/verdicts/` and `.spectra/dropped-tests.json` once. Offered patterns are recorded as a
  `# spectra-offered:` line in `.gitignore`, and a marked pattern is never re-added. The old per-clone record is
  migrated once. A pattern the `.gitignore` negates counts as offered and is marked too.
- **Duplicate manifest keys** (pre-096): all their hashes count; a file matching any of them is unedited, otherwise it is
  customized. One normalised entry is saved.
- Exit code unchanged (0).

## `spectra init` (changed)

- Writes the manifest with normalised keys.
- Writes the gitignore patterns plus one `# spectra-offered:` line.
- The config template no longer has `ai.generation_batch_size`.

## `spectra suite delete <suite> [--dry-run] [--force] [--no-interaction]` (changed)

- The refusals are unchanged: `AUTOMATION_LINKED`, and `EXTERNAL_DEPENDENCIES` without `--force`.
- The suite's own baseline entries are never edited and no baseline lock is taken for them: `_source-baseline.json`
  goes with the suite folder. Other suites' baseline files are untouched.
- Cleanup (with `--force`, or with no external dependents):
  - scalar `depends_on` in other suites is cleared losslessly;
  - list or unparseable dependents are warned and not edited;
  - no other byte of another suite's files changes.
- Leftovers removed: `.spectra/updates/<suite>/`, `.spectra/repairs/<suite>/`, `.spectra/generation-plan/<suite>.json`,
  and the suite's tests' verdict and fingerprint-record files. The suite folder (incl. `_source-baseline.json`) is
  removed as before.
- `--dry-run` lists dependents, warnings and leftovers, and writes nothing.
- JSON adds `dependents_cleaned`, `warnings`, `leftovers_removed` (`leftovers` in a dry run).

## `spectra show <id>` (fixed)

Finds a test through its suite folder plus the index `file`. The legacy `suite/file` index form is still accepted.

## `spectra validate` (fixed)

Exit 1, status `failed`, `"<n> error(s) found."` when any test fails to parse, the validator reports errors, or a profile
error is counted. Warnings alone: exit 0.

## `spectra delete` (faster, same results)

Targets are resolved through suite indexes (scanning a suite without one). Dependents are found from frontmatter, and
only targets and scalar dependents are fully parsed.

## Repository

`.gitattributes`: `*.sh text eol=lf`.
