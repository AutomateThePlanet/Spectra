# Contract: `spectra ai compile-repair-prompt`

**Spec**: 071 | **Phase**: 2

## Purpose

Deterministic, model-free repair-prompt compiler. Reads the test artifact and its per-test verdict JSON, then emits a plain-text repair prompt to stdout that the in-session agent uses to produce a corrected version of the test. Mirrors `compile-critic-prompt` in structure; never calls a model.

## Invocation

```
spectra ai compile-repair-prompt --suite <suite> --test <id> [--from <verdict-file>]
```

### Options

| Option | Description |
|--------|-------------|
| `--suite <suite>` | Suite name; resolves test file via `_index.json` |
| `--test <id>` | Test ID to compile repair prompt for |
| `--from <file>` | Verdict JSON path (default: `.spectra/verdicts/critic-verdict-{id}.json`) |

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Prompt emitted to stdout |
| 1 | General error (suite/test not found, I/O error) |
| 4 | Refused — verdict file not found, or verdict is not `partial` (only partial tests need repair) |
| 5 | Verdict file empty |
| 6 | Verdict JSON parse failure |

## Behaviour

1. Loads suite index, resolves test file path for `--test {id}`.
2. Parses test `.md` file via `TestCaseParser`.
3. Reads and classifies verdict JSON via `VerdictIngestor.Classify()`.
4. If verdict is not `partial` → exit 4 (refused; repair is partial-only by design).
5. Extracts non-grounded findings: `findings[]` where `status` is `unverified` or `hallucinated`.
6. Loads source docs from the test's `source_refs` (same `LoadDocumentsFromRefsAsync` as `compile-critic-prompt`). If no `source_refs`, emits prompt without doc context (repair continues; agent has less grounding material).
7. Emits `RepairPromptCompiler.Compile(test, findings, docs)` → plain text on stdout.

## Output (stdout — plain text, no JSON envelope)

The emitted prompt contains:

```
# SPECTRA Test Repair

You are correcting a generated test that the critic found partially ungrounded.

## Test to repair: TC-113

**Title**: Verify file size conversion from bytes to kilobytes
**Preconditions**: ...
**Steps**:
1. ...
2. ...
**Expected Result**: ...

---

## Critic findings (claims that could not be grounded)

The critic identified these specific elements as unverified:

1. **Step 3** — "Conversion factor not verbatim in documentation"
   The test claims the conversion factor is X. The documentation below contains the actual value.

---

## Source documentation

### sizes.md
**Path**: docs/file-management/sizes.md

[content of doc, truncated at 8000 chars with marker]

---

## Instructions

Rewrite ONLY the elements listed in the critic findings above to make them traceable to the
documentation. Do not change: id, priority, component, tags, title (unless wrong), or any element
the critic found grounded. Return a JSON array containing the ONE corrected test, using the
same schema as the generation output. Preserve the test's id (TC-113).
```

## Invariants

- Emitted prompt is deterministic: identical inputs → identical output.
- ONLY emits for `partial` verdicts. For `grounded` or `hallucinated` → exit 4.
- Source docs resolved from test's own `source_refs` — no `--docs` override in repair (the test already carries its refs).
- No JSON envelope. Skill reads stdout directly and uses it as the repair prompt.
