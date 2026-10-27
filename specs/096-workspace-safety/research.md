# Research: Workspace Safety and Hygiene (Phase 6, Part 1)

All facts verified on `23aa08c`. No open questions.

## R1 — One manifest key form (F1)

**Facts:**
- `InitHandler` writes absolute keys for skills and agents, and relative keys (with `Path.Combine`, so `\` on Windows) for
  `USAGE.md`, the prompt templates, `profiles/_default.yaml` and `CUSTOMIZATION.md`.
- `PromptsResetHandler` writes a relative key. `UpdateSkillsHandler` reads and writes absolute keys.
- `CompileBatchPrompt.IsUneditedCopyAsync` tries the absolute key and both relative forms.

**Decision:** add `Spectra.CLI.Skills.ManifestKey`:
- `For(string root, string path)` gives the repo-relative path with `/` separators.
- `TryNormalize(string root, string key, out string normalized)` returns false for a key outside `root`.

`SkillsManifestStore.LoadAsync` normalises every key:
- an absolute key under the root becomes relative;
- a relative key keeps its `\` → `/`;
- **a key outside the root (analyze M1):** an absolute key outside the current workspace is **re-rooted** when its path
  ends with a known install path — `.claude/skills/<name>/SKILL.md`, `.claude/agents/<file>`, `.spectra/prompts/<id>.md`, `profiles/_default.yaml`, `CUSTOMIZATION.md`, `USAGE.md` — matching on the last path segments (case-insensitive on
  Windows). Its hash is kept under the normalised relative key, and joins the duplicate candidates below if that key
  already exists. Only keys that match no install path are dropped, reported as **one** summary warning:
  "N manifest entries from another location were dropped (was the workspace moved?)". A moved workspace therefore keeps every record, with no warning and no no-record skips;
- **duplicates after normalisation (analyze H1):** when several keys normalise to the same path, all their hashes are
  kept as *candidates*. Each was written by SPECTRA, so a user-edited file matches none of them. A file counts as
  unedited when its current hash equals **any** candidate. On save, one entry is written under the normalised key: the
  candidate that matched, or the bundled hash after an update; the rest are dropped. If no candidate matches, the file
  is customized (skipped, existing message). Example: `init` wrote `.spectra\prompts\x.md` = hash(v1), the base's
  `update-skills` overwrote the file with v2 under the absolute key = hash(v2); file = v2 → matches → unedited (updated
  or unchanged), never "customized". "First in ordinal order" would have kept hash(v1) and frozen the file.

Every writer (`InitHandler`, `PromptsResetHandler`, `UpdateSkillsHandler`) and reader (`UpdateSkillsHandler`,
`IsUneditedCopyAsync`) goes through `ManifestKey.For`. The next save writes only normalised keys.

**Rationale:** one form removes the init/update-skills mismatch at its root. Normalising on load fixes every existing
manifest without a migration step.

## R2 — update-skills with no record (F1)

**Decision:** in `UpdateSkillsHandler.ProcessFileAsync`, for an existing file with no record:
- content hash = bundled hash → record it, and list it as unchanged;
- otherwise → `skippedNoRecord` (never written).

The report lists each skipped file as "skipped (no record — it may have been edited); run spectra prompts reset <id> or
delete it to get the new version" for templates, and "…; delete it to get the new version" for the others. The rest is
unchanged:
- missing file → recreate and record;
- record ≠ file hash → skipped as customised;
- record = file hash → update when the bundled content differs, else unchanged.

**Why this fails on the base without touching bundled content:** the real init records templates under relative keys and
`update-skills` looks them up by absolute path, so the record is never found. An edited template differs from the bundled
text, so the base overwrites it.

## R3 — Suite delete through the deletion service (F2)

**Facts:**
- `CascadeRemoveDependsOnAsync` regex-edits every other suite's files: it removes indented `- <ID>` lines anywhere and
  collapses blank-line runs. That runs for every file, not just the dependents.
- `CountExternalDepsAsync` finds the dependents.
- Leftovers are not touched.

**Decision:**
- `SuiteDeleteHandler` loads `TestDeletionService` and resolves every test of the suite (its index, or a scan). For
  `--force` and the dry run it uses the service's plan:
  - scalar dependents in other suites — cleaned with `TestFileWriter` (lossless);
  - warnings for list or unparseable dependents;
  - baseline entries, staging and verdict files.
- The suite folder is deleted as before; the deletion of its tests' own files is covered by that, and the suite's
  `_source-baseline.json` lives inside the folder.
- **The suite's own baseline (analyze M2):** suite delete never removes the suite's own baseline entries and takes no
  baseline lock for it — `_source-baseline.json` is deleted with the suite folder. It runs only the other suites'
  dependent cleanup, the verdict and fingerprint-record files, the staging folders and the generation plan. Other suites'
  baseline files stay byte-identical.
- Added suite-level leftovers:
  - `.spectra/updates/<suite>/` and `.spectra/repairs/<suite>/` (whole folders, incl. `failed-*`);
  - `.spectra/generation-plan/<suite>.json`;
  - the tests' `critic-verdict-<id>.json` and `critic-input-<id>.json` (from the service).
- The refusal rules (automation links, external dependencies without `--force`) are unchanged, but
  `ExternalDependencyCleanup` is now built from the service's plan (scalar dependents plus the warned ones), so counts
  stay meaningful. `CascadeRemoveDependsOnAsync` is deleted.
- `SuiteDeleteResult` adds `dependents_cleaned`, `warnings`, `leftovers_removed` (paths). In dry run, `leftovers` lists
  them.

**Why the tests fail on the base:** the regex removes an indented `  - TC-101` step line in another suite. It also
collapses a 3-newline run in an unrelated file (the rewrite runs on all files when any dependent exists). Scalar
`depends_on` is ignored, and the leftovers stay.

## R4 — show (F3)

**Decision:** `ShowHandler` uses the suite folder plus the index `file`; if that file doesn't exist, it tries the tests
folder plus `file` (legacy `suite/file`). The same rule is used by `AuditGroundingHandler` / `CompileRepairBatchCommand`
since 073.

## R5 — validate (F4)

**Decision:** `failed = !result.IsValid || totalErrors > 0`. Both outputs (JSON status and message, human
"Validation passed." vs "N error(s) found.") and the exit code use `failed`. Warnings don't change it.

## R6 — Offered markers in `.gitignore` (F5)

**Decision:**
- A marker is a comment line `# spectra-offered: <p1>, <p2>, …`; patterns contain no `, `. A pattern counts as offered
  when it is named by a marker line.
- `EnsureForInitAsync` writes the patterns, then one marker line naming all of them.
- `OfferNewAsync(root, legacyRecord)`:
  - `offered` = markers ∪ `legacyRecord` (the per-clone manifest list, migration) ∪ (`Pre090Offered`, only when there
    are neither markers nor a record);
  - candidates = `Patterns − offered`; patterns present or negated are not re-added (090 rules);
  - then one marker line naming every pattern of `offered ∪ candidates` that has no marker yet is appended (the
    migration and the new offers, once).
  - No `.gitignore` → nothing written (090 rule).
- The appended block keeps 090's byte-preserving append (file newline style; `# SPECTRA` section header only if absent).
- `manifest.GitIgnoreOffered` is still read for the one-time migration and is no longer written (init no longer sets
  it). The JSON property stays for old manifests.
- A pattern the user's `.gitignore` negates counts as offered (090 rule) and gets a marker too, so it is never offered
  again (analyze L2).
- `Patterns` gains `.spectra/verdicts/` and `.spectra/dropped-tests.json`. `Pre090Offered` stays the explicit 089 list
  (094 R10), so these two new entries are offered to pre-090 workspaces too.

**Rationale:** `.gitignore` is committed, so the record is per repository. A single marker line per offer keeps the file
readable (two lines per upgrade at most). Users can delete a marker to be offered a pattern again.

**Alternatives:** one marker per pattern (≈ 25 comment lines on init); a separate committed file (another file to
explain).

## R7 — Delete performance (F6)

**Facts:** `TestDeletionService.LoadAsync` reads and fully parses (`TestCaseParser`, with the 078 layout capture) every
test file.

**Decision:**
- `LoadAsync` builds the catalog from a frontmatter-only read of each file: the text up to the closing `---`, parsed with
  the existing `FrontmatterOnly` (YamlDotNet), giving the id and the `depends_on` node.
- A full `TestCaseParser.Parse` runs lazily, only for:
  - targets (title, `automated_by`);
  - files whose scalar `depends_on` names a deleted id (they need a `TestCase` for the lossless writer).
- Target resolution goes through each suite's `_index.json` (id → file) when present, falling back to the catalog. Ids
  missing from an index are still found through the catalog, so results are identical.
- **Deterministic guard (analyze H2):** `TestDeletionService` gets an internal full-parse counter (a test hook, through
  `InternalsVisibleTo` as 086's `GitAccess`). The row asserts that deleting one test among 2,500 with one scalar
  dependent performs exactly 2 full parses (the target and that dependent), not 2,500. The elapsed time is only written
  to the test output. SC-005 stays a recorded measurement: `23aa08c` (T002) against 096, on the same machine and data,
  in T031 / Notes — not an assertion.

## R8 — `.gitattributes` (F7)

**Facts:** the three `specs/*/acceptance/*.sh` files are `i/lf w/crlf` (`core.autocrlf=true`).

**Decision:**
- Add `.gitattributes` with `*.sh text eol=lf`, then rewrite the three working copies with LF (their index content is
  already LF, so the commit changes only `.gitattributes`).
- Test (`[GitFact]`): `git check-attr eol -- <script>` reports `lf`, and the working copy has no CR.

## R9 — Test infrastructure (F8)

**Decision:**
- Add `tests/Spectra.CLI.Tests/TestFixtures/WorkingDirectoryCollection.cs`:
  `[CollectionDefinition("WorkingDirectory", DisableParallelization = true)]`. Like `CriteriaCommands`, it runs
  sequentially, after the parallel collections.
- `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown` asserts the headings the embedded guide has: drop
  the missing one(s) (determined at implementation) and keep the rest.

## R10 — The unused key (F9)

**Decision:**
- Remove `"generation_batch_size": 30` from `src/Spectra.CLI/Templates/spectra.config.json`.
- `AiConfig.GenerationBatchSize` stays, with `[Obsolete("Unused; generation.batch_size applies (Spec 094/096).")]`; no
  code reads it.
- `docs/configuration.md` marks it as unused and points to `generation.batch_size`.
- A test asserts the template has no `generation_batch_size`, and that an old config containing it loads (guard).

## R11 — Baseline and proof

- Baseline on `23aa08c`: Core 1245 / CLI 2194 (1 known failure — F8 fixes it) / Execution 228 / Integration 8;
  4 warnings.
- Rows using new members (`ManifestKey`) can't compile on the base: staged out for the proof run, as in 094/095.
