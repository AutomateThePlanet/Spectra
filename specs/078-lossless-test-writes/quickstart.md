# Quickstart: verifying Spec 078

Run the steps from the repo root after implementation.

```bash
dotnet build
dotnet test
```

## 1. Lossless writes (US1, US4)

Create a workspace with one suite and one test file that has every field set.

1. Put an unknown key in the frontmatter (`owner: qa-team`).
2. Put a `## Notes` section between Steps and Expected Result.
3. Give one step a sub-bullet line.

Then run each writer path, and after each one run `git diff`:

```bash
spectra ai ingest-grounding --suite smoke --all             # after placing a grounded verdict in .spectra/verdicts/
spectra ai update --suite smoke --no-interaction --no-review # with the test's only doc deleted → orphan marking
```

Expected:
- **Grounding write-back**: only the `grounding:` block changes.
- **Orphan marking**: only `status`, `orphaned_reason` and `orphaned_date` change.
- **In both cases**, `owner`, `## Notes`, the sub-bullet, `automated_by`, `requirements`, `bugs` and `custom` are all
  byte-identical.

## 2. Ref resolution (US2)

1. Use tests with `source_refs: ["docs/checkout.md#Payment"]` where `docs/checkout.md` exists.
2. Run the command below. Expected: 0 ORPHANED "Source document not found or empty".
3. Delete `docs/checkout.md` and re-run. Expected: only the tests referencing that file alone are ORPHANED.

```bash
spectra ai update --suite checkout --dry-run
```

## 3. Classification writes nothing (US3)

1. Run the command below.
2. Check `git status --porcelain`. Expected: empty. The run-status files are gitignored by `spectra init`.
3. Check that the classification tables were printed to stdout.
4. Check that `.spectra-result.json` contains the classification counts and `"applied": false`.
5. Repeat without `--dry-run` (`--no-interaction` only). Expected: the same result, plus the hint
   `Pass --no-review to apply.`

```bash
spectra ai update --suite checkout --diff --dry-run --no-interaction --output-format json --verbosity quiet
```

## 4. Criteria skill (US5)

Expected: no match for "file tools to read".

```bash
grep -n "file tools to read" src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md
```
