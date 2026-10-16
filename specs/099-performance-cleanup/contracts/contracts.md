# Contracts: Performance and Cleanup (Phase 6, Part 4)

099 changes no external contract. This file states what must stay identical and the internal contracts the guards
pin.

## C1 — External surface (unchanged)

For every command in the golden sequence (research R0) and every other command:
- files written: byte-identical;
- stdout (JSON and human): byte-identical after the documented masks;
- stderr: byte-identical after the documented masks;
- exit codes: identical;
- `--help` text: identical (the F6 rename is a private const; the JSON `command` values stay `docs-apply-move` /
  `docs-retire`).

No CLI command or option, config key, prompt template, skill/agent text or file format changes.

## C2 — Writer guard (F3, internal)

`TestFileWriter.FormatTestCase` / `WriteAsync` with a test whose `LayoutOmitted` is true:
- throws `InvalidOperationException`: `Test <id> was parsed without layout; a writer path must parse with layout capture`;
- writes nothing.

A test with `Layout == null` and `LayoutOmitted == false` is written canonically (unchanged).

## C3 — Count hooks (internal, tests only)

All hooks are members of one `ParseCounter` scope (`using var scope = ParseCounter.Begin();` around the measured
call; `AsyncLocal`, so parallel tests never see each other's counts):

| Member | Incremented in | Guard |
|---|---|---|
| `FullParses` | `TestCaseParser.Parse` | N per ingest call of a suite of N test files (`ingest-tests`, `ingest-update --test-id`, `ingest-update --all`, with and without `--keep-baseline`; `--dry-run` for `ingest-update` only); any remaining extra pinned at its real number with a Notes reason |
| `DocumentHeadingScans` | `BaselineContext` | one per distinct document path per context |
| `LayoutCaptures` | `TestCaseParser.AttachLayout` | 0 during a read-only command (`validate`, `show`, `docs index`, `update-triage`, `audit-grounding`, `ai analyze --coverage`, `dashboard`, `compile-*`) |
| `JsonParses` | `GeneratedTestIngestor` | K per `ingest-update --all` of K staged files; 1 per `--test-id` |

## C4 — Build

- `dotnet build --no-incremental`: 0 warnings in `Spectra.CLI` and `Spectra.Core` (restore advisories for
  `SixLabors.ImageSharp` in `Spectra.Execution` are out of scope; they also block `-c Release` today — separate task).
- `Spectra.CLI.csproj`, `Spectra.Core.csproj`: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (all configurations),
  `<WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>`.
