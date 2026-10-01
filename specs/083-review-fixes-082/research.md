# Research: Spec 083 (review fixes for 082)

## R0 — What real workspaces contain (the check 080–082 skipped)

**Findings**
- `spectra init` writes the config from the embedded resource `Spectra.CLI.Templates.spectra.config.json`
  (`InitHandler.CreateConfigFileAsync` → `GetEmbeddedTemplate`). That file's top-level sections are `source`, `tests`,
  `ai`, `generation`, `validation`, `dashboard`, `execution`, `bug_tracking`, `git`, `testimize` and `debug`. It has
  **no `coverage`**.
- `ConfigLoader.GenerateDefaultConfig()` serialises `SpectraConfig.Default`, which does include `coverage`. It is
  only `GetEmbeddedTemplate`'s last-resort fallback. 081 and 082 checked against that, not against what `init`
  writes.
- A pre-083 workspace therefore has the template's sections, and no `coverage` unless the user added one.
- `InitHandler(logger, dir).HandleAsync(force: false)` is non-interactive by default (`interactive = false`), makes
  no network calls, and is already used by the init tests. T2 and T3 can drive the real template through it.

## R1 — One config rule (FR-001–FR-005)

**Decision**
- `ConfigSections` becomes `{ None, Source, Tests }`. `Coverage` is removed.
- The entry points are `LoadForReadAsync(repoRoot, ConfigSections reads, ct)` and
  `LoadForWriteAsync(repoRoot, ConfigSections reads, ct)`. The `requires` parameter is removed.
- The only difference between the two remains the missing-file branch: read → `SpectraConfig.Default`, write →
  `CONFIG_MISSING`.
- **Pre-scan**:
  - Parse the file. `JsonException` or `ArgumentException` → `CONFIG_INVALID`.
  - A root that is not an object → `CONFIG_INVALID`.
  - Case-variant duplicate top-level keys → `CONFIG_INVALID` (R2).
  - Then `source`, `tests`, `coverage` and `ai`, when absent or `null`, are replaced with `{}`. This keeps the typed
    `required` members satisfied and a `null` `coverage` from crashing.
  - A warning is added for each replaced section that is in `reads` (so only `source` and `tests` can warn).
  - Then the existing `Load` / `Validate` runs; a type error there → `CONFIG_INVALID`.
- `ErrorConfigSectionMissing` and every path producing it are removed. `ErrorConfigMissing` and `ErrorConfigInvalid`
  stay.
- Declarations (FR-003):
  - `Tests`: `ingest-criteria`, `docs retire`;
  - `Source | Tests`: the critic, repair-prompt and repair-batch compilers;
  - `Source`: `docs changed`;
  - `None`: `docs apply-move`, `docs migrate-criteria`, `--import-criteria`, `compile-extraction-prompt`.
- `CriteriaCommandIO.LoadConfigAsync(repoRoot, reads, ct)` drops its `requires` parameter.
- The skill's "`CONFIG_MISSING` / `CONFIG_INVALID` / `CONFIG_SECTION_MISSING`" becomes "`CONFIG_MISSING` /
  `CONFIG_INVALID`" at all four places.

**Rationale**
- Writing into the wrong folder is already prevented by requiring the config file for write commands.
- A required section adds nothing, and it is exactly what broke every init-created workspace.
- Filling `{}` is the one mechanism already proven in 081 and 082. It leaves `SpectraConfig` untouched for the
  commands outside this spec.

**Alternatives**
- Keep `coverage` required and only fix the template. This still breaks every pre-083 workspace, which T3 forbids.
- Warn on a missing `coverage`. This warns on every run in every pre-083 workspace for a section whose defaults are
  exactly what `init` now writes, and FR-001 forbids it.

## R2 — Duplicate and case-variant keys (FR-008)

**Finding (probed on net8.0).**
- `JsonNode.Parse("{\"a\":1,\"a\":2}")` throws `ArgumentException` ("An item with the same key has already been
  added") when the object is materialised. Today's pre-scan catches only `JsonException`, so a config with an exact
  duplicate key **crashes** the command.
- `{"coverage":{},"Coverage":null}` parses into two keys. The typed deserializer (`PropertyNameCaseInsensitive`) then
  silently keeps the last one.

**Decision**
- Materialise the key list inside the `try` (`root.Select(kv => kv.Key).ToList()`), and catch `ArgumentException`
  alongside `JsonException`. Both give `CONFIG_INVALID` with the parser's message.
- Then group the keys case-insensitively. The first group with more than one key gives `CONFIG_INVALID` with
  `duplicate top-level key '<k1>' / '<k2>' (keys may not differ only in case)`, which names every spelling.
- The 082 A3 branch "a present non-null match wins among case-variant keys" becomes unreachable and is removed.

**Tests (fail on `3594d62`).**
- `"tests"` + `"Tests": null`, and `"generation"` + `"Generation"`, each for one read command and one write command:
  exit 1, `CONFIG_INVALID`, the message names the key.
- In the loader: exact duplicate `{"a":1,"a":2}` → `CONFIG_INVALID`, not an exception.

## R3 — Init template gets `coverage` (FR-006, FR-007)

**Decision.** Add this to `src/Spectra.CLI/Templates/spectra.config.json`, directly after `tests`:

```json
"coverage": {
  "criteria_dir": "docs/criteria",
  "criteria_file": "docs/criteria/_criteria_index.yaml"
}
```

Only these two keys go in. The other `CoverageConfig` fields keep their built-in defaults, and the template shows
only where criteria go. Existing workspaces are not touched; `init` without `--force` does not rewrite an existing
config.

**Test.** `InitCommandTests`: after `init`, the config has `coverage.criteria_dir == "docs/criteria"` and
`coverage.criteria_file == "docs/criteria/_criteria_index.yaml"`. This fails on `3594d62`.

## R4 — HTML forms (FR-009–FR-013)

**Decision.** One `GeneratedRegex` with five alternatives, applied through a match evaluator:

1. `</(?:NAMES)\s*>`
2. `<(?:NAMES)(?:ATTR)+\s*/?>`
3. `<(?:NAMES)\s*/>`
4. `<(?-i:br|hr|wbr)\s*>`
5. `<(?<bare>NAMES)\s*>`, a candidate only

The evaluator strips (1)–(4) outright. For (5) it strips only if `</` + the captured spelling + `\s*>` occurs later
in the text, matched **case-sensitively**; otherwise it returns the match unchanged, so the text stays. The element
and boolean-attribute lists are unchanged.

**Rationale.**
- Rule 5 needs an exact-spelling look-ahead. Doing it in code (a case-sensitive search from the match end) is clearer
  and more certain than relying on a backreference's case handling under `IgnoreCase`.
- Alternatives are tried in order, so `<br>` hits (4) before (5), and `<BR>` falls to (5) and stays text unless
  `</BR>` follows.

**Checked.** A model of the 082 rule and of this rule was run over all 71 distinct angle-bracket inputs in the test
suites: **0 differences**. It was also run over the FR-012 examples: every one gives the listed result. So no
existing test expectation changes.

**Residual (documented in the XML summary and `cli-reference.md`).** A lowercase placeholder whose exact lowercase
closing tag appears later in the same text is treated as markup.

## R5 — The guard tests (FR-014–FR-018)

**T1 — config matrix**, `ConfigRuleMatrixTests` (CLI, `CriteriaCommands` collection).
- One `TheoryData` row per FR-001 situation, run against `compile-critic-prompt --suite s --test TC-1` (read; reads
  `source` and `tests`) and `ingest-criteria` (write; reads `tests`).
- Each row states the expected exit code, the `error_code` (or none), the config-warning count for each command, and,
  for write rows that succeed, that the criteria file exists under `docs/criteria/`.
- Config warnings are counted as the lines or entries containing `spectra.config.json has no`, in the command's
  warning channel: stderr lines for the critic, and the JSON `warnings` array for ingest.
- Rows:

  | Row | Critic | Ingest |
  |---|---|---|
  | No file | exit 0, 0 warnings | exit 1, `CONFIG_MISSING` |
  | Malformed | exit 1, `CONFIG_INVALID` | exit 1, `CONFIG_INVALID` |
  | Root not an object (`[]`, `null`) | exit 1, `CONFIG_INVALID` | exit 1, `CONFIG_INVALID` |
  | Case-variant duplicate (`tests`/`Tests`) | exit 1, `CONFIG_INVALID` | exit 1, `CONFIG_INVALID` |
  | `source` missing, or `null` | 1 warning | 0 warnings |
  | `tests` missing, or `null` | 1 warning | 1 warning |
  | `coverage` missing, or `null` | 0 warnings | 0 warnings, writes to `docs/criteria` |
  | `ai` null, `generation` missing | 0 warnings | 0 warnings |

  An unreadable file (Windows exclusive lock) is covered by a Windows-only row.

**T2 — real template**, `InitTemplateWorkspaceTests.RealTemplate_…`.
1. Run `InitHandler.HandleAsync(force: false)` in an empty temp directory.
2. Add `docs/a.md`, `docs/gone.md` and `docs/move.md`.
3. Run `docs changed`, `compile-extraction-prompt --doc docs/a.md`, and `ingest-criteria` for each doc with fixture
   responses.
4. Delete `gone.md` and run `docs retire`.
5. Move `move.md` to `docs/moved.md` and run `docs apply-move`.

All exit 0, with zero config-warning lines or entries, and the criteria files are under `docs/criteria/`.

**T3 — pre-083 workspace.** The same run after removing `coverage` from the written config with `JsonNode`, which
reproduces a pre-083 template. Same assertions.

**T4 — normaliser table**, `TextNormalizerTests.HtmlForms_DecisionTable`. One row per FR-012 example in both lists,
with the expected key, plus the documented residual (`Enter <summary> … </summary>` → markup). All existing rows are
kept as they are; R4 shows they don't change.

**T5 — duplicate keys** is in T1's matrix (the `tests` row) plus a `generation`/`Generation` row, and the loader-level
exact-duplicate test from R2.

**Replaced 082 tests (FR-019)**, because they assert the removed rule:
- `ConfigLoaderEntryPointTests`:
  - `Write_MissingRequiredSection_IsAnError_NamingTheSection` → removed;
  - `Read_MissingCoverage_UsesDefaults_AndWarns` → becomes "no warning";
  - the `CONFIG_SECTION_MISSING` part of `ErrorCodes_NameEachConfigProblem` → removed;
  - `NullSection_BehavesLikeAMissingSection` and `Write_MissingSectionItOnlyReads_Warns` → rewritten without
    `requires`.
- `ConfigLoadingConsistencyTests`:
  - `WriteCommand_MissingCoverage_Exits1_AndWritesNothing` → becomes "succeeds, writes to `docs/criteria`, no
    warning";
  - `ReadCommand_MissingCoverage_UsesDefaults_AndWarns` → becomes "no warning";
  - `NullCoverage_RefusesAWrite_NamingTheSection` → becomes "succeeds";
  - the `nocoverage` rows of `ConfigErrorCodes_JsonMode` → removed.
- `SkillsManifestTests.CriteriaSkill_NamesConfigErrorCodes` → asserts `CONFIG_SECTION_MISSING` is absent.

**Fails on `3594d62`**: T1 (the `coverage` and `tests`-on-ingest rows, the duplicate rows), T2, T3, the T4 FR-012
rows, T5, and R3's template test.
