# Quickstart: verifying Spec 086

```bash
dotnet test --filter "FullyQualifiedName~Git"                   # T1–T3 (GitReader, owners, renames)
dotnet test --filter "FullyQualifiedName~Relink"                # T4, T7, skill
dotnet test --filter "FullyQualifiedName~HistoryBootstrap"      # T5
dotnet test --filter "FullyQualifiedName~Baseline"              # 084/085 guards + T6
dotnet test                                                     # everything; known ProfileFormatLoaderTests failure only
```

Manual check with the built CLI, in a scratch git repo:

1. `git init`, then `spectra init`. Add `docs/a.md` (with `## Quotas`, "10GB") and a test referencing
   `docs/a.md#Quotas`. Commit, then run `spectra ai source-baseline --init --all-suites`.
2. `git mv docs/a.md docs/guides/a.md` and change "10GB" to "50GB" in the same commit.
3. `spectra ai update-triage --all-suites`: the test is RELINK, `renamed to docs/guides/a.md`.
4. `spectra ai relink-sources --all-suites --dry-run` shows `docs/a.md#Quotas → docs/guides/a.md#Quotas`, and
   `git status` is clean.
5. `spectra ai relink-sources --all-suites`: `git diff` shows only the ref line in the test, `_index.json` and the
   baseline key. Then `update-triage` shows LOAD, `numbers: 10gb→50gb`.
6. Check that no `.git/index.lock` exists, and that `git status` shows only SPECTRA's own file changes.
7. Bootstrap: delete the baseline, then `source-baseline --init --all-suites --from-date <the date before step 2>`.
   Triage shows LOAD for the number change.
8. `spectra docs topology` lists one owner (`.`) with 1 referenced doc.
9. Outside any git repo (copy the workspace without `.git`): triage shows REMOVED `not found; not in a git repository` instead
   of RELINK, and nothing fails.
