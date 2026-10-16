# Quickstart: Docs and Guidance (Phase 6, Part 5)

Run the guards only:

```bash
dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~Spectra.CLI.Tests.Docs"
```

Re-capture the golden files (Windows; after a text change), then review the diff:

```bash
SPECTRA_GOLDEN_RECORD=1 dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~GoldenOutputTests.FreshWorkspace"
```

```bash
git diff 71b1bea -- tests/Spectra.CLI.Tests/Golden/099/
```

Allowed changes: contracts.md C3. The pre-099 run must pass against `files-pre099.txt` without re-capture.

Full check (also in a fresh worktree checkout — 099 lesson on line endings):

```bash
dotnet build --no-incremental
```

```bash
dotnet test
```
