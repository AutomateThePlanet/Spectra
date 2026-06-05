# CLI Contract: `spectra ai compile-extraction-prompt`

Deterministic, model-free compiler that emits the acceptance-criteria extraction prompt for a single
document. Mirrors `spectra ai compile-prompt` (Spec 053). Writes nothing to disk. No model call.

## Synopsis
```
spectra ai compile-extraction-prompt --doc <path> [--component <name>] [--output-format json|human]
```

## Inputs
| Option | Required | Notes |
|--------|----------|-------|
| `--doc`, `-d <path>` | yes | Path (relative to cwd) of the source document to extract from |
| `--component, -c <name>` | no | Component hint; defaults to filename-derived slug if omitted |
| `--output-format` | no | `human` (default) or `json` (affects refusal payload only) |

## Behavior
1. If `--doc` is missing/whitespace → **refuse** (`document_path`).
2. Read the document. If it does not exist → environment error (exit 1).
3. If content is empty/whitespace → print a notice that this is an empty-source short-circuit
   (`Extracted, []`, no prompt needed) and exit `0` **without emitting a prompt** — preserves
   "no model turn" (FR-003). (Machine note: `{"short_circuit": true, "outcome": "Extracted"}` in json mode.)
4. Otherwise call `ExtractionPromptCompiler.Compile(docPath, content, component, templateLoader)`.
   - `Success` → write the compiled prompt to **stdout** (trailing newline ensured), exit `0`.
   - `MissingRequired` → emit refusal, exit `4`.

## Exit codes
| Code | Meaning |
|------|---------|
| `0` | Prompt emitted to stdout (or empty-source short-circuit) — nothing written to disk |
| `4` | Refuse-to-emit — required input missing (`missing_input` names it) |
| `1` | Environment error (no config, doc not found, read error) |

## Refusal payload (json mode, stderr)
```json
{"refused": true, "missing_input": "document_path", "message": "A source document path is required."}
```

## Determinism guarantee (FR-002)
Identical `(doc content, component, template files)` ⇒ byte-identical stdout. No timestamps, GUIDs,
or unordered enumeration in the emitted prompt.
