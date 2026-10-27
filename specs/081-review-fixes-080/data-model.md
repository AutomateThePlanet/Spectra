# Data Model: Spec 081 (changes only)

## CriteriaProvenance (C1)

```text
OwnedBy(criteria, doc, repoRoot) : criteria whose source_doc and doc normalise, via
                                   CriteriaPaths.NormalizeSourceDoc(value, repoRoot), to the same repo-relative path
                                   (OrdinalIgnoreCase). A value that fails to normalise (empty, or outside the
                                   repository) belongs to no document.
SameDocument(a, b, repoRoot)     : the same comparison for two document paths (index lookup)
IsShared(index, source)          : unchanged (criteria-file names are relative to the criteria dir)
```

## ConfigSections and ConfigLoadResult (C2)

```text
[Flags] enum ConfigSections { None = 0, Source = 1, Tests = 2 }   // `ai` is never read; `coverage` stays optional

ConfigLoader.LoadForReadAsync(repoRoot, ConfigSections reads, ct)
ConfigLoader.LoadForWriteAsync(repoRoot, ConfigSections reads, ct)

ConfigLoadResult(SpectraConfig? Config, string? Error, IReadOnlyList<string> Warnings)
  missing file      read → Default, no warnings           write → Error = MissingConfigMessage
  malformed / type  both → Error = InvalidConfigPrefix + first error
  section missing   not in reads → filled with defaults, silently
                    in reads: read → defaults + warning "spectra.config.json has no '<s>' section; using the built-in defaults."
                              write → Error = "spectra.config.json is invalid: missing '<s>' section"
```

Declared reads per command:

| Command | Reads |
|---|---|
| `compile-extraction-prompt`, `docs apply-move`, `docs migrate-criteria`, `--import-criteria` | `None` |
| `compile-critic-prompt`, `compile-repair-prompt`, `compile-repair-batch` | `Source \| Tests` |
| `docs changed` | `Source` |
| `ingest-criteria`, `docs retire` | `Tests` |

## Criteria failure document (C4)

```text
{ "command": string, "success": false, "error_code": string|null, "errors": [string, …], "warnings": [string, …], …extras }
extras: ingest-criteria            → "outcome"
        compile-extraction-prompt  → "missing_input" (on a refusal, error_code "MISSING_INPUT")
```

Writer: `CriteriaCommandIO.WriteFailure(json, command, errorCode, message, warnings, extras)`, the only one.

## TestCaseLayout entry split (C5)

For each top-level entry, record the last scalar at any depth, and whether it is a keep-chomped block scalar (`+` in
its header). When it is, the entry's detachable trailing lines start at the first column-0 `#` line of the trailing
run, or there are none. Otherwise the 080 rule applies. The invariant
`Leading + Σ(Prelude + RawText) + Trailing == frontmatter` is unchanged.
