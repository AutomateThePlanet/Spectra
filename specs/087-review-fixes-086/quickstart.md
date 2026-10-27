# Quickstart: verifying Spec 087

```bash
dotnet test --filter "FullyQualifiedName~Spectra.Core.Tests.Git"              # executable, environment, gitlinks, casing seam
dotnet test --filter "FullyQualifiedName~Commands.Git"                          # T1–T5 against real repositories
dotnet test                                                                     # everything; known ProfileFormatLoaderTests failure only
```

Manual check with the built CLI, in scratch repositories:

1. **Submodule.** A docs repository `child` and a tests repository `main` with `child` as the `docs/sub` submodule
   and a test referencing `docs/sub/guide.md#Setup`, baselined and committed. `git clone main clone` (without
   `--recurse-submodules`), then `spectra ai update-triage --all-suites` in `clone`: one REMOVED group for `docs/sub`,
   with `not found; submodule not checked out: docs/sub (run git submodule update --init)`. Run
   `git submodule update --init` and triage again: UNCHANGED.
2. **Executable.** Copy any program to `clone/git.exe` (Windows) and run triage from `clone`: the copied program never
   runs (the output is as in step 1).
3. **Hook environment.** In `main`, rename `guide.md` inside the submodule and commit there. Set `GIT_DIR=<main>/.git`
   and `GIT_INDEX_FILE=<main>/.git/index`, run triage: RELINK to `docs/sub/<new name>`.
4. **Casing** (Windows/macOS). Reference `docs/Sub/Guide.md#Setup` for a file git records as `docs/sub/guide.md`,
   rename it, triage: RELINK with git's casing.
5. **Bootstrap threshold.** A doc renamed with heavy edits: `source-baseline --init --all-suites --from-date <before>`
   leaves it unbaselined; with `--min-similarity 60` it is baselined from the old path. `--min-similarity 60` without
   `--from-date` → `INVALID_ARGUMENTS`.
6. `git status` in every repository shows only SPECTRA's own files; no `index.lock` anywhere.
