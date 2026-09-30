# Contract: user-visible behaviour changes in Spec 082

## Config sections (A1, A3)

| Config | Criteria write commands¹ | Read commands that use it² |
|---|---|---|
| `coverage` missing or `null` | Exit 1: `spectra.config.json is invalid: missing 'coverage' section` (`CONFIG_SECTION_MISSING`); nothing written, also under `--dry-run` | Default criteria location + `Warning: spectra.config.json has no 'coverage' section; using the built-in defaults.` |
| `tests` missing or `null` | `ingest-criteria`, `docs retire`: run; default tests location for the referencing-test counts + one `Warning: … 'tests' …` | Critic/repair compilers: defaults + warning (unchanged) |
| `source` missing or `null` | not read | Critic/repair compilers, `docs changed`: defaults + warning (unchanged) |
| `ai` missing or `null` | accepted | accepted |
| `{}` for any section | present: defaults, no warning | same |

¹ `ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`, `ai analyze --import-criteria`.
² `compile-extraction-prompt` (coverage), `compile-critic-prompt` (source, tests, coverage),
`compile-repair-prompt`/`compile-repair-batch` (source, tests), `docs changed` (source, coverage).

**Changes from 081:**
- `coverage` is now required by the write commands.
- A missing `tests` section is a warning, not an error, for `ingest-criteria` and `docs retire`.
- A `null` section is treated as missing. It used to fail validation, and for `coverage` it crashed.

## Config error codes (A4)

In JSON mode, config failures carry a named `error_code`:

| Code | When |
|---|---|
| `CONFIG_MISSING` | No `spectra.config.json` where one is required (write commands) |
| `CONFIG_INVALID` | Not valid JSON, top-level not an object, or fails validation (wrong types) |
| `CONFIG_SECTION_MISSING` | A section the command requires is missing or `null`; the message names it |

Where the code appears:

| Command(s) | Document |
|---|---|
| The five criteria commands, `compile-extraction-prompt` | the shared failure JSON on stderr (`command`, `success`, `error_code`, `errors`, `warnings`) |
| `compile-critic-prompt`, `docs changed` | **new:** the shared failure JSON on stderr in JSON mode (was a plain text line) |
| `ai analyze --import-criteria` | `ErrorResult` on stdout, with a new `error_code` field |
| `compile-repair-prompt`, `compile-repair-batch` | no JSON mode: plain text line, unchanged |

Human-mode wording is unchanged everywhere.

## Placeholders (A2)

A known HTML element in angle brackets is markup only in HTML form:
- lowercase (`<details>`);
- closing (`</Summary>`);
- with attributes (`<Details open>`, `<INPUT type=checkbox>`);
- or opened and later closed (`<P>Para</P>`, `<Summary>x</Summary>`).

A bare capitalised or all-capitals word is text (`<Source>`, `<Summary>`, `<Section>`, `<Input>`, `<Details>`,
`<Caption>`, `<LIMIT>`, `<BR>`), so a change inside it is a real change.
