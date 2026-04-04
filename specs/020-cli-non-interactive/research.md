# Research: CLI Non-Interactive Mode and Structured Output

## R1: Global Options Registration Pattern

**Decision**: Add `--output-format` and `--no-interaction` as global options via `GlobalOptions.AddTo()`
**Rationale**: The existing pattern in `GlobalOptions.cs` already registers `--verbosity`, `--dry-run`, and `--no-review` globally. Adding two more follows the established pattern. The generate command's local `--no-interaction` will be removed in favor of the global one.
**Alternatives considered**: Per-command options (rejected — would require duplication across 12+ commands and inconsistent behavior)

## R2: VerbosityLevel Enum Alignment

**Decision**: Keep existing `VerbosityLevel` enum (Quiet, Minimal, Normal, Detailed, Diagnostic) as-is
**Rationale**: The spec mentions quiet/normal/verbose, but the codebase already has a 5-level enum that is more granular. The existing enum already covers the spec's needs: Quiet maps to "quiet", Normal to "normal", Detailed/Diagnostic to "verbose". No change needed.
**Alternatives considered**: Replacing with 3-level enum (rejected — would break existing code and tests that reference Minimal/Detailed/Diagnostic)

## R3: OutputFormat Enum

**Decision**: New `OutputFormat` enum with `Human` (default) and `Json` values
**Rationale**: Simple enum matching the spec. The existing `ReportFormat` in the analyze command has `Json/Markdown/Text` for file output — different concern. `OutputFormat` controls stdout rendering format for all commands.
**Alternatives considered**: Reusing `ReportFormat` (rejected — semantically different; report format is for file output, output format is for stdout rendering)

## R4: ExitCodes Extension

**Decision**: Add `MissingArguments = 3` to `ExitCodes` static class
**Rationale**: Spec requires exit code 3 for missing args in non-interactive mode. The existing class has Success=0, Error=1, ValidationError=2, Cancelled=130. Adding 3 fits naturally.
**Alternatives considered**: Reusing Error=1 for missing args (rejected — spec explicitly requires distinct code 3 for SKILL/CI integration)

## R5: JSON Output Pattern

**Decision**: Introduce `CommandResult<T>` base record with common fields (command, status, timestamp) and per-command derived types. A `JsonResultWriter` utility serializes to stdout.
**Rationale**: Each command has different result data. A typed result model per command enables compile-time safety and clear JSON schemas. The writer handles serialization options (camelCase, indented, enums as strings).
**Alternatives considered**: 
- Anonymous objects (rejected — no compile-time safety, hard to test)
- Dictionary<string, object> (rejected — same issues, plus boxing)
- Single flat result class with nullable fields (rejected — unclear which fields apply to which command)

## R6: Suppressing Spectre.Console Output in JSON Mode

**Decision**: Pass `OutputFormat` to `ProgressReporter` and presenters. When `Json`, all methods become no-ops. Verbose logging goes to stderr via `Console.Error.WriteLine()`.
**Rationale**: The presenters already check verbosity levels. Adding an output format check follows the same pattern. Using stderr for verbose logs in JSON mode follows Unix conventions (stdout for data, stderr for diagnostics).
**Alternatives considered**:
- Redirecting Spectre.Console to /dev/null (rejected — too coarse, loses stderr logging)
- Post-processing stdout to strip ANSI (rejected — fragile and slow)

## R7: Non-Interactive Argument Validation

**Decision**: Each command handler checks `isNonInteractive` early. If required args are missing, return exit code 3 with a JSON error (if JSON mode) or text error listing the missing args.
**Rationale**: The generate handler already does this check. Standardizing across all handlers follows the same pattern. The error message lists which args are missing so SKILL files and CI can diagnose issues.
**Alternatives considered**: Middleware/filter approach (rejected — System.CommandLine doesn't have a clean middleware for this; per-handler check is simpler and more explicit)

## R8: Analyze Command --format Migration

**Decision**: Keep the analyze command's `--format` flag for backward compatibility but make `--output-format` the primary global flag. When `--output-format json` is used, it controls stdout rendering. The existing `--format` on analyze controls the report file format and is a separate concern.
**Rationale**: The two flags serve different purposes: `--output-format` controls how results appear on stdout (human vs JSON), while `--format` on analyze controls the file output format (json/markdown/text). They can coexist.
**Alternatives considered**: Removing `--format` from analyze (rejected — breaking change, and it serves a different purpose)
