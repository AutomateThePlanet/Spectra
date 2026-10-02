# Quickstart: verifying Spec 084

```bash
dotnet test --filter "FullyQualifiedName~Baseline"                    # T1–T5, T8, T9 (Core + CLI)
dotnet test --filter "FullyQualifiedName~BaselineInitTemplate"        # T6, T7
dotnet test --filter "FullyQualifiedName~UpdateTriagePerformance"     # SC-003, SC-004
dotnet test --filter "FullyQualifiedName~ConfigRuleMatrix|FullyQualifiedName~SkillsManifest"
dotnet test                                                           # all green except ProfileFormatLoaderTests
```

Manual check with the built CLI, in an empty scratch directory:

1. `spectra init`, then add `docs/storage.md`:
   ```markdown
   # Storage
   ## Quotas
   Each user gets 10GB.
   ## Archive tier
   Old files move after 90 days.
   ```
2. Ingest a test with `source_refs: [docs/storage.md#Quotas]`, using `spectra ai ingest-tests storage --from t.json`.
   `test-cases/storage/_source-baseline.json` has one entry, and `.spectra/source-snapshots/` has one file.
3. Run `spectra ai update-triage --suite storage --output-format json`. Expect every test UNCHANGED and no
   `Warning:` line.
4. Change `Each user` to `**Each** user` only. Triage: the ref is COSMETIC and auto-acked, and the next triage shows
   UNCHANGED.
5. Change `10GB` to `50GB`. Triage again. The test is in group G1 (LOAD) with `why: ["numbers: 10gb→50gb"]`, and
   `no_tests` lists `Archive tier`.
6. Run `spectra ai update-ack --suite storage --group G1`. Triage again: UNCHANGED. `git diff` shows only the
   baseline file and the new snapshot.
7. Delete `_source-baseline.json`. Triage: every test is UNBASELINED. `spectra ai source-baseline --init --suite storage`,
   then triage: clean.
8. With `--dry-run` on triage, ack and baseline, `git status` shows nothing new.
