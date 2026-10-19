# spectra-update — reference

Reference for the `spectra-update` skill. Read only the part SKILL.md names, when it names it.

## Exit codes (triage, relink, ack, baseline)

- Exit `0` → success; read the JSON on stdout. Triage and `relink-sources` exit 0 whatever they
  find (no git, or no rename, is a `reason`, not an error).
- Exit `4` → refused; nothing was written. Show `errors[0]` and the `error_code`
  (`SUITE_NOT_FOUND`, `TEST_NOT_FOUND`, `REF_REMOVED`, `CRITERION_REMOVED`, `TRIAGE_MISSING`,
  `GROUP_NOT_FOUND`, `TRIAGE_STALE`, `INVALID_ARGUMENTS`) and stop for that suite.
- Exit `1` → an error; nothing was written. On `LOCK_TIMEOUT` (another SPECTRA command is
  writing baselines), retry the same command once. Otherwise — or if the retry also fails —
  report the `error_code` (`BASELINE_UNREADABLE` names the file to fix; `CONFIG_MISSING` /
  `CONFIG_INVALID` for a config problem — fix `spectra.config.json` first) and stop for that suite.

## Buckets

When presenting results, explain the buckets (a test's bucket is its worst change):
- **RELINK** — a referenced doc file was renamed or moved (found in git history); cleared by `relink-sources` after the user's yes
- **REMOVED** — a referenced section (or its file) is gone; review only, cleared by fixing the ref
  (`not found; submodule not checked out` is not a removal: initialise the submodule and re-run triage)
- **CRITERIA_REMOVED** — a linked criterion no longer exists; review only
- **LOAD** — a number, a modal word (must/should/may/cannot/required/only/not) or many words changed, or no diff was available
- **CRITERIA_CHANGED** — a linked criterion's text changed
- **LIGHT** — a small wording change
- **COSMETIC** — markup, whitespace or case only; acknowledged automatically
- **UNCHANGED** — nothing changed since the baseline
- **UNBASELINED** — no baseline yet (never counted as changed); `source-baseline --init` adopts the current docs

## Submodules and first-time setup

**Submodule not checked out** — when triage's `notes` contain a line like `N reference(s)
from M test(s) point into submodule <path>, which isn't checked out (run git submodule update
--init)`, that submodule's docs are missing in this clone: it is a setup problem, not a doc
change. Tell the user to run git submodule update --init in the repository themselves (never
run it from this skill), then re-run triage. While that note is present, don't offer
`source-baseline --init` — a baseline taken now would leave every ref into the submodule out.

**First-time setup** — when the suite has unbaselined tests, offer the baseline only now,
after the relink step (whether the user relinked or not):
`spectra ai source-baseline --init --suite <suite> --from-date <YYYY-MM-DD> --output-format json --verbosity quiet`,
with the date the tests were written or last reviewed. It reads each doc as it was on that
date — following renames back to the old path — so doc changes made since then show at the
next triage. If there is no git, or the user doesn't know a date, plain
`spectra ai source-baseline --init --suite <suite> --output-format json --verbosity quiet`
adopts the current docs instead. Run either only after the user agrees, then re-run triage.

## Clearing REMOVED entries

List every other entry of `removed`: the test, the ref or criterion that is gone, and its
`reason` (`anchor not found`; `not found; <why>` for a missing file — e.g. `not found; no git`,
`not found; ambiguous rename`; `not in any criteria file`). Do not compile an
edit prompt for these tests, and never edit the `.md` file yourself — there is no current
section to edit against. Tell the user how to clear each one:
- the heading was renamed or the doc moved → fix the ref in the test's `source_refs`
  (or the ID in its `criteria`), then run
  `spectra ai update-ack --suite <suite> --ids <id> --output-format json --verbosity quiet`
  to record the new baseline;
- the section is gone but the test is still valid through its other refs → drop the
  ref from `source_refs`;
- the feature itself was removed → the test can be deleted with the `spectra-delete`
  skill (`spectra delete <id> --dry-run`, then confirm). Never run the delete from this skill.

When the reason is `not found; no rename found at 90% similarity`, the doc may have been
renamed with heavier edits than triage looks for. Tell the user that
`spectra ai relink-sources --suite <suite> --min-similarity <N> --dry-run --output-format json --verbosity quiet`
(N between 50 and 100; lower finds heavier-edited renames) previews it; show the result, and
relink without `--dry-run` only after the user says yes, as in Step 2a.

## Suggest next steps

After updating:
- "Run coverage analysis?" → use spectra-coverage SKILL
- "Validate the updated test cases?" → use spectra-validate SKILL
- "Generate test cases for the sections no test covers?" → use spectra-generate SKILL
- "View the updated test cases?" → use spectra-list SKILL

## Example user requests

- "Update test cases for the checkout suite"
- "My docs changed, update the test cases"
- "Check if any test cases are outdated"
- "Edit the outdated login test cases to match the new docs"
- "Update all test suites"
- "Which tests reference sections that were removed?"
- "Refresh test cases after the API docs update"
