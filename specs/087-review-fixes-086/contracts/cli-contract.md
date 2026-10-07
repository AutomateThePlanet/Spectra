# CLI Contract: Spec 087 (changes to 086)

Exit codes, failure shape and JSON documents are as in 086. Only these change.

## Reasons

| Command | Reason |
|---|---|
| `update-triage` (`removed[].reason`, group `why`) | `not found; submodule not checked out: <path> (run git submodule update --init)` |
| `relink-sources` (`not_relinked[].reason`) | `submodule not checked out: <path> (run git submodule update --init)` |
| `source-baseline --from-*` (`suites[].unbaselined[].reason`) | the bare submodule reason; `git history unavailable: <git reason>`; `ambiguous rename` (path-at-point, Windows/macOS) |

`<path>` is the submodule's path relative to the repository root, with `/`. For a nested submodule it is the nested one.

## `update-triage` groups

Refs into a submodule that isn't checked out fold into one group per suite and submodule. Such groups are not chunked
by `update.group_size`. Their ref item is:

```json
{"ref": "docs/requirements/gitbook-repo", "state": "REMOVED", "submodule": true}
```

`submodule` is an optional key, present only on these items (like 086's `target`). The `removed` list stays one entry
per ref.

## `update-triage` notes and `source-baseline --init` warnings (analysis U1)

One line per submodule that isn't checked out:

```
N reference(s) from M test(s) point into submodule <path>, which isn't checked out (run git submodule update --init)
```

- **N** counts (test, ref) pairs: a ref listed by three tests counts 3. Every missing ref into the submodule counts,
  baselined or not, including refs of tests with no baseline entry.
- **M** is the number of distinct tests among them.
- Both are counted over the command's scope (the triaged suites; `source-baseline`'s suites).

It goes in triage's `notes`, and in `source-baseline --init`'s `warnings` (plain, `--from-date` and `--from-ref`; also a
`Warning:` line on stderr). Those refs stay unbaselined.

## `source-baseline --init`

| Option | Rule |
|---|---|
| `--min-similarity <N>` | 50–100, default 90; only with `--from-date` or `--from-ref`. Out of range → `INVALID_ARGUMENTS` `--min-similarity must be between 50 and 100 (got N).` (the same text as `relink-sources`); without `--from-*` → `INVALID_ARGUMENTS` `--min-similarity is only used with --from-date or --from-ref.` |

With `--from-*`, the `from` object gains `"min_similarity": N`.

## Git process start (all commands, and the execution identity)

- **Executable:** git is started by the absolute path of the first `git.exe` (Windows) / `git` found in an absolute
  PATH entry. Empty, `.`, relative and current-folder entries are skipped. None → `no git`.
- **Environment:** `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`,
  `GIT_ALTERNATE_OBJECT_DIRECTORIES`, `GIT_COMMON_DIR`, `GIT_NAMESPACE`, `GIT_CEILING_DIRECTORIES`,
  `GIT_DISCOVERY_ACROSS_FILESYSTEM`, `GIT_CONFIG`, `GIT_CONFIG_PARAMETERS`, `GIT_CONFIG_COUNT` and
  `GIT_CONFIG_KEY_*` / `GIT_CONFIG_VALUE_*` are removed; `GIT_OPTIONAL_LOCKS=0` and `GIT_TERMINAL_PROMPT=0` are set.
- **Arguments** are passed one by one, with no shell.

## Allowed subcommands

Unchanged: `rev-parse`, `log`, `show`, `ls-files`, `cat-file`.
