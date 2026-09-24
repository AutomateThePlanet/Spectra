# Research: Lossless Test Writes and Doc-Reference Resolution (Spec 078)

Everything here comes from reading the code on branch `078-lossless-test-writes`. No NEEDS CLARIFICATION items are left.
Line numbers are as of the branch point.

## Confirmed facts that shape the design

| # | Fact | Where |
|---|------|-------|
| F1 | `TestCase` is a `sealed class` with no equality override. Copies are made by hand, field by field, in 4 places. | `Spectra.Core/Models/TestCase.cs:8`; `GroundingWriteBackService.CopyWithGrounding:105-133`; `ReviewFlaggedHandler.CopyWithGrounding:165-193`; `UpdateHandler.ApplyChangesAsync:659-680`; `UpdatedTestIngestor.ApplyEdit:120-157` |
| F2 | The writer never writes `description`, `environment`, `related_work_items`, `custom`, `automated_by`, `requirements`, `bugs`, or grounding `source`/`created_by`/`note`. The parser never reads `status`, `orphaned_reason`, `orphaned_date`. | `TestFileWriter.cs`, `TestCaseFrontmatter.cs` |
| F3 | Frontmatter uses YamlDotNet with `IgnoreUnmatchedProperties`, so unknown keys disappear. No raw pass exists. `ExtractYamlContent` drops every line whose trimmed text is `---`, including lines inside the YAML. | `MarkdownFrontmatterParser.cs:20-23,129-142` |
| F4 | The body parser keeps only: the first H1; the first Preconditions / Steps / Expected Result / Test Data section; numbered lines in Steps. It drops the text between the H1 and the first `##`, unknown `##` sections, duplicate known sections, and step continuation lines or bullets. | `TestCaseParser.cs:142-189` |
| F5 | The writer's escaping is incomplete: backslashes and `\r` are not escaped. Tags, component, source_refs, criteria, generator and critic are written unquoted, so values containing `: ` or ` #` or starting with an indicator character break. `score` uses the current culture. `verified_at` has a literal `Z` with no UTC conversion. Duration loses its seconds part once it is ≥1 min. | `TestFileWriter.cs:199-215` |
| F6 | For a manual verdict, `GroundingFrontmatter.ToMetadata` sets `VerifiedAt = DateTimeOffset.UtcNow`, so two parses of the same file give different results. The manual branch also drops the flagged, repair and findings fields. | `Grounding/GroundingFrontmatter.cs:54-107` |
| F7 | `ApplyEdit` copies neither `EstimatedDuration` nor anything the original had outside the typed model. | `UpdatedTestIngestor.cs:120-157` |
| F8 | Nothing in `src` ever sets `UpdateProposal.ProposedTest`, so `ToUpdate` is always empty. The real writes in auto-apply are orphan marking, deletes (`--delete-orphaned`) and `_index.json`. | `UpdateHandler.cs:606-632`; `UpdateReviewer.cs:143` |
| F9 | With `--diff`, `--dry-run` returns before `ProgressManager.Complete`, which is the only place that writes the final `.spectra-result.json`. `ai update` never writes a JSON document to stdout. The human tables print whatever `--output-format` / `--verbosity` say. | `UpdateHandler.cs:355-359,541`; `ClassificationPresenter`, `DiffPresenter` |
| F10 | No existing test runs `ai update` with `--no-interaction` or `--dry-run`. Every one passes `--no-review`. | `tests/Spectra.CLI.Tests/Commands/UpdateCommandTests.cs` |
| F11 | `LoadSourceDocumentsAsync` joins refs onto `source.local_dir` (default `docs/`) and keys entries by the raw ref. It also walks every `*.md` under the source dir, keyed relative to it. `TestClassifier.ClassifyBatch` does an exact `TryGetValue` on the raw ref, and the first hit wins. | `UpdateHandler.cs:564-604`; `TestClassifier.cs:174-181` |
| F12 | The critic, repair-prompt and repair-batch commands each carry their own copy of the ref loader: split on `#`, then use the ref if rooted or join it onto the cwd. Critic and repair-prompt throw → exit 1; repair-batch silently skips missing files. None of them checks that a path stays inside the repo. | `CompileCriticPromptCommand.cs:342-366`; `CompileRepairPromptCommand.cs:146-168`; `CompileRepairBatchCommand.cs:147-168` |
| F13 | `SourceRefNormalizer` already exists for string-only normalisation (strip fragment, `\`→`/`, trim leading `/`). Every command uses `Directory.GetCurrentDirectory()` as the repo root. | `Spectra.Core/Coverage/SourceRefNormalizer.cs` |
| F14 | `spectra init` does not gitignore the root-level `.spectra-result.json` / `.spectra-progress.html`. Every long-running command writes them, including dry runs. | `InitHandler.cs:376-398` |
| F15 | No test asserts on the `spectra-criteria` Step 2b text, and adding `--dry-run` to the update skill lines breaks no skill test. Skills are embedded resources with no checked-in hash manifest. | `SkillsManifestTests.cs` |

## R1 — How the writer becomes lossless: baseline-diff serialisation

**Decision**
- At parse time, record a source layout on the test (R2). It holds the raw text of every top-level frontmatter entry, the
  raw body segments, and the baseline values the parser produced.
- The writer emits known frontmatter keys in one fixed order (see data-model.md §Key order). For each key:
  - **Unchanged** from the baseline: reproduce the original exactly. Use the raw text if the key was present; emit
    nothing if it was absent.
  - **Changed or new**: render it canonically through YamlDotNet's emitter (correct quoting and escaping).
- Unknown keys follow, verbatim, in their original order.
- The body is handled the same way, section by section (R3).
- A test with no layout (a newly generated test) is rendered fully canonically.

**Rationale**
- The spec requires values to survive (FR-001), no content to be dropped (FR-004), and output to be byte-identical after a
  normalising first write (FR-003).
- Reproducing unchanged values verbatim keeps diffs minimal. For example, orphan marking touches only its 3 lines (SC-006).
- It also avoids a whole class of re-render mistakes: an unparseable duration, an invalid priority, `custom` values whose
  quoting would change type in other YAML readers, or frontmatter `preconditions` shadowed by the body.
- Canonical rendering, which is now correct, only applies to values a command actually changed.

**Alternatives considered**
1. **Always re-render canonically from the typed model.** Rejected:
   - It can't be lossless for values the model can't represent (invalid priority → `medium`, sub-second durations,
     quoted-numeric `custom` strings, which deserialise untyped).
   - It rewrites every hand-formatted line on the first touch.
2. **Edit the raw text in place per field**, like `FrontmatterUpdater`'s regexes. Rejected: it's fragile for nested blocks
   (`grounding`, `custom`), and each writer path would need its own patch logic.
3. **Fail loud on unknown content.** Rejected by the user in the 2026-09-24 clarification.

**What "unchanged" means**, in the order the writer checks:
- Strings, enums and nullable structs: value equality.
- Lists: sequence equality.
- `Custom` and `Grounding`: reference equality with the baseline. Only the parser creates these, `with` copies keep the
  reference, and every grounding write-back builds a new instance.

## R2 — Capturing raw frontmatter entries

**Decision**
- Take the frontmatter text from the file slice between the opening and closing `---` delimiter lines. Replace
  `ExtractYamlContent`'s filter that strips `---` lines, so a `---` inside a block scalar survives.
- Run YamlDotNet's low-level `Parser` over that text. For each top-level mapping key (a `Scalar` event at depth 1), record
  `key → raw slice`. A slice runs from the key's start index to the next top-level key's start index, or to the end of
  the text.
- The typed DTO deserialisation stays as it is. The raw pass runs alongside it.

**Rationale**: YamlDotNet is already a dependency (16.3.0), and its event marks give exact character offsets. There's no
hand-rolled YAML scanning.

**Alternatives considered**: a line-based column-0 key regex. Rejected because it misreads multi-line flow collections and
block scalars.

Edge cases:
- A comment line directly above a key belongs to the previous entry's slice. It is lost only if that previous entry is
  re-rendered because its value changed. This is accepted.
- Duplicate keys still fail the typed parse, as they do today.

## R3 — Body sections

**Decision**
- Split the body into ordered segments:
  - `Leading`: anything before the H1
  - `Title`: the H1 line
  - `Intro`: from the H1 to the first `##`
  - one segment per `##` section: header line plus raw content, up to the next `##`
- Classify each section as Preconditions, Steps, ExpectedResult or TestData using the existing header regexes. Only the
  first match of each kind counts; later duplicates are `Unknown`, which matches today's parser semantics.
- When writing, walk the segments in order:
  - **Known section whose model value equals the baseline**: emit the raw text. This keeps step sub-bullets,
    continuation lines and header variants such as `## Test Steps`.
  - **Known section whose value changed**: render it canonically in place.
  - **Known section now empty in the model**: drop it.
  - **Known section that is new in the model** (absent from the layout): insert it canonically right after the nearest
    preceding known section, in canonical order Preconditions → Steps → Expected Result → Test Data.
  - `Leading`, `Intro` and `Unknown` segments: emit verbatim.

**Rationale**: This meets FR-004 exactly: unknown sections stay in place, and content is byte-preserved. It also stops the
parser's lossy projections (step bullets, continuation lines) from silently deleting content from files that were never
edited.

**Alternatives considered**
1. **Append unknown sections after the known ones.** Kept only as the fallback the spec allows, for when an edit removes
   the anchor section.
2. **Extend the step parser to capture continuation lines.** Rejected for this phase: it changes execution-time display
   semantics.

## R4 — `TestCase` becomes a `sealed record`; every copy uses `with`

**Decision**
- Change `public sealed class TestCase` to `public sealed record TestCase`.
- Replace all 4 hand copies (F1) with `with` expressions:
  - orphan marking: `original with { Status, OrphanedReason, OrphanedDate }`
  - grounding write-back and review accept: `original with { Grounding = … }`
  - ingest-update: `original with { Title, Preconditions, Steps, ExpectedResult, TestData, ScenarioFromDoc, SourceRefs, Criteria }`

**Rationale**
- Field-by-field copying is the root cause of P2 and F7. `with` carries every current and future property, including the
  new `Layout`, so the unknown-content carry-over for ingest-update (FR-004) follows automatically.
- A grep found no `HashSet<TestCase>`, no `Dictionary<TestCase,…>`, and no reference-equality checks on tests, so the
  synthesized value equality is safe.
- Round-trip tests use a test-only deep comparer, because the synthesized equality compares list members by reference.

**Alternatives considered**
1. **A shared `CopyWith(...)` helper on the class.** Rejected: it's still a field list someone has to maintain.
2. **Keep the class and add a clone method.** Rejected for the same reason.

## R5 — Grounding round-trip

**Decision**
- `GroundingMetadata` gains optional `Source`, `CreatedBy` and `Note`, mapped from the DTO and written by the writer.
- `ToMetadata` for a manual verdict:
  - takes `verified_at` from the file when it parses, and falls back to `DateTimeOffset.MinValue` otherwise. MinValue is
    the existing fallback for an unparseable date, so this makes the parse deterministic (F6).
  - maps flagged, repair and findings like the other verdicts.
- Canonical rendering:
  - `score`: invariant culture, format `0.00##############`. `0.9` → `0.90` (unchanged from today); `0.873` → `0.873`
    (exact).
  - `verified_at`: converted to UTC, then `yyyy-MM-ddTHH:mm:ss.FFFFFFFZ` in invariant culture. The fraction is omitted
    when it is zero, so existing files keep their bytes.

**Rationale**: FR-001 names `grounding` as a preserved field, and FR-003 needs parsing to be deterministic.

## R6 — Lifecycle fields and orphan dates

**Decision**
- `TestCaseFrontmatter` gains `status`, `orphaned_reason` and `orphaned_date`, mapped to `TestCase`.
- `orphaned_date` is parsed with the invariant culture as `yyyy-MM-dd`. If it doesn't parse, the model value is null and
  the raw text is reproduced while it stays unchanged (R1).
- Orphan marking sets `OrphanedDate` to today's UTC date at midnight, so the in-memory value equals what is written.
- A test that is already `status: orphaned` keeps its existing `orphaned_date` (FR-007).

## R7 — Duration and line endings

**Decision**
- **Duration**: canonical form is `{h}h{m}m{s}s`, omitting zero parts, with `0s` for zero. The parser already accepts
  combined forms. A value the canonical form can't hold, such as a sub-second `00:00:00.5`, stays exact because it
  remains unchanged and so its raw text is reproduced.
- **Line endings**: the layout records the original file's newline sequence (CRLF if its first line break is CRLF,
  otherwise LF), and the writer uses it for the whole file. Tests with no layout are written with `\n`.
- **Raw segments** are stored with their original newlines, so byte preservation holds.

**Rationale**: This gives deterministic output independent of platform for new files, and byte-identity for existing ones.
Today's `Environment.NewLine` makes output depend on the platform.

## R8 — `spectra ai update` apply semantics (FR-009 / FR-009a / FR-010)

**Decision**
- `classifyOnly = _dryRun || (_noInteraction && !_noReview)`.
- Remove the early `return` at `UpdateHandler.cs:355-359`.
- After classification and the optional diff:
  - **When `classifyOnly`**: skip review, apply and index regeneration. Build the same `UpdateResult` with
    `TestsUpdated = 0` and `TestsRemoved = 0`, call `ProgressManager.Complete`, render the run summary, and return
    `ExitCodes.Success`.
  - **Otherwise**: auto-review when `_noReview`, else the interactive reviewer. This is today's path.
- `UpdateResult` gains `applied: bool`. CI and the skill can then tell "nothing to change" from "not applied" after the
  breaking change.
- A classify-only run that is not a dry run prints one info line:
  `Classification only — no changes written. Pass --no-review to apply.`
- The `--diff` help text drops "without applying", which was never true.

**Rationale**
- This follows the user's answer on 2026-09-24.
- Exit codes are the same for the same classification outcome (FR-010), because the success path returns 0 either way.
- No existing test breaks (F10).
- The auto-detected non-interactive branch (redirected I/O) is unchanged, as the spec's assumption says.

**Run-status files.** `.spectra-result.json` and `.spectra-progress.html` are not treated as "writes". They are how every
long-running command reports its result, and the skill reads them. To keep `git status` clean (SC-005), `spectra init`
adds both to its idempotent gitignore list (F14).

**Alternatives considered**: a skill-only `--dry-run` fix. Rejected: it was superseded by the user's answer, and on its own
it's broken by F9.

## R9 — The shared doc-reference resolver (FR-011 to FR-015)

**Decision**
- Add `SourceRefResolver` in `Spectra.Core/Coverage/`, next to `SourceRefNormalizer`, reusing its `StripFragment`.
- It is built from `(repoRoot, sourceDir)`. `Resolve(IEnumerable<string> refs)` returns a `SourceRefResolution` with
  separate `Resolved` and `Missing` lists (data-model.md). It does no I/O except `File.Exists`, and never throws.
- Resolution order for each ref:
  1. Strip the fragment and trim, then normalise `\` → `/`. If the result is empty → `Missing(Empty)`.
  2. If the ref is fully qualified (e.g. `C:\…`, or `/abs` on Unix) **and** lies under the repo root: use it if the file
     exists.
  3. Treat it as repo-relative, trimming a leading `/`, and combine with the repo root. Use it if the file exists.
  4. Fallback for legacy refs: combine with the source dir. Use it if the file exists.
  5. Any candidate whose full path is outside the repo root → `Missing(OutsideRepository)`. Otherwise → `Missing(NotFound)`.
- Files are deduplicated by full path. The comparer is `Ordinal` on Linux and `OrdinalIgnoreCase` elsewhere, matching the
  critic loader's existing behaviour on Windows. No case folding is added to the lookup itself.
- `repoRoot` is `Directory.GetCurrentDirectory()`, as in every command today (F13). `sourceDir` is `source.local_dir`.

**Rationale**
- The rules match the user's FR4. Fully-qualified and leading-`/` refs are handled deterministically.
- There is exactly one new abstraction, which Constitution V explicitly allows here.

**Adoption**

| Consumer | File:line | Category | Action |
|---|---|---|---|
| `UpdateHandler.LoadSourceDocumentsAsync` | UpdateHandler.cs:564-604 | opens files (broken) | **Adopt.** Key the dictionary by the original raw ref, and read each distinct file once. Remove the walk over every `*.md`; the resolver's source-dir fallback covers it. |
| `TestClassifier.ClassifyBatch` | TestClassifier.cs:174-181 | lookup in the ref-keyed dictionary | **Keep the API.** Change first-hit-wins to joining the distinct contents of **all** resolved refs, so classification uses every referenced doc (FR-013). ORPHANED "Source document not found or empty" now means no ref resolved to a non-empty file. |
| `BatchProposeUpdatesTool` | BatchProposeUpdatesTool.cs:43 | same dictionary | Fixed by the change above. It still omits criteria, which is out of scope. |
| `CompileCriticPromptCommand.LoadDocumentsFromRefsAsync` | :342-366 | opens files, fails loud | **Adopt.** Any `Missing` → same message `Source ref not found: {ref}`, exit 1. Refs outside the repo now fail loud too (CHANGELOG). |
| `CompileRepairPromptCommand` loader | :146-168 | opens files, fails loud | **Adopt**, fail-loud unchanged. |
| `CompileRepairBatchCommand` loader | :147-168 | opens files, lenient | **Adopt**, keep skipping missing refs. |

**Left alone, deliberately (FR-015).** None of these open files; they compare refs as identifiers:

| Consumer | File:line | Why it's left alone |
|---|---|---|
| `DocumentationCoverageAnalyzer` | :39, :89 | Compares identifiers (fragment stripped, case-sensitive). Changing matching changes coverage numbers, which is out of scope. |
| `AnalyzeHandler` coverage counts | :894, :910 | Raw identifier compare; it undercounts fragment refs. Recorded as a follow-up. |
| `GapAnalyzer` | :22-26, :180-186 | Already uses `SourceRefNormalizer`. |
| `DataCollector` (dashboard) | :553-566, :672-711 | Identifier and graph-node keys. |
| `CoverageSnapshotBuilder` | :59-61, :101, :185 | Fragments are **meaningful**: they are matched against section refs. |
| `CriticPromptBuilder` | :163-165 | Matches already-resolved docs by substring. |
| `LinkReconciler` | :75, :200, :358 | Automation-file heuristic. |
| `dashboard-site/scripts/app.js` | :989-991 | Display grouping. |
| Passthroughs: parser, index generator, ingestors, show, reports, bug-report template, `UpdatePromptCompiler` | — | Copy or display only. |

## R10 — Skills and docs

**Decision**
- **`spectra-update.md`**: add `--dry-run` to the Step 1 command. The rest of the flow is unchanged; `.spectra-result.json`
  is now produced (R8).
- **`spectra-generate.md:411` and `spectra-execute.md:138`**: add `--dry-run` to the update delegation rows.
- **`spectra-criteria.md`**:
  - Step 2b becomes: "Read the compiled prompt — it already contains the full document text; do not read `{path}`
    separately — and identify …".
  - Line 11, "(reading the document with your file tools)", is reworded the same way.
- **New skill-content tests**:
  - every `ai update` classification line in these three skills contains `--dry-run`
  - `spectra-criteria` contains no instruction to read the document file
- **Docs**:
  - `docs/cli-reference.md` `ai update` section: the flag semantics table; replace the CI example
    `--no-interaction   # CI mode` with `--no-interaction --no-review`; the classify-only note; ref resolution.
  - `docs/usage.md`: the update section.
  - `CHANGELOG.md` `[2.5.0]`: Fixed, Changed with a **Breaking** note, and "not recovered — use Git".
  - `CLAUDE.md`: the Recent Changes bullet, and the `ai update` line under Commands.
  - `Directory.Build.props` version 2.5.0.

## Out-of-scope observations (not fixed here)

- `DeleteHandler.StripDependsOnAsync` and `SuiteDeleteHandler` remove **every** `- TC-n` list line anywhere in a file and
  collapse blank lines. That is lossy raw-text editing of bodies.
- `DoctorIdsHandler.ReplaceIdInFrontmatter` replaces the id across the whole file, not only in the frontmatter.
- `AnalyzeHandler` / `DocumentationCoverageAnalyzer` ref matching is inconsistent (fragments and bare refs).
- A test with zero `source_refs` still classifies ORPHANED ("not found"). With R8 this no longer triggers a write.
- `UpdateProposal.ProposedTest` is dead code (F8).
