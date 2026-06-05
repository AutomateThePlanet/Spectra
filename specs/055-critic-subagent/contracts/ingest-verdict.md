# Contract: `spectra ai ingest-verdict`

The **fail-loud** verdict-ingest boundary. Reads an agent-produced critic JSON response, classifies
it into a typed outcome, and reports the verdict + gate decision — **never** coercing a missing
`verdict`/`score` into a soft `Partial` / `0.5` pass, and **never** throwing. Mirrors `ingest-tests`
(053) and `ingest-criteria` (054).

## Synopsis

```
spectra ai ingest-verdict [--from <file>] [--output-format human|json]
```

## Inputs

| Option | Required | Description |
|--------|----------|-------------|
| `--from` | no | File containing the critic's JSON response. Omit to read stdin. |
| `--output-format` | no | `human` (default) or `json`. |

## Behavior

1. Read the critic response from `--from` or stdin.
2. `VerdictIngestor.Classify(json)` → `VerdictIngestResult`:
   - empty/whitespace → `EmptyResponse` (damage),
   - missing/unparseable `verdict` or `score`, or non-JSON → `ParseFailure` (damage, specific
     error),
   - well-formed `{ verdict, score, findings }` → `Verdict` carrying a `VerificationResult`.
3. On `Verdict`, report the verdict, score, and the **gate decision** (`drop` iff `hallucinated`;
   otherwise `pass`). On damage, report the specific error and which field was missing/unparseable.

The verdict stays advisory-gating (a `hallucinated` verdict's `drop` is the unchanged gate); damage
fails loud (FR-005, FR-006).

## Exit codes

| Code | Condition | Maps to |
|------|-----------|---------|
| 0 | `Verdict` — classified a well-formed verdict (any of grounded/partial/hallucinated). | FR-005 |
| 5 | `EmptyResponse` — empty/whitespace critic response. | FR-006 |
| 6 | `ParseFailure` — missing/unparseable `verdict` or `score`, or non-JSON. | FR-006 |
| 1 | Environment error (e.g. no `spectra.config.json`). | — |

Exit 5 vs. 6 keeps **damage** distinct at the process boundary; a critic *call failure*
(exception/timeout) is a separate runtime concern that yields an `Unverified`-style result on the
retained in-process path and is **not** routed through this command — preserving the FR-007
failure-vs-parse-miss distinction.

## Output (json)

```jsonc
// exit 0 — a verdict (gate decision included)
{ "outcome": "Verdict", "verdict": "hallucinated", "score": 0.1, "drop": true }
{ "outcome": "Verdict", "verdict": "grounded", "score": 0.95, "drop": false }

// exit 6 — damage, fail loud (no soft default)
{ "outcome": "ParseFailure", "errors": ["critic response missing required 'verdict' field"] }

// exit 5 — empty
{ "outcome": "EmptyResponse", "errors": ["critic returned no content"] }
```

## Examples

```bash
# Ingest a hallucinated verdict → exit 0, drop=true
echo '{"verdict":"hallucinated","score":0.1,"findings":[]}' | spectra ai ingest-verdict
echo $?   # 0

# Missing verdict field → fail loud, exit 6 (NOT a Partial/0.5 soft pass)
echo '{"score":0.5}' | spectra ai ingest-verdict
echo $?   # 6

# Empty response → exit 5
printf '' | spectra ai ingest-verdict
echo $?   # 5
```

## Guarantees

- **Never throws**: every input maps to exactly one typed outcome.
- **No soft default**: a missing/unparseable `verdict` or `score` is `ParseFailure`, never
  `Partial` / `0.5` (FR-006).
- **Gating unchanged**: only `hallucinated` drops; grounded/partial pass (FR-005).
