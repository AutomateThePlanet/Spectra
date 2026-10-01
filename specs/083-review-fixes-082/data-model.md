# Data Model: Spec 083 (changes only)

## ConfigLoader

```text
[Flags] enum ConfigSections { None = 0, Source = 1, Tests = 2 }        // Coverage removed

LoadForReadAsync(repoRoot, ConfigSections reads, ct)                    // missing file → SpectraConfig.Default
LoadForWriteAsync(repoRoot, ConfigSections reads, ct)                   // missing file → CONFIG_MISSING; `requires` removed

ConfigLoadResult(Config, Error) { ErrorCode ∈ {CONFIG_MISSING, CONFIG_INVALID} | null; Warnings }
ErrorConfigSectionMissing — removed
```

**Pre-scan order**
1. Read the file. Unreadable → `CONFIG_INVALID`.
2. Parse it and materialise the keys. A `JsonException` or `ArgumentException` (an exact duplicate key) →
   `CONFIG_INVALID`.
3. A root that is not an object → `CONFIG_INVALID`.
4. Top-level keys equal ignoring case → `CONFIG_INVALID`: `duplicate top-level key '<a>' / '<b>' (keys may not differ
   only in case)`.
5. `source`, `tests`, `coverage` and `ai` that are absent or `null` → `{}`. Each one in `reads` gets the warning
   `spectra.config.json has no '<s>' section; using the built-in defaults.`
6. Deserialize and validate. A failure → `CONFIG_INVALID`.

## Declared reads

| Command | reads |
|---|---|
| `ingest-criteria`, `docs retire` | `Tests` |
| `compile-critic-prompt`, `compile-repair-prompt`, `compile-repair-batch` | `Source \| Tests` |
| `docs changed` | `Source` |
| `docs apply-move`, `docs migrate-criteria`, `--import-criteria`, `compile-extraction-prompt` | `None` |

## Init template

`src/Spectra.CLI/Templates/spectra.config.json` gains `"coverage": { "criteria_dir": "docs/criteria",
"criteria_file": "docs/criteria/_criteria_index.yaml" }`, after `tests`.

## HTML-form tag rule

A known-element span is markup when it is one of:
1. a closing tag, any case;
2. a tag with at least one attribute, any case;
3. a self-closing bare tag `<name/>`, any case;
4. a lowercase `<br>`, `<hr>` or `<wbr>`;
5. a bare opening tag followed later by a closing tag of exactly the same spelling (case-sensitive).

Otherwise it is text. Residual: a lowercase placeholder with its exact lowercase close later in the text counts as
markup.
