# CLI Behaviour Changes: Spec 085

## `spectra ai ingest-update` — new option

| Option | Forms | Effect |
|---|---|---|
| `--keep-baseline` | single (`--test-id`) and `--all` | The edited test's existing baseline fingerprints are not changed. Refs and criteria the edit added get entries; a test with no entry gets one. Used by critic repairs, which fix grounding, not doc drift |

The success JSON's `baseline` object gains `"mode": "rebuild"` (the default) or `"mode": "keep"`.

## All baseline writers — pruning

`ingest-tests`, `ingest-update`, `update-triage` (auto-ack), `update-ack` and `source-baseline --init` prune entries
for deleted tests **only when every `*.md` test file in the suite (not starting with `_`) parsed**. Otherwise nothing
is pruned in that suite, and the warnings say:

```
test-cases/storage/TC-9.md could not be parsed; no baseline entries were pruned in suite 'storage'.
```

The success JSON of `ingest-tests` and `ingest-update` gains `warnings` (always an array).

## Full rebuilds — dead refs

`ingest-update` (without `--keep-baseline`), `source-baseline --init --force` and `update-ack` keep the previous
fingerprint of a ref or criterion the test still lists but that no longer resolves or exists. It keeps reporting
REMOVED / CRITERIA_REMOVED until the test drops it.

## `update-triage` — anchors, words, `no_tests`

- **Anchor matching**, in order, first hit wins:
  1. the heading's text key (HTML and links removed, a trailing `{#…}` / `{.…}` block removed);
  2. an explicit ID (`id="…"`, `id='…'`, `{#id}`);
  3. a GitHub duplicate suffix `-n`, meaning the (n+1)th heading with the base's text key.
- **LOAD words:** must, should, may, required, no, not, never, none, nothing, cannot, can't, don't, doesn't, won't,
  isn't, aren't, shouldn't, mustn't, without, unless, except, only, always, all, any, every. A curly apostrophe counts
  as a straight one.
- **`no_tests`** headings are shown as cleaned text (no HTML, no `{#…}`).

## Every command — output encoding

- A redirected stdout or stderr is written as UTF-8 without a BOM.
- An interactive console is left as the runtime provides it; the terminal's code page is never changed.
- 084's assignment of `Console.OutputEncoding` is removed.

## Unchanged

- The baseline file format, the snapshots, the detail file.
- Exit codes, error codes and the failure shape.
- Every other command's output.
