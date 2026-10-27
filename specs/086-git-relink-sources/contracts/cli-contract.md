# CLI Contract: Spec 086

Exit codes are as in 084: 0 success, 1 environment error, 4 refusal. Failures are one JSON document on stderr in the
shared shape. The human summary is printed in every mode, on stderr in JSON mode. Git is never required; when it is
unavailable, the reason is reported and the command still succeeds.

## `spectra ai relink-sources (--suite <s> | --all-suites) [--min-similarity N] [--dry-run]`

Writes, except on `--dry-run`: the relinked test files (ref values only, and orphan keys when cleared), the suite
`_index.json`, and the suite `_source-baseline.json` (key moves). It takes `.spectra/baseline.lock`.

```json
{"command":"relink-sources","success":true,"scope":{"suite":"s"},"dry_run":false,"min_similarity":90,
 "relinked":[{"suite":"s","test":"TC-1","refs":[{"from":"docs/a.md#Quotas","to":"docs/guides/a.md#Quotas"}],"orphan_cleared":false}],
 "not_relinked":[{"suite":"s","test":"TC-2","ref":"docs/gone.md#Y","reason":"no rename found at 90% similarity"}],
 "counts":{"tests_relinked":1,"refs_relinked":1,"refs_not_relinked":1},"warnings":[]}
```

The `reason` values below are the bare git or candidate reasons, used as-is in `relink-sources`'
`not_relinked[].reason` and in `docs topology`'s `unowned[].reason`. In `update-triage`, a missing file's REMOVED
reason always starts with `not found` and adds one of them after a semicolon (FR-013):

| Triage REMOVED reason (missing file) | When |
|---|---|
| `not found; no git` | Git is not installed |
| `not found; not in a git repository` | The doc isn't in any repository |
| `not found; no rename found at 90% similarity` | Git is available and history has no candidate (triage always uses 90%) |
| `not found; ambiguous rename`, `not found; rename chain ends at a missing file`, … | The other rows below, prefixed the same way |

Other REMOVED reasons (`anchor not found`, `outside allowed roots`, `empty ref`) are unchanged from 084/085.

The bare reasons are:

| Reason | Meaning |
|---|---|
| `no git` | Git is not installed |
| `not in a git repository` | The doc isn't in any repository |
| `repository not readable (dubious ownership)` | Git refuses to read the repository |
| `git failed: …` | A git call failed for another reason |
| `no rename found at <N>% similarity` | History records no rename of the path at the threshold (`(shallow history)` is appended for shallow repositories). Triage always reports it at 90% |
| `ambiguous rename` | The path has two or more distinct rename targets anywhere in its history |
| `rename chain ends at a missing file` | The renames lead to a file that no longer exists |
| `renamed outside the allowed roots` | The new location is outside the repo root and the source dir |

Refusals: `INVALID_ARGUMENTS` (scope, or `--min-similarity` outside 50–100), `SUITE_NOT_FOUND`. Errors:
`CONFIG_MISSING`, `CONFIG_INVALID`, `BASELINE_UNREADABLE`, `LOCK_TIMEOUT`.

## `spectra ai update-triage` (changed)

- **New bucket `RELINK`**, which ranks highest: RELINK > REMOVED > CRITERIA_REMOVED > LOAD > CRITERIA_CHANGED >
  LIGHT > COSMETIC > UNCHANGED. A RELINK ref has `why: ["renamed to <target>"]`, and its groups split by target.
- **RELINK covers refs without a baseline entry** (FR-012): an unbaselined ref of a test with an entry is RELINK, not
  an unbaselined ref; a test with no entry and a RELINK ref is listed once under RELINK. Its detail record in
  `.spectra/update-triage.json` is the same as any triaged test's (empty baseline refs, `current` filled for every
  ref) plus `"unbaselined": true`. It is removed from `unbaselined.full` (stdout and detail file), and so from
  `counts.unbaselined` and the human UNBASELINED line. A baselined test whose only unbaselined refs are RELINK refs is
  removed from `partial`. The refs of a test with no entry are evaluated for RELINK only.
- **`removed`** lists REMOVED refs only, each with its reason. A missing file's reason is one of the
  `relink-sources` reasons above.

## `spectra ai update-ack` (changed)

A test with a RELINK ref is refused with `REF_REMOVED`, and the message names `spectra ai relink-sources`, on both
paths: `--ids` (a listed test has a RELINK ref) and `--group` (the group's bucket is `RELINK`). The `--ids` check
covers missing refs with no baseline key too, including every ref of a test with no entry; `update-ack` never creates
an entry for a test that still has a RELINK ref.

## `spectra ai source-baseline --init` (new options)

| Option | Effect |
|---|---|
| `--from-date <ISO date>` | Missing entries are filled from each owner's last commit at or before the date (following renames backwards) |
| `--from-ref <rev> --owner <path>` | That owner uses the given commit; other owners use current content, with a warning |

Rules:
- **Fills missing keys only:** it never changes a recorded fingerprint, and criteria use current content.
- **Unbaselined refs:** a ref whose file or anchor didn't exist at the point is left unbaselined. JSON:
  `suites[].unbaselined: [{test, ref, reason}]`, with reasons `file did not exist at <hash>`, `anchor did not exist at
  <hash>` and `no commit at or before <date>`.
- **Refused with `INVALID_ARGUMENTS`:** `--force` with `--from-*`; `--from-ref` without `--owner`; `--owner` without
  `--from-ref`; `--from-date` or `--from-ref` without `--init`; `--from-date` together with `--from-ref`; an
  unparseable date.
- **Refused with `GIT_REF_UNAVAILABLE` (exit 4):** an `--owner` that isn't a readable repository, or a `--from-ref`
  that doesn't resolve in it.
- **Output:** the JSON gains `"from": {"date": "…"} | {"ref": "…", "owner": "…"}`.

## Test hook (internal)

`Spectra.CLI.Baseline.GitAccess.RunnerOverride` (internal, CLI) replaces the git process runner in tests. Core's
`GitReader` receives its runner through its constructor, so Core has no test hooks.

## `spectra docs topology [--output-format json]` (new, read-only)

```json
{"command":"docs-topology","success":true,
 "owners":[{"root":".","is_submodule":false,"head":"<40 hex>","shallow":false,"referenced_docs":12},
           {"root":"docs/requirements/gitbook-repo","is_submodule":true,"head":"<40 hex>","shallow":false,"referenced_docs":340}],
 "unowned":[{"doc":"../shared/x.md","reason":"not in a git repository"}],"warnings":[]}
```

## Git access (all commands)

- **Subcommands:** only `rev-parse`, `log`, `show`, `ls-files` and `cat-file`.
- **Every call:** `-C <owner> -c core.quotepath=false`, with `GIT_OPTIONAL_LOCKS=0` and `GIT_TERMINAL_PROMPT=0`. Output
  is NUL-separated where possible and decoded as UTF-8.
- **Nothing is ever written** under any `.git` directory.
