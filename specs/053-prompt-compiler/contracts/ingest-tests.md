# CLI Contract: `spectra ai ingest-tests`

Fail-loud boundary. Ingests agent-generated test content, validates it, and persists only when the whole batch is valid. The single reliability net now that the model call leaves C#.

## Synopsis

```
spectra ai ingest-tests <suite> [--from <file>] [--output-format json|human]
# content also accepted on stdin when --from is omitted
```

## Inputs

| Input | Source | Required | Notes |
|-------|--------|----------|-------|
| suite | positional | yes | Target suite to persist into. |
| content | `--from <file>` or stdin | yes | Agent's final message — expected to contain a JSON array of test objects. |

## Behavior

1. Read content from `--from` (or stdin).
2. `GeneratedTestIngestor.Ingest(content)`:
   - Extract the JSON array; parse strictly (**no truncation salvage**).
   - Parse each element into a `TestCase`.
   - `TestValidator.ValidateAll` the batch.
3. **All valid** ⇒ persist every test via `TestPersistenceService.PersistAsync` (write files + regenerate `_index.json`); exit `0`.
4. **Any failure** ⇒ persist nothing; emit `error_code` + specific messages; exit non-zero. The corpus and indexes are byte-for-byte unchanged.

## Error codes (machine-readable — the retry contract)

| `error_code` | Meaning | Exit |
|--------------|---------|------|
| `EMPTY_CONTENT` | Null/whitespace or no JSON array present. | 5 |
| `MALFORMED_JSON` | Not a parseable JSON array. | 5 |
| `TRUNCATED` | Array opened but never closed (token cut-off). | 5 |
| `NO_TESTS` | Array parsed but zero valid test objects. | 5 |
| `SCHEMA_INVALID` | One+ tests fail validation; `errors[]` echoes `ValidationError` codes/messages. | 6 |
| (success) | Persisted N tests. | 0 |

## Guarantees (testable)

- **Zero persistence on failure**: for every non-success outcome, `test-cases/` + all `_index.json` are unchanged.
- **No silent repair / no default substitution**: malformed or truncated input fails loud; it is never salvaged or defaulted.
- **Batch atomicity**: a single invalid test fails the whole batch — partial persistence never occurs.
- **Specific, actionable error**: `error_code` + `errors[]` are sufficient for a skill to instruct the agent to regenerate against the exact problem (FR-007 choreography input).
- **Happy path reuses the unchanged persist path**: valid content lands via `TestPersistenceService`, producing the same on-disk frontmatter as today (FR-008).
