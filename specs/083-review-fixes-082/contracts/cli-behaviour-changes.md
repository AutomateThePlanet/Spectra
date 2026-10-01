# Contract: user-visible behaviour changes in Spec 083

## The config rule (replaces 081 C2 and 082 A1/A3)

| Situation | Read commands¹ | Write commands² |
|---|---|---|
| No `spectra.config.json` | built-in defaults, no warning | exit 1, `CONFIG_MISSING` |
| Not valid JSON, not an object, unreadable, or an exact duplicate key | exit 1, `CONFIG_INVALID` | exit 1, `CONFIG_INVALID` |
| Two top-level keys that differ only in case | exit 1, `CONFIG_INVALID` naming the key | exit 1, `CONFIG_INVALID` naming the key |
| `source` or `tests` missing or `null`, and the command reads it | defaults + one warning naming the section | defaults + one warning naming the section |
| `coverage` missing or `null` | defaults, no warning | defaults, no warning |
| Any section the command doesn't read, missing or `null` | ignored | ignored |

¹ `compile-extraction-prompt`, `compile-critic-prompt`, `compile-repair-prompt`, `compile-repair-batch`,
`docs changed`. ² `ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`,
`ai analyze --import-criteria`.

Which commands can warn:
- `tests`: `ingest-criteria`, `docs retire`, and the critic/repair compilers;
- `source`: `docs changed`, and the critic/repair compilers.

A warning appears in `warnings` where the command's JSON has that field, and otherwise as a `Warning:` line. It never
changes the exit code.

**Changes from 082:**
- No section is ever required. `CONFIG_SECTION_MISSING` no longer exists.
- A missing `coverage` no longer refuses writes and no longer warns.
- Case-variant and exact duplicate top-level keys are `CONFIG_INVALID`; an exact duplicate used to crash.

## `spectra init`

The written `spectra.config.json` gains:

```json
"coverage": {
  "criteria_dir": "docs/criteria",
  "criteria_file": "docs/criteria/_criteria_index.yaml"
}
```

Existing workspaces are not rewritten and need no change.

## Criterion text comparison

A known HTML element in angle brackets is markup only as:
- a closing tag;
- a tag with attributes;
- a self-closing bare tag (`<br/>`);
- a lowercase `<br>`, `<hr>` or `<wbr>`;
- or an opening tag closed later with exactly the same spelling (`<p>x</p>`, `<P>x</P>`).

Everything else is text:
- `<source>`, `<input>`, `<summary>` with no matching close;
- `<Summary>`, `<Source>`;
- `<LIMIT>`, `<BR>`;
- `<Summary>` when only `</summary>` follows.

**Residual:** a lowercase placeholder whose exact lowercase closing tag appears later in the same text counts as
markup.
