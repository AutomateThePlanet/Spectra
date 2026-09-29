# Contract: `spectra ai compile-extraction-prompt` existing criteria (Spec 079)

```bash
spectra ai compile-extraction-prompt --doc <path> [--component <c>] [--output-format json]
```

- **Existing criteria.** The command reads (never writes) the criteria index. It locates this doc's criteria through the
  index entry for the normalised `source_doc`, using its `file`. This works in both the old and the new layout.
- **Old layout.** The command prints one warning to stderr and moves nothing.
- **`existing_criteria` placeholder.** It is filled with a compact JSON array, one object per criterion in file order, with
  the keys in the fixed order `id`, `text`, `source_section` (`source_section` is omitted when null). The array is
  serialized with `System.Text.Json`, without indentation and with relaxed escaping, so identical inputs give identical
  bytes. With no existing criteria it is `""`, and the template's block is skipped.

Built-in template addition (inside `{{#if existing_criteria}}`):

```text
This document already has acceptance criteria. For every criterion you extract that expresses the SAME requirement as one
of these (even if you word it differently), return that criterion's "id" unchanged. Omit "id" for new requirements. Never
invent an id, and never use the same id twice.

Existing criteria:
{{existing_criteria}}
```

The response example gains one item with an `"id"` field.

## Custom templates

An override in `.spectra/prompts/criteria-extraction.md` that doesn't reference `existing_criteria` still compiles. The
command prints a stderr warning naming `spectra prompts reset criteria-extraction`.
