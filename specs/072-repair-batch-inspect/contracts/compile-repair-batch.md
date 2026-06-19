# Contract: `spectra ai compile-repair-batch`

**Spec 072 FR1** | New command | Model-free, deterministic

---

## Synopsis

```
spectra ai compile-repair-batch --suite <suite>
```

Reads all partial verdict JSONs for the suite. Filters to those whose `.md` file lacks a `grounding:` block (the resume checkpoint). Compiles a repair prompt for each via `RepairPromptCompiler.Compile()`. Emits a JSON array manifest to stdout.

---

## Options

| Option | Required | Description |
|--------|----------|-------------|
| `--suite` / `-s` | Yes | Suite name |

Global options `--output-format`, `--verbosity`, `--no-interaction` are accepted but ignored (output is always JSON array to stdout; this command has no interactive mode).

---

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success — manifest emitted (may be empty array if all tests already grounded) |
| 1 | Error — suite index not found, verdict file unreadable, or other fatal error (to stderr) |

---

## stdout (exit 0)

JSON array. One entry per ungrounded partial test:

```json
[
  {
    "id": "TC-101",
    "suite": "unit-converter",
    "file": "test-cases/unit-converter/TC-101.md",
    "source_refs": ["docs/unit-converter.md"],
    "repair_prompt": "# SPECTRA Test Repair\n\n..."
  }
]
```

Empty array `[]` when all partials are already grounded (nothing to repair).

---

## stderr (exit 1)

Plain-text error messages. Never JSON. Example: `Suite index not found: test-cases/unit-converter/_index.json`.

---

## Behavior

1. Resolves `testsDir` from `spectra.config.json` (or default `test-cases`).
2. Reads the suite index at `test-cases/{suite}/_index.json`.
3. Lists all `critic-verdict-TC-NNN.json` files in `.spectra/verdicts/`.
4. For each verdict file:
   a. Parses the verdict JSON via `VerdictIngestor.Classify()`.
   b. Skips non-partial verdicts (grounded or hallucinated).
   c. Looks up the test `.md` via the suite index; parses it with `TestCaseParser`.
   d. If `tc.Grounding is not null` → test is already grounded → skip (resume checkpoint).
   e. Otherwise: calls `RepairPromptCompiler.Compile(test, nonGroundedFindings, sourceDocs)`.
   f. Adds the entry to the manifest.
5. Emits the manifest as a JSON array to stdout.

---

## Guarantees

- **Never calls a model.** Pure file I/O + `RepairPromptCompiler.Compile()`.
- **Idempotent.** Re-running on a partially-repaired suite produces a manifest of only the remaining incomplete tests.
- **Resume-safe.** The filter in step 4d is the grounding-block checkpoint — tests with grounding blocks are always excluded.
- **Reuses `RepairPromptCompiler.Compile()`.** No duplicate prompt logic.

---

## Test contract

```
CompileRepairBatchCommandTests:
  EmptyManifest_WhenAllPartialTestsAlreadyGrounded → exit 0, stdout = "[]"
  ManifestContainsOnlyUngroundedPartials → exit 0, manifest ids match expected subset
  ManifestExcludesGroundedVerdicts → exit 0, grounded tests absent from manifest
  ManifestExcludesHallucinatedVerdicts → exit 0, hallucinated tests absent
  SuiteNotFound_Exits1 → exit 1
  NoVerdictFilesForSuite_EmptyManifest → exit 0, stdout = "[]"
  ManifestEntryHasFileField → entry.file == relative path to .md
  RepairPromptIsNonEmpty → entry.repair_prompt.Length > 0
```
