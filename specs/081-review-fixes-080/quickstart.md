# Quickstart: verifying Spec 081

Each item's regression test must fail on `1c8d8de` and pass after the fix. As in 080, run the new tests in a detached
worktree at `1c8d8de` with the test files copied in. Tests of brand-new APIs fail to compile there, which counts as
failing.

```bash
dotnet test --filter "FullyQualifiedName~CriteriaProvenance|FullyQualifiedName~SharedCriteriaFile"        # C1
dotnet test --filter "FullyQualifiedName~ConfigLoaderEntryPoint|FullyQualifiedName~ConfigLoadingConsistency"  # C2
dotnet test --filter "FullyQualifiedName~TextNormalizer"                                                    # C3
dotnet test --filter "FullyQualifiedName~CriteriaFailureShape|FullyQualifiedName~CriteriaSkill"            # C4
dotnet test --filter "FullyQualifiedName~FrontmatterCommentPlacement|FullyQualifiedName~TestCaseLayout"    # C5
```

Manual spot checks with the built CLI in a scratch workspace:

1. **C2.** Delete `ai` from `spectra.config.json`, then run `spectra docs changed --output-format json` and
   `spectra ai ingest-criteria …`. Neither is rejected. Delete `tests` too: `ingest-criteria` exits 1 with
   `spectra.config.json is invalid: missing 'tests' section`, and `compile-critic-prompt --suite s` prints the
   defaults warning.
2. **C4.** Run `spectra ai compile-extraction-prompt --output-format json`, then
   `spectra docs retire --doc docs/none.md --output-format json`. The stderr of each has `command`, `success`,
   `error_code`, `errors` and `warnings`.
