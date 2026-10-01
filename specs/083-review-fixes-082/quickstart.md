# Quickstart: verifying Spec 083

Each fix's regression test must fail on `3594d62` and pass after. As in 080–082, run the new tests in a detached
worktree at `3594d62` with the test files copied in. Tests that use removed or new APIs fail to compile there, which
counts as failing.

```bash
dotnet test --filter "FullyQualifiedName~ConfigRuleMatrix|FullyQualifiedName~ConfigLoaderEntryPoint"     # T1, T5
dotnet test --filter "FullyQualifiedName~InitTemplateWorkspace|FullyQualifiedName~InitCommandTests"      # T2, T3, F1
dotnet test --filter "FullyQualifiedName~TextNormalizer"                                                  # T4
```

Manual check with the built CLI, which is exactly what failed on `3594d62`:

1. In an empty scratch directory, run `spectra init`.
2. Add `docs/a.md`.
3. Run `spectra docs changed`. Expect no `Warning:` line.
4. Run `spectra ai ingest-criteria --doc docs/a.md --from c.json`. Expect exit 0, criteria in `docs/criteria/`, and no
   warning.
5. Delete the `coverage` block from `spectra.config.json` and repeat steps 3–4. Expect the same result.
6. Add a second `"Tests": null` key. `spectra docs changed` exits 1 with `CONFIG_INVALID` naming `tests`.
