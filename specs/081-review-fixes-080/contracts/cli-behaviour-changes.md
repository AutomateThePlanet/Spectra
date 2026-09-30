# Contract: user-visible behaviour changes in Spec 081

## Config sections (C2)

| Config | Command doesn't read the section | Read command reads it | Write command reads it |
|---|---|---|---|
| Section missing (`source` / `tests` / `ai`) | Accepted, no message | Built-in defaults + `Warning: spectra.config.json has no '<s>' section; using the built-in defaults.` on stderr (and in `docs changed`'s JSON `warnings`) | Exit 1: `spectra.config.json is invalid: missing '<s>' section`, nothing written |
| `coverage` missing | Defaults, no message (unchanged) | same | same |
| Malformed JSON or a wrong type | Exit 1: `spectra.config.json is invalid: …` (unchanged) | same | same |
| No file | Read: defaults · Write: exit 1 `No spectra.config.json found. Run 'spectra init' first.` (unchanged) | | |

**Change from 080:** a config without `ai` is now accepted by all ten commands.

## Criteria failure JSON (C4)

In JSON mode, every failure of `compile-extraction-prompt`, `ingest-criteria`, `docs retire`, `docs apply-move` and
`docs migrate-criteria` is exactly one JSON document on stderr:

```json
{ "command": "docs-retire", "success": false, "error_code": "NOT_INDEXED", "errors": ["'docs/x.md' is not an indexed document source."], "warnings": [] }
{ "command": "ingest-criteria", "success": false, "error_code": "INVALID_CRITERION_ID", "errors": ["…"], "warnings": [], "outcome": "Rejected" }
{ "command": "compile-extraction-prompt", "success": false, "error_code": "MISSING_INPUT", "errors": ["A source document path is required (--doc)."], "warnings": [], "missing_input": "document_path" }
{ "command": "compile-extraction-prompt", "success": false, "error_code": "DOCUMENT_NOT_FOUND", "errors": ["Document not found: docs/x.md"], "warnings": [] }
```

**Change from 080:** `compile-extraction-prompt`'s `refused` and `message` fields are replaced by `success`,
`error_code` and `errors`. Every other command gains `warnings` (an empty array unless there is something to warn
about). Exit codes and human-mode output are unchanged.

## Ownership (C1)

A criterion belongs to a document when the repository-relative forms of its `source_doc` and the document are the
same. That covers absolute paths inside the repository, `\` and `./`. The same rule finds a document's index entry. An
out-of-repository or empty `source_doc` belongs to no document.

## Normaliser (C3)

- Tags of the listed elements are stripped, with valued attributes and bare boolean attributes (`nowrap`, `open`,
  `disabled`, …) in any mix. HTML comments are stripped.
- Anything else in angle brackets stays text: `total<limit and qty>5`, `<Limit>`, `a<b and c>d`, and placeholder
  words such as `<data>`, `<time>`, `<label>`.

## Frontmatter rewrites (C5)

The trailing blank lines of a keep-chomped block value (`|+`, `>+`, at any depth) stay with it when the key below is
changed or removed. A column-0 comment after such a value still belongs to the key below.
