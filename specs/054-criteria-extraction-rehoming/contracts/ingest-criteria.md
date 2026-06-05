# CLI Contract: `spectra ai ingest-criteria`

The fail-loud, model-free boundary that ingests agent-extracted content, classifies it through the
reused-verbatim `CriteriaExtractor.ClassifyResponse`, and persists **only** genuine `Extracted`
results. Mirrors `spectra ai ingest-tests` (Spec 053). No model call.

## Synopsis
```
spectra ai ingest-criteria --doc <path> [--component <name>] [--from <file>] [--dry-run] [--output-format json|human]
```

## Inputs
| Option | Required | Notes |
|--------|----------|-------|
| `--doc, -d <path>` | yes | Source document the criteria belong to (sets `SourceDoc`, drives the `.criteria.yaml` filename + component slug) |
| `--component, -c <name>` | no | Component override; defaults to filename-derived slug |
| `--from <file>` | no | File with the agent's response; omit to read **stdin** |
| `--dry-run` | no | Classify + report, persist nothing |
| `--output-format` | no | `human` (default) or `json` |

## Behavior
1. Read agent content from `--from` or stdin.
2. `outcome = CriteriaExtractor.ClassifyResponse(content, docPath, component)` (reused verbatim).
3. Branch on outcome:
   - **`Extracted`** → assign/reuse IDs and persist via `CriteriaFileWriter` + criteria-index upsert
     (the exact write path `AnalyzeHandler` uses) unless `--dry-run`; exit `0`.
   - **`EmptyResponse`** → persist nothing; fail-loud; exit `5`.
   - **`ParseFailure`** → persist nothing; fail-loud; exit `6`.
4. Cache gate (FR-006): only `Extracted` updates `DocHash`/index. Non-cacheable outcomes leave the
   index byte-for-byte unchanged so a later run re-attempts.

## Exit codes
| Code | Meaning | Retry-skill action (Spec 055) |
|------|---------|-------------------------------|
| `0` | Criteria persisted (or dry-run preview) | done |
| `5` | `EmptyResponse` — agent returned nothing usable | re-prompt agent; re-ingest |
| `6` | `ParseFailure` — response present but unparseable | re-prompt with the parse error; re-ingest |
| `1` | Environment error (no config, file not found) | abort |

## Success payload (json mode, stdout)
```json
{"success": true, "outcome": "Extracted", "persisted": 7, "ids": ["AC-PAYMENT-001", "..."]}
```
## Failure payload (json mode, stderr)
```json
{"success": false, "outcome": "ParseFailure", "errors": ["Response did not contain a JSON array."]}
```

## Guarantees
- **No model call.** Pure classify + persist.
- **Fail-loud (FR-003).** Never throws on bad content; returns a typed non-zero exit.
- **No cache poisoning (FR-006).** `EmptyResponse`/`ParseFailure` write nothing.
- **Short-circuit (FR-003).** Empty/whitespace content classifies as `EmptyResponse` (a *response*
  is empty), distinct from the empty-*source* short-circuit handled at compile time.
