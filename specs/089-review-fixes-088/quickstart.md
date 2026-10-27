# Quickstart: verifying Spec 089

```bash
dotnet test --filter "FullyQualifiedName~StagingFolderSplit"      # T1 (F1)
dotnet test --filter "FullyQualifiedName~MultipleTests"           # T2 (F2)
dotnet test --filter "FullyQualifiedName~UnchangedPending"        # T3 (F3)
dotnet test --filter "FullyQualifiedName~UpdateContextBuilder"    # T4 (F4)
dotnet test --filter "FullyQualifiedName~IngestUpdateExitCode"    # T5 (F5)
dotnet test --filter "FullyQualifiedName~StagingGitIgnore"        # T6 (F6)
dotnet test --filter "FullyQualifiedName~SkillsManifest"          # T7
dotnet test                                                        # everything; only the known ProfileFormatLoaderTests failure
```

Manual check with the built CLI in a scratch workspace:

1. **Setup:** run `spectra init`. Check that `.gitignore` lists `.spectra/updates/` and `.spectra/repairs/`. Add
   `docs/storage.md` with `## Quotas` ("30GB") and `## Sharing`. Add tests through `ai ingest-tests`:
   TC-101 and TC-102 on `#Quotas`, TC-103 on `#Sharing`.
2. **Change the docs:** change "30GB" to "45GB" (LOAD for TC-101 and TC-102). Reword the Sharing sentence slightly
   (LIGHT for TC-103). Run `spectra ai update-triage --suite storage --dry-run`.
3. **F3:** stage identical edits for TC-101 and TC-103 in `.spectra/updates/storage/`, then run
   `spectra ai ingest-update storage --all`.
   - TC-101 is `unchanged_pending` with `bucket: LOAD`.
   - TC-103 is `unchanged`.
   - Exit 0, and both staging files are gone.
   - The next triage dry run shows TC-101 still LOAD and TC-103 unchanged.
4. **F2 and F5:** stage a 3-element array as `updated-TC-102.json`, then run `--all`.
   - Exit 8, `failed` with `MULTIPLE_TESTS` naming 3 ids.
   - Nothing written; the file is kept.
5. **F1:** with that file still kept, stage `.spectra/repairs/storage/repaired-TC-101.json` (a real edit), then run
   `spectra ai ingest-update storage --all --keep-baseline`.
   - Only TC-101's repair is ingested (`written`, `baseline.mode: keep`), and `updated-TC-102.json` is untouched.
   - A following `--all` reads only `updated-TC-102.json`, never the repairs folder.
6. **F4:** make a test link a criterion that was never baselined, then run
   `spectra ai compile-update-prompt --suite storage --test-id …`. The line reads `(not baselined — compare the test
   with the current text)`.
7. **F6 on an existing workspace:** take a workspace whose `.gitignore` predates 089 and run `spectra update-skills`.
   The two entries are added once, nothing else changes, and a second run adds nothing.
