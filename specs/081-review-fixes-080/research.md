# Research: Spec 081 (Part C review fixes)

## R1 — C1: repo-aware ownership and document lookup

**Decision**
- `CriteriaProvenance.OwnedBy(criteria, doc, repoRoot)` normalises both sides with
  `CriteriaPaths.NormalizeSourceDoc(value, repoRoot)`:
  - an absolute path inside the repository becomes repository-relative;
  - `\` and `./` are handled;
  - relative paths need no file on disk.
- A value that fails to normalise (empty, or outside the repository) belongs to no document (FR-002).
- The five callers pass their repository root: ingest, apply-move, retire, compile and `LeaveSharedFileAsync`.
- The same comparison is used for document lookup on the index, through one helper,
  `CriteriaProvenance.SameDocument(a, b, repoRoot)`. The helper is used by `CriteriaWorkspace.FindDocumentSource`,
  compile's `LoadExistingCriteriaAsync` and `DocsChangedHandler.FindDocumentSource`.

**Spec amendment (FR-003).** The lookup also covers the workspace (the write paths under `--dry-run`, which skips
migration) and `docs changed`. These are the same defect class: an absolute index `source_doc` makes the document
look `new` or unowned.

**Rationale.** Migration normalises per-criterion `source_doc` only in files it moves
(`CriteriaLayoutMigrator.cs:76`). Collided (shared) files are left as they are (`:93` normalises only the index
entry), so absolute or old-style per-criterion paths survive exactly where ownership matters.

**Alternatives.**
- Normalising per-criterion `source_doc` in migration: out of scope (spec, Out of Scope). It also would not help
  read-only paths.
- Keeping the lexical comparison and adding an absolute-path special case: this reinvents `NormalizeSourceDoc`.

**Test (fails on `1c8d8de`).**
- `SharedCriteriaFileTests`: A's criteria carry `source_doc` as `Path.Combine(root, "docs", "a", "login.md")`
  (absolute).
  - compile offers them;
  - ingest with their IDs exits 0, keeps them, and they leave the shared file;
  - retire of B (stored `./docs\b\login.md`) removes only B's.
- `CriteriaProvenanceTests`:
  - absolute-inside matches;
  - absolute-outside is owned by none;
  - relative still matches.

## R2 — C2: per-command config sections

**Decision**
- `ConfigLoader.LoadForReadAsync(repoRoot, ConfigSections reads, ct)` and `LoadForWriteAsync(...)`.
  `ConfigSections` is a `[Flags]` enum: `None`, `Source`, `Tests`.
- Load order:
  1. Read the file.
  2. Parse it into a `JsonNode`. A parse failure is malformed → `InvalidConfigPrefix + message`, as in 080.
  3. For each of `source`, `tests`, `ai` missing at the top level (case-insensitive, matching
     `PropertyNameCaseInsensitive`), add `{}`. Record the missing ones.
  4. Deserialize and validate through the existing `Load` path. Wrong types are still invalid (FR-005).
  5. Check the recorded missing sections against `reads`. For a write command, the first one gives
     `spectra.config.json is invalid: missing '<section>' section`. For a read command, each one adds a warning to
     `ConfigLoadResult.Warnings`: `spectra.config.json has no '<section>' section; using the built-in defaults.`
- `ai` is never in `reads`; nothing in scope reads it. `coverage` is not part of `ConfigSections` (FR-004c): it
  stays optional, with defaults and no warning, as today.
- Declared sections (spec, Assumptions):
  - critic, repair-prompt, repair-batch: `Source | Tests`;
  - `docs changed`: `Source`;
  - `ingest-criteria`, `docs retire`: `Tests`;
  - the rest: `None`.
- Warnings from a read command:
  - `docs changed` adds them to its JSON `warnings` and prints them as stderr lines;
  - the critic and repair commands print them as `Warning: …` on stderr;
  - `compile-extraction-prompt` reads no checked section, so it never gets one.

**Rationale.**
- `SpectraConfig.Source/Tests/Ai` are `required` members, so a missing section throws during deserialization,
  before `Validate` runs. That is why 080 rejects a config without `ai`.
- Filling `{}` works because every field of `SourceConfig`, `TestsConfig` and `AiConfig` has a default.
- It leaves the shared `SpectraConfig` model and every out-of-scope loader untouched. Dropping `required` would
  silently change `ai update`, `docs index` and ~30 other commands.

**Alternatives.**
- Making the members optional: behaviour change outside scope.
- A pre-scan with string search: fragile against comments and trailing commas, which the loader allows.
  `JsonNode.Parse` with the same `JsonDocumentOptions` (comments skipped, trailing commas allowed) matches the
  loader.

**Tests.**
- `ConfigLoaderEntryPointTests`. The 080 test `MissingAiSection_IsAnError_ForBothEntryPoints` encoded the defect and
  is replaced:
  - no `ai` → accepted by both entry points;
  - read, no `tests`, reads `Tests` → defaults plus one warning naming `tests`;
  - write, no `tests`, reads `Tests` → the missing-section error;
  - reads `None` with neither `source` nor `tests` → accepted, with no warnings;
  - wrong type (`"source": 5`) → invalid prefix.
- `ConfigLoadingConsistencyTests`:
  - all ten commands accept a config without `ai`;
  - the critic without `tests` warns and exits 0;
  - `ingest-criteria` and `retire` without `tests` exit 1 with the message and write nothing;
  - `apply-move`, `migrate-criteria` and `--import-criteria` without `source`/`tests` are not rejected for them;
  - `docs changed` without `source` has the warning in its JSON `warnings`.

## R3 — C3: normaliser tags and boolean attributes

**Decision**
- The element whitelist is 080's set plus `figure`, `figcaption`, `details`, `summary`, `section`, `article`,
  `aside`, `caption`, `col`, `colgroup`, `tfoot`, `center`, `font`, `picture`, `source`, `big`, `tt`, `wbr`, `samp`,
  `dfn`, `bdi`, `bdo`, `ruby`, `rt`, `rp`, `input`, `video`, `audio`, `iframe`.
- An attribute is either valued (as in 080) or a bare name from a fixed boolean list: `allowfullscreen`, `async`,
  `autofocus`, `autoplay`, `checked`, `compact`, `controls`, `default`, `defer`, `disabled`, `hidden`, `ismap`,
  `loop`, `multiple`, `muted`, `nohref`, `noresize`, `noshade`, `novalidate`, `nowrap`, `open`, `readonly`,
  `required`, `reversed`, `selected`. A bare name must be followed by whitespace, `/` or `>`, so `openx` is not
  `open`.
- Attributes may appear in any order and mix.

**Spec amendment (FR-007).** Remove `header`, `footer`, `nav`, `main`, `time`, `data`, `label` and `button` from the
list. In documentation they are also common placeholder words (`<data>`, `<time>`, `<label>`, `<header> row`).
Stripping them would reintroduce the B9 problem (`<Limit>`-style text lost). They are rare as real markup in Markdown
docs. `input` stays for the `<input disabled>` example.

**Rationale.** A fixed boolean list keeps `if a<b and c>d then` as text: `and` and `c` are not boolean attributes,
so `<b and c>` is not a tag.

**Test (fails on `1c8d8de`).** `TextNormalizerTests`:
- each added element, opening and closing, is stripped;
- `<td nowrap>`, `<details open>`, `<input disabled>`, `<video controls autoplay muted src="x">` are stripped;
- `<b openx>` and `<b and c>` are text;
- all 080 B9 rows unchanged.

## R4 — C4: one criteria failure shape

**Decision**
- `CriteriaCommandIO.WriteFailure(json, command, errorCode, message, warnings = null, extra = null)`. In JSON mode it
  writes `{command, success:false, error_code, errors:[message], warnings:[…]}` plus any `extra` fields (a small
  dictionary, flattened). Human mode is unchanged: `message [code]`, preceded by `Warning: …` lines when there are
  warnings.
- `ingest-criteria`'s classified failure (`IngestCriteriaCommand.cs:~160`) writes the same five fields plus
  `outcome`.
- `compile-extraction-prompt`'s `WriteFailure`/`WriteRefusal` delegate to it with `command:
  "compile-extraction-prompt"`. A refusal carries `error_code: "MISSING_INPUT"` and `missing_input`. `refused` and
  `message` are gone.
- The skill (`spectra-criteria.md`) Step 2a says "Exit 4 (refused) → show `errors[0]`".
- The 080 tests that asserted `message` or `refused` are updated. That is expected: those fields are exactly what
  FR-011 replaces.

**Rationale.**
- There is one writer for the shape, so it can't drift again.
- Nothing is released yet (2.6.0), so this is a correction, not a break.

**Test (fails on `1c8d8de`).** A theory over the five commands, each triggering a failure in JSON mode, checks:
- stderr parses as one document;
- it has all five common fields, and `warnings` is an array;
- the extra fields are present where expected.

Plus a skill content test: no `` `message` `` in the failure handling of Steps 1a, 1b, 2a and 2c.

## R5 — C5: keep-chomped block scalars

**Decision.** `TestCaseParser.CaptureFrontmatterEntries` records, per top-level entry, the last scalar event at any
depth. When that scalar is `Literal` or `Folded`:
- find its header by scanning from the scalar's start mark back to the `|`/`>` indicator on the same line;
- read the chomping indicator from the header characters (`[|>]` followed by `+`/`-` and an optional digit, in either
  order).

If it is keep (`+`), `TrimTrailingDetachedLines` detaches only from the first column-0 `#` line in the trailing
run onwards. Blank lines before that comment stay in the entry (they are the value's content), and so do blank lines
when there is no comment. Otherwise the 080 rule applies unchanged.

**Rationale.** YAML's keep chomping makes the trailing line breaks content, up to the first comment line (which ends
the scalar). The parse events expose the style (`Literal`/`Folded`), but not the chomping indicator, so the header
text is read from the source span. The start mark gives the position.

**Check at implementation.** Where the block scalar's `Start` mark points: at the indicator, or at the first content
line. The scan works either way (back to the indicator on the header line, or back from the content to the previous
line's indicator). A unit test pins it.

**Test (fails on `1c8d8de`).** `FrontmatterCommentPlacementTests`:
- (a) `description: |+` with content and two trailing blank lines, then `depends_on`. Remove `depends_on`: the
  re-parsed `Description` is unchanged, trailing newlines included.
- (b) `custom: notes: |+` with trailing blank lines, then `status`. Remove `status`: `Custom["notes"]` is unchanged.
- (c) A `|+` value, a blank line, `# c`, then the next key: `# c` stays above the next key.
- (d) Clip and strip values keep 080 behaviour; the existing tests stay green.
