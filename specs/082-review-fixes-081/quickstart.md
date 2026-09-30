# Quickstart: verifying Spec 082

Each item's regression test must fail on `7df3fef` and pass after the fix. Run the new tests in a detached worktree
at `7df3fef` with the test files copied in, as in 080 and 081. Tests of brand-new APIs fail to compile there, which
counts as failing.

```bash
dotnet test --filter "FullyQualifiedName~ConfigLoaderEntryPoint|FullyQualifiedName~ConfigLoadingConsistency"   # A1, A3, A4
dotnet test --filter "FullyQualifiedName~TextNormalizer|FullyQualifiedName~CriteriaChangeset"                  # A2
dotnet test --filter "FullyQualifiedName~Skills"                                                               # A4 skill
```

Manual spot checks with the built CLI in a scratch workspace:

1. **A1.** Remove `coverage` from `spectra.config.json`, then run `spectra ai ingest-criteria --doc docs/a.md --from
   c.json --output-format json`. Expect exit 1, `error_code: "CONFIG_SECTION_MISSING"`, and no new file under
   `docs/criteria/`. Put `coverage` back and remove `tests`: the same command exits 0 with one `tests` warning.
2. **A3.** Set `"ai": null`. `spectra docs changed` runs normally.
3. **A4.** Break the JSON. `spectra docs changed --output-format json` prints one failure document with
   `error_code: "CONFIG_INVALID"`.
