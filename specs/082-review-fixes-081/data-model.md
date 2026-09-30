# Data Model: Spec 082 (changes only)

## ConfigSections and the loader (A1, A3)

```text
[Flags] enum ConfigSections { None = 0, Source = 1, Tests = 2, Coverage = 4 }   // `ai` is never read

ConfigLoader.LoadForReadAsync(repoRoot, ConfigSections reads, ct)
ConfigLoader.LoadForWriteAsync(repoRoot, ConfigSections requires, ConfigSections reads = None, ct)

Pre-scan: for each of source, tests, coverage, ai whose top-level key is absent or `null` (case-insensitive key match):
          replace with {} and record it as missing. Then deserialize and validate as before.

  missing ∈ requires (write)  → Error "spectra.config.json is invalid: missing '<s>' section", ErrorCode CONFIG_SECTION_MISSING
  missing ∈ reads             → defaults + Warning "spectra.config.json has no '<s>' section; using the built-in defaults."
  missing ∉ requires ∪ reads  → defaults, silently
```

## ConfigLoadResult (A4)

```text
ConfigLoadResult(SpectraConfig? Config, string? Error)
  + string? ErrorCode                  // CONFIG_MISSING | CONFIG_INVALID | CONFIG_SECTION_MISSING; null on success
  + IReadOnlyList<string> Warnings     // unchanged from 081
```

| Situation | ErrorCode |
|---|---|
| No file (write load) | `CONFIG_MISSING` |
| Unreadable, not JSON, top-level not an object (incl. `null`, empty file), validation or type error | `CONFIG_INVALID` |
| A `requires` section missing or `null` | `CONFIG_SECTION_MISSING` |

## Declared sections

| Command | requires | reads |
|---|---|---|
| `ingest-criteria`, `docs retire` | Coverage | Tests |
| `docs apply-move`, `docs migrate-criteria`, `--import-criteria` | Coverage | — |
| `compile-extraction-prompt` | — | Coverage |
| `compile-critic-prompt` | — | Source, Tests, Coverage |
| `compile-repair-prompt`, `compile-repair-batch` | — | Source, Tests |
| `docs changed` | — | Source, Coverage |

## ErrorResult (A4)

It gains `error_code` (`string?`, omitted when null), which `--import-criteria` sets on config failures.

## HTML-form tag rule (A2)

A known-element span is markup when it is:
- (a) a closing tag, any case;
- (b) a tag with at least one attribute, any case;
- (c) a bare tag with an all-lowercase name;
- (d) a bare opening tag with a matching closing tag later in the text (case-insensitive).

Otherwise it is text.
