# Quickstart: verifying Spec 090

```bash
dotnet test --filter "FullyQualifiedName~EditDropsVerdict"        # T1
dotnet test --filter "FullyQualifiedName~VerdictStale"            # T2
dotnet test --filter "FullyQualifiedName~ManualVerdict"           # T3
dotnet test --filter "FullyQualifiedName~RepairLoopAfterEdit"     # T4
dotnet test --filter "FullyQualifiedName~SkillsManifest"          # T5
dotnet test --filter "FullyQualifiedName~FailedStaging"           # T6 (fails on fc82b7a)
dotnet test --filter "FullyQualifiedName~IngestUpdateDryRun"      # T7
dotnet test --filter "FullyQualifiedName~SafeDelete"              # T8, T9
dotnet test --filter "FullyQualifiedName~LegacyUpdateDelete"      # T10
dotnet test --filter "FullyQualifiedName~GitIgnoreOffer"          # T11–T13 (fail on fc82b7a)
dotnet test --filter "FullyQualifiedName~AuditGroundingUnverified" # T14 (FR-A7)
dotnet test                                                        # everything; only the known ProfileFormatLoaderTests failure
```

Manual check with the built CLI in a scratch workspace:

1. **Setup:** run `spectra init`, add a doc and three tests through `ai ingest-tests`. Run `compile-critic-prompt
   --suite s --test TC-101`; check that `.spectra/verdicts/critic-input-TC-101.json` exists. Write a `grounded` verdict
   file for TC-101, then run `ingest-verdict --suite s --all` and `ingest-grounding --suite s --all`. TC-101 has a
   grounding block.
2. **Edit drops the verdict (A):** stage an edit of TC-101 and run `ingest-update s --all`. The grounding block, the
   verdict file and the record are gone, and `audit-grounding --suite s` shows TC-101 unverified.
3. **Stale verdict (A):** run `compile-critic-prompt` for TC-102 and write its verdict. Edit TC-102 by hand, then run
   `ingest-grounding --suite s --test TC-102`: exit 4 `VERDICT_STALE`. Give TC-103 a Manual block, then run
   `ingest-grounding --suite s --test TC-103`: exit 4 `MANUAL_VERDICT`.
4. **Failed staging (B):** stage a malformed `updated-TC-102.json` and run `--all`: exit 8, `failed-TC-102.json`. Run
   `--all` again: nothing reported, exit 0. Stage a good `updated-TC-102.json` and run `--all`: written, and
   `failed-TC-102.json` is gone.
5. **Dry run (B):** stage files of several outcomes and run `--all --dry-run`. The same outcomes and exit code appear as
   in the real run that follows, and the workspace hash is unchanged after the dry run.
6. **Delete (C):** TC-102 has `depends_on: TC-101`, and TC-103's body has `- TC-101`. Run `spectra delete TC-101
   --dry-run`, then for real. Only TC-102's `depends_on` line is gone, TC-103 is byte-identical, and TC-101's baseline
   entry, staging and verdict files are removed.
7. **Gitignore (D):** remove `.spectra/progress.json` from `.gitignore`, convert it to CRLF without a final newline, and
   delete `gitignore_offered` from the manifest (pre-090). Run `spectra update-skills`: `.spectra/progress.json` is not
   re-added, the existing bytes are unchanged, and the new lines are CRLF. Run it again: no change. Delete `.gitignore`
   and run `update-skills`: no file is created, and a warning lists the patterns.
8. **Fresh clone (FR-A7):** delete `.spectra/verdicts/` and run `spectra ai audit-grounding --suite s`. Every test with a
   grounding block keeps its block's verdict (never `none`), and `unverified` counts only tests without a block.
