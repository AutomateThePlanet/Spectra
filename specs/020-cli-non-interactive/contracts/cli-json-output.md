# CLI JSON Output Contract

## Global Flags

All commands accept these global options:

| Flag | Values | Default | Description |
|------|--------|---------|-------------|
| `--output-format` | `human`, `json` | `human` | Controls stdout rendering format |
| `--no-interaction` | (flag) | false | Fail with exit 3 if required args missing |
| `--verbosity` | `quiet`, `minimal`, `normal`, `detailed`, `diagnostic` | `normal` | Controls output detail level |
| `--dry-run` | (flag) | false | Preview without writing |
| `--no-review` | (flag) | false | Skip interactive review |

## Exit Code Contract

| Code | Condition |
|------|-----------|
| 0 | Success |
| 1 | Runtime error |
| 2 | Validation errors found |
| 3 | Missing required args with --no-interaction |
| 130 | User cancelled |

## JSON Output Rules

1. When `--output-format json`, stdout contains exactly one JSON object
2. No ANSI escape codes, no spinner characters, no progress bars in stdout
3. Verbose/diagnostic logs go to stderr when JSON mode is active
4. Error conditions produce a JSON error object (never unstructured text)
5. All timestamps are ISO 8601 UTC
6. All enum values serialized as lowercase strings
7. Null/empty optional fields are omitted from JSON output

## Required Arguments Per Command

| Command | Required Args (for --no-interaction) |
|---------|--------------------------------------|
| `spectra ai generate` | `<suite>` |
| `spectra ai update` | `<suite>` |
| `spectra ai analyze` | (none — all flags optional) |
| `spectra validate` | (none) |
| `spectra dashboard` | `--output` |
| `spectra list` | (none) |
| `spectra show` | `<test-id>` |
| `spectra init` | (none) |
| `spectra docs index` | (none) |
| `spectra config *` | subcommand-specific |
