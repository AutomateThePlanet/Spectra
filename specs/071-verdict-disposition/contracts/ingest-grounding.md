# Contract: `spectra ai ingest-grounding`

**Spec**: 071 | **Phase**: 1

## Purpose

Reads a per-test critic verdict JSON and writes the condensed `grounding:` frontmatter block into the named test's `.md` file. This activates the existing (previously dead) `TestFileWriter` grounding code path. The command is deterministic and model-free; it never calls a model.

## Invocation

```
spectra ai ingest-grounding --suite <suite> --test <id> [--from <file>] [--repaired] [--repair-attempts <n>]
```

### Options

| Option | Description |
|--------|-------------|
| `--suite <suite>` | Suite name; resolves test file via `_index.json` |
| `--test <id>` | Test ID to write grounding for (e.g., TC-113) |
| `--from <file>` | Path to per-test verdict JSON (default: `.spectra/verdicts/critic-verdict-{id}.json`) |
| `--repaired` | Mark test as repaired (sets `repaired: true` in block) |
| `--repair-attempts <n>` | Number of repair attempts performed (default: 0) |

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Grounding block written successfully |
| 1 | General error (suite/test not found, parse failure, I/O error) |
| 4 | Refused — test has `verdict: hallucinated`; grounding write not allowed for dropped tests |
| 5 | Verdict JSON missing or empty |
| 6 | Verdict JSON parse failure (missing verdict/score) |

## Behaviour

1. Loads `spectra.config.json` to resolve `tests.dir` (default: `test-cases`).
2. Reads `test-cases/{suite}/_index.json` to resolve the test file path for `--test {id}`.
3. Fails loud (exit 1) if suite, index, or test file not found.
4. Reads the verdict JSON from `--from` (or the default per-test path).
5. Classifies via `VerdictIngestor.Classify()` — reuses existing classification logic.
6. If verdict is `hallucinated` → exit 4 (refuse; caller should have called `record-drop` + delete instead).
7. Builds `GroundingMetadata` from the verdict:
   - `Verdict`, `Score`, `Critic` (from `critic_model` in verdict JSON)
   - `Generator`: extracted from test frontmatter if present, otherwise `"claude-code-session"`
   - `VerifiedAt`: current UTC timestamp
   - `FlaggedForReview: true` when verdict is `Partial` and repair did not succeed
   - `RepairAttempts`: from `--repair-attempts` option
   - `Repaired`: from `--repaired` flag
   - `CondensedFindings`: from verdict `findings[]` where `status` is `unverified` or `hallucinated` — element + reason only
8. Reads existing test `.md`, parses with `TestCaseParser`, creates updated `TestCase` with `Grounding` set.
9. Writes updated test file via `TestFileWriter.WriteAsync`. The grounding block is added or overwritten.
10. Outputs result JSON / human text.

## Output (JSON mode)

```json
{
  "success": true,
  "id": "TC-113",
  "suite": "file-management",
  "verdict": "partial",
  "score": 0.72,
  "flagged_for_review": true,
  "repair_attempts": 1
}
```

## Invariants

- The test's non-grounding fields (id, title, priority, component, tags, steps, etc.) are NEVER changed by this command — it only adds/overwrites the `grounding:` frontmatter block.
- `_index.json` is NOT modified (the test already exists; no entry added or removed).
- If the test file already had a grounding block, it is overwritten with the new verdict (supports re-verification).
