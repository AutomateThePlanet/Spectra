# Research: Spec 082 (review fixes for 081)

## R1 — A1: `coverage` is required on criteria writes; `tests` only warns there

**Decision**
- `ConfigSections` gains `Coverage = 4`.
- `ConfigLoader.LoadForWriteAsync(repoRoot, ConfigSections requires, ConfigSections reads = None, ct)`:
  - a missing section in `requires` → `CONFIG_SECTION_MISSING`, with the message
    `spectra.config.json is invalid: missing '<section>' section`;
  - a missing section in `reads` only → defaults plus a warning, as read commands already do.
- `LoadForReadAsync(repoRoot, reads, ct)` keeps its shape. `Coverage` is now checkable there too.
- The pre-scan list becomes `source`, `tests`, `coverage`, `ai`. Each missing or null one is filled with `{}`
  (see R3), and `ai` still maps to `None`.
- Declarations:

  | Command | requires | reads (warn) |
  |---|---|---|
  | `ingest-criteria`, `docs retire` | `Coverage` | `Tests` |
  | `docs apply-move`, `docs migrate-criteria`, `--import-criteria` | `Coverage` | — |
  | `compile-extraction-prompt` | — | `Coverage` |
  | `compile-critic-prompt` | — | `Source \| Tests \| Coverage` |
  | `compile-repair-prompt`, `compile-repair-batch` | — | `Source \| Tests` |
  | `docs changed` | — | `Source \| Coverage` |

**Spec amendment (FR-003).** `compile-critic-prompt` also reads `coverage`: `CriteriaContextLoader` uses
`config.Coverage.CriteriaDir` for the criteria context. It therefore declares it, and warns when it is missing.
This follows the spec's own rule ("each command declares the sections it reads").

**Where write-command warnings go.**
- `ingest-criteria` puts the config warnings first in its result's `warnings`. These show up in the JSON `warnings`
  field and as `Warning:` lines on stderr, exactly as the reference-counter warnings do today.
- `docs retire` has no `warnings` field in its success JSON, so it prints them as `Warning:` lines on stderr in both
  modes, and passes them to `WriteFailure` if it fails later.
- The refusal happens before the lock, migration and any write, so `--dry-run` refuses the same way (FR-004).

**Rationale.** Criteria writes land in `coverage.criteria_dir` / `criteria_file`, so the location a user never chose
is `coverage`. `tests` feeds only the informational referencing-test counts. That is the opposite of what 081 guarded.

**Tests (fail on `7df3fef`).**
- `ConfigLoaderEntryPointTests`:
  - a write load requiring `Coverage` without it → `CONFIG_SECTION_MISSING` naming `coverage`;
  - a write load reading `Tests` without it → success plus one warning;
  - a read load reading `Coverage` without it → defaults plus a warning.
  - The 081 case `Write_MissingSectionItReads_IsAnError_NamingTheSection` (`tests` on a write) encoded the defect and
    is replaced.
- `ConfigLoadingConsistencyTests`:
  - all 5 write commands without `coverage` → exit 1 naming `coverage`, snapshot unchanged, with and without
    `--dry-run`;
  - `ingest-criteria` and `retire` without `tests` → exit 0 and exactly one `tests` warning. These replace 081's
    `WriteCommand_MissingTests_Exits1…`.
  - `compile-extraction-prompt` and `docs changed` without `coverage` → exit 0 plus a warning.

## R2 — A2: only HTML-form tags are markup

**Decision.** An angle-bracket span of a known element is markup in four cases:
- (a) a closing tag `</name>`, any case;
- (b) an opening or self-closing tag with at least one attribute (valued or bare boolean), any case;
- (c) a bare opening or self-closing tag with an all-lowercase name;
- (d) a bare opening tag, any case, followed later in the text by a matching closing tag of the same name
  (case-insensitive).

Anything else stays text. That covers `<Source>`, `<Summary>`, `<Section>`, `<Input>`, `<Details>` and `<Caption>`
when not closed, and all-capitals bare words such as `<LIMIT>` and `<BR>`.

**Implementation.** One `GeneratedRegex` with four alternatives. (a) and (b) use the case-insensitive name group. (c)
uses a case-sensitive `(?-i:…)` lowercase group. (d) captures the name and uses a lookahead
`(?=[\s\S]*?</\k<n>\s*>)` under `IgnoreCase`. The element and boolean-attribute lists are unchanged from 081.

**Spec amendment (FR-005).** Add (d). Without it, 080's B9 case `<P>Para</P>` → `para` would change, and FR-006
forbids that. With (d), `<Summary>x</Summary>` and `<P>Para</P>` are markup, while a lone `<Summary>` placeholder is
text.

**Rationale.**
- Placeholders are written capitalised and bare.
- Real HTML is lowercase, has attributes, or comes in open/close pairs.
- The spec's asymmetry argument holds: calling real markup text only costs a spurious "modified"; calling a
  placeholder markup hides a real change.

**Tests (fail on `7df3fef`).** `TextNormalizerTests`:
- each of the 6 named placeholders swapped for another gives different keys (`Enter <Summary> and press Save` vs
  `<Details>`);
- `<Source>` vs `<Target>` gives different keys;
- `<LIMIT>` and `<BR>` stay text.

Guards that must stay green:
- `<details open>`, `</Summary>`, `<Details open>x`, `<P>Para</P>`, `<Summary>x</Summary>` are markup;
- every 080 and 081 row is unchanged.

## R3 — A3: a `null` section is a missing section

**Decision.** In the pre-scan, a top-level key whose value is JSON `null` counts as missing. It is replaced by `{}`
and recorded, for all four checked sections. That covers `"coverage": null`, which the current loader passes through
as a null `Coverage` property, leading to a null reference later. A top-level `null` document, or an empty file,
stays `CONFIG_INVALID`: the root is not an object, or the JSON fails to parse.

**Rationale.** The rule is simple and uniform, and it matches the spec's edge cases (`{}` is present; `null` is
absent).

**Tests (fail on `7df3fef`).** `ConfigLoaderEntryPointTests` theory over `source`, `tests`, `coverage` and `ai` set
to `null`, comparing the result with the missing-section result (config, error, error code, warnings). Plus a
`ConfigLoadingConsistencyTests` case: `"ai": null` accepted by `docs changed`, and `"coverage": null` refused by
`ingest-criteria`.

## R4 — A4: named config error codes

**Decision**
- `ConfigLoadResult` gains `ErrorCode`: `CONFIG_MISSING`, `CONFIG_INVALID` or `CONFIG_SECTION_MISSING`.
- `CriteriaCommandIO.LoadConfigAsync` returns it, and every caller passes it to `WriteFailure` instead of `null`:
  `ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`.
- `compile-extraction-prompt` passes `loaded.ErrorCode` to its failure writer.
- `--import-criteria`: `ErrorResult` gains `[JsonPropertyName("error_code")] string? ErrorCode`, omitted when null
  (the writer already ignores nulls), and it is set on the config failure.
- `compile-critic-prompt` and `docs changed` have a JSON mode but printed config errors as text. In JSON mode they now
  emit the shared failure document (`CriteriaCommandIO.WriteFailureJson`, with `command` `compile-critic-prompt` or
  `docs-changed`) carrying the code. Human mode is unchanged (FR-011).
- The `spectra-criteria` skill, Steps 1a, 1b, 2a and 2c, names the three codes and drops "`null` for a config problem".

**Spec amendment (FR-008/009).** `compile-repair-prompt` and `compile-repair-batch` have no JSON mode: they always
print a plain prompt or manifest and ignore `--output-format`. So no failure document can carry a code. Their config
errors keep the plain-text line (FR-011). Giving them a JSON mode would be a feature, which this spec excludes.

**Rationale.** A code on every config failure lets consumers stop inferring from `null`. Using one writer per shape
keeps the codes from drifting.

**Tests (fail on `7df3fef`).**
- `ConfigLoadingConsistencyTests`, over the JSON-mode commands:
  - no file → `CONFIG_MISSING` (write commands);
  - malformed → `CONFIG_INVALID` for all JSON-mode in-scope commands: the five criteria commands,
    `compile-extraction-prompt`, `compile-critic-prompt`, `docs changed` and `--import-criteria` (stdout);
  - a missing required section → `CONFIG_SECTION_MISSING`.
- `SkillsManifestTests`: the skill names the three codes and has no "`null` for a config".
