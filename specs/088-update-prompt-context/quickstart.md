# Quickstart: verifying Spec 088

```bash
dotnet test --filter "FullyQualifiedName~UpdateContextBuilder"        # T1, T2
dotnet test --filter "FullyQualifiedName~CompileUpdatePrompt"          # T3, T4, T6, T7, SC-001
dotnet test --filter "FullyQualifiedName~IngestUpdate"                 # T5 + existing ingest tests
dotnet test --filter "FullyQualifiedName~UpdatePromptEndToEnd"         # T8
dotnet test                                                            # everything; known ProfileFormatLoaderTests failure only
```

Manual check with the built CLI in a scratch workspace:

1. `spectra init`. Add `docs/storage.md` with `## Quotas` ("30GB") and `## Sharing`, criteria `AC-1` (quotas) and
   `AC-2` (sharing), and three tests: TC-101 `[docs/storage.md#Quotas]` + `AC-1`; TC-102 `[docs/storage.md#Quotas]`;
   TC-103 `[docs/storage.md#Sharing]` + `AC-2`. Run `spectra ai source-baseline --init --all-suites`.
2. Change "30GB" to "45GB". `spectra ai update-triage --suite storage`: one LOAD group (TC-101, TC-102),
   `numbers: 30gb→45gb`.
3. `spectra ai compile-update-prompt --suite storage --group G1`: `#### S1 — docs/storage.md#Quotas` appears once, with
   the current section ("45GB"), the previous normalised version ("30gb") and `What changed`; criteria list only `AC-1`;
   `### TEST TC-101` and `### TEST TC-102` once each; no `AC-2`.
4. Stage `.spectra/updates/storage/updated-TC-101.json` (an edit to 45GB) and `updated-TC-102.json` (identical to the
   original), then `spectra ai ingest-update storage --all`: TC-101 written, TC-102 unchanged; both staging files gone.
5. `spectra ai update-triage --suite storage`: TC-101 and TC-102 UNCHANGED; `git status` shows only TC-101.md and the
   baseline changed.
6. `spectra ai compile-update-prompt --suite storage --group G9` → `GROUP_NOT_FOUND`; a test with only a deleted doc
   and no criteria → `NOTHING_TO_RECONCILE`.
