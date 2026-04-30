# Phase 0 Research — Document Index Restructure

**Branch**: `045-doc-index-restructure`
**Date**: 2026-04-29

## R-001 — YAML serializer choice for the manifest

**Decision**: Use **YamlDotNet** (already a dependency since Spec 044) for `_manifest.yaml`.

**Rationale**:
- The existing `CriteriaIndex` (Spec 023) already serializes/deserializes via `YamlDotNet` with `[YamlMember]` attributes. Using the same library is the YAGNI-aligned choice and minimizes new dependencies.
- YAML round-trips comments and key order well enough for our deterministic-ordering requirement (we re-emit on every write — comments are decorative-only).
- The manifest is small (~2–5K tokens for typical projects, max ~10K) — perf is not a concern.

**Alternatives considered**:
- *System.Text.Json with .yaml extension renamed to .json*: rejected — users will manually inspect the manifest; YAML is more readable and matches `_criteria_index.yaml` convention from Spec 023.
- *Tomlyn / TOML*: rejected — adds a new dependency for no concrete benefit.
- *Manual YAML emitter*: rejected — re-implementing escaping for unusual suite IDs is risk we don't need.

## R-002 — Glob matcher for `coverage.analysis_exclude_patterns`

**Decision**: Use **`Microsoft.Extensions.FileSystemGlobbing.Matcher`** (already transitively available via .NET 8 / ASP.NET Core in `Spectra.MCP`).

**Rationale**:
- Native support for `**/Old/**`, `**/CHANGELOG*`, etc. exactly as written in the spec.
- Same library Microsoft uses for `Include`/`Exclude` patterns in MSBuild and dotnet CLI — well-tested.
- No regex translation or third-party glob library needed.

**Alternatives considered**:
- *DotNet.Glob*: rejected — third-party dependency for a feature already in BCL.
- *Custom regex from glob*: rejected — error-prone (matching `**` correctly is non-trivial).

## R-003 — Atomic file writes

**Decision**: Use **write-to-temp-then-rename** for the manifest, checksum store, and any group file. Helper goes in `Spectra.Core/Index/AtomicFileWriter.cs` (new).

**Rationale**:
- The spec's safety requirement (FR-021) is that migration leaves no partial new layout if it fails midway. The write-to-temp pattern accomplishes this per file.
- `File.Move(source, dest, overwrite: true)` on .NET 8 is atomic on the same volume on both Windows and POSIX.
- For migration we go further: write the entire new `docs/_index/` tree under a sibling `docs/_index.tmp/` directory, then rename the directory only after every per-suite file landed successfully. On failure we delete the tmp directory and leave the legacy `_index.md` untouched.

**Alternatives considered**:
- *File.WriteAllTextAsync directly*: rejected — can leave a half-written manifest if the process is killed.
- *Two-phase commit with a `.lock` file*: overkill; we do not need crash-recovery semantics within a single process.

## R-004 — Suite ID casing & sanitization

**Decision**: **Preserve the directory name's casing** (e.g., `SM_GSG_Topics`, not `sm_gsg_topics`). Sanitize by replacing `/` with `.`, trimming leading/trailing dots, and rejecting frontmatter values that contain spaces or path separators.

**Rationale**:
- Spec §7 Q1 recommends preserving casing — matches filesystem and existing `--suite` argument habits.
- Test suites (`tests/{suite}/`) already preserve casing today; doc-suite IDs should follow the same convention.
- Sanitization rules are minimal and predictable; the spec's frontmatter validation requirement (FR-008) handles the user-supplied case explicitly.

**Alternatives considered**:
- *Lowercase normalization*: rejected per spec author's recommendation. Would break existing `--suite` invocations on case-sensitive filesystems.
- *Slugify (replace underscores with dashes)*: rejected — invasive and would not match existing directories.

## R-005 — Token-budget pre-flight check location

**Decision**: A new `PreFlightTokenChecker` class in `Spectra.CLI/Index/`, invoked by `BehaviorAnalyzer` (and later by `RequirementsExtractor`) **before** any AI call. Reads `ai.analysis.max_prompt_tokens` from config (default 96000). Throws `SpectraException` with a structured message naming the suite IDs and per-suite token estimates.

**Rationale**:
- Keeping the check in CLI rather than Core: the actionable error references CLI flags (`--suite`, `--analyze-only`) and suite IDs. Core is provider-agnostic.
- Default of **96000 tokens** leaves a 32K margin under the model's 128K window for: response (~8K), prompt template (~4K), existing-test frontmatter snapshot (~5K), coverage snapshot (~5K), category and technique guidance (~5K), retry buffer (~5K). Conservative enough to never trip the model-side 400 even on edge cases.
- The check is **soft on per-suite content but hard on aggregate**: if `--suite X` is passed and that suite alone fits, we proceed. If `--suite X,Y,Z` overflows, we list all three suites and their estimates.
- Already-existing `SpectraException` (used in other CLI handlers) is the right vehicle. Caught by command handlers, formatted to stderr, exit code 1.

**Alternatives considered**:
- *Soft truncation*: rejected — the spec explicitly requires "fail fast with an actionable error" (FR-014). Silent truncation hides the problem from the user and lies about what was analyzed.
- *Place the check in Core/BehaviorAnalyzer*: rejected — Core can't reference CLI flags in error messages, and the error is most useful right where the user can act on it.

## R-006 — Migration trigger and detection

**Decision**: `LegacyIndexMigrator.NeedsMigration(basePath, sourceConfig)` returns `true` iff:
1. `<local_dir>/_index.md` exists, AND
2. `<local_dir>/_index/_manifest.yaml` does not exist.

`DocsIndexHandler.ExecuteAsync` calls the migrator first, before any other index work. Migration is opt-out via `--no-migrate` (in which case the handler errors out if needs-migration is true).

**Rationale**:
- Mirrors Spec 026's auto-detect-and-migrate pattern (`docs/requirements/` → `docs/criteria/`).
- The dual condition (legacy present AND new absent) means a user who manually deletes their `_index.md.bak` after success still gets normal incremental behavior on the next run — re-creating `_index.md` from disk would be extremely unusual.
- If both legacy and new exist (someone copy-pasted), we treat new as authoritative and emit a warning.

**Alternatives considered**:
- *Require explicit `--migrate` flag*: rejected — every existing user would break on first upgrade. The spec mandates "no flag required, no breaking change" (User Story 2).
- *Migrate transparently on `BehaviorAnalyzer` invocation*: rejected — migration should be a deliberate `docs index` event with a clear log line, not a side effect of an unrelated command. If a user runs `spectra ai generate` without first running `spectra docs index`, they get a clear error pointing to the missing manifest.

## R-007 — Spillover threshold and file naming

**Decision**: `coverage.max_suite_tokens` defaults to **80000**. When a suite exceeds this, the indexer additionally writes per-doc files under `docs/_index/docs/{sanitized-relative-path}.index.md`. The manifest entry gains a `spillover_files: [...]` array listing the relative paths (under `docs/`, not under `_index/`) of the source documents whose per-doc index files exist.

**Rationale**:
- 80K leaves clear headroom against the 96K pre-flight budget (R-005). A single suite hitting spillover is genuinely rare on real projects per spec §6 — this is insurance for one-giant-suite projects, not the common path.
- Sanitization for the spillover filename: replace path separators with `__` and strip the `.md` extension before appending `.index.md`. So `RD_Topics/Old/3-9-7.md` becomes `docs/_index/docs/RD_Topics__Old__3-9-7.index.md`. Reversible enough to debug, deterministic.
- Spec 041 will consume `spillover_files`; this spec freezes the format so 041 doesn't need a follow-up shape change.

**Alternatives considered**:
- *Sub-directory layout `docs/_index/docs/RD_Topics/Old/3-9-7.index.md`*: rejected — leaves empty intermediate directories that complicate cleanup and look like a parallel doc tree on disk.
- *Per-suite chunked files (`SM_GSG_Topics.part1.index.md`, `.part2.index.md`)*: rejected — Spec 041 wants per-doc granularity for cross-batch dedup; chunks defeat that.

## R-008 — Backwards-compat: `source.doc_index` (single-file path)

**Decision**: Treat `source.doc_index` as a deprecated hidden alias. If set in config, derive `doc_index_dir` as `Path.GetDirectoryName(doc_index)` if it ends in `_index.md`, else log a warning and fall back to `<local_dir>/_index/`. New default field: `source.doc_index_dir = "docs/_index"`. The legacy field stays in the JSON schema (deserializes without warnings) but is not surfaced in `spectra config` or in any docs.

**Rationale**:
- Most users never set `source.doc_index` — it defaults from `local_dir`. The alias matters for the small set of users who pinned a custom index path.
- "Hidden alias, not removed" follows Constitution V (no backwards-compat shims when code can be changed directly) — but here the field is user-facing config; removing it silently would break their `spectra.config.json`.

**Alternatives considered**:
- *Remove `source.doc_index` outright*: rejected — would break anyone with a custom index path on upgrade.
- *Add `source.doc_index_dir` and require manual migration of `source.doc_index`*: rejected — defeats User Story 2 (no manual action).

## R-009 — Edge case: directory with single document

**Decision**: When the directory-based default rule encounters a single-document directory, **roll up to the next directory level that has multiple documents**. Walk up at most one level beyond `local_dir`. If we reach `local_dir` itself, the document is assigned to suite `_root`.

**Rationale**:
- Avoids one-doc suites like `SUMMARY` becoming their own group (the user would have ~50 trivial groups in a typical GitBook export).
- One-level-up is enough: deeply-nested unique files like `SM_GSG_Topics/manage-items/standard-items/setting-up-standard-items.md` already get rolled up to `SM_GSG_Topics` because every intermediate directory only has one match in the leaf.
- The edge case from spec §3.5 examples (`SUMMARY.md` → `_root`) is preserved.

**Alternatives considered**:
- *One-doc directories become their own suite*: rejected — produces noise and bloats the manifest.
- *Always roll up to top-level segment under `local_dir`*: rejected — too aggressive; would merge `RD_Topics` and `RD_Topics/Old/` into one suite and lose the exclusion-pattern hook.

## R-010 — Test fixture for the 541-doc real case

**Decision**: Check the actual 378 KB legacy `_index.md` reported by the user into the repo at `tests/TestFixtures/legacy_index_541docs/_index.md`. Add a sibling `README.md` documenting provenance. The fixture is read-only in tests; assertions check group counts, exclusion-pattern matches, and migration output.

**Rationale**:
- Spec §5.1 explicitly calls for this fixture.
- 378 KB is small enough to commit and adds zero CI cost.
- The fixture is the only way to catch real-world edge cases (escaped pipes in section summaries, non-ASCII titles, weird path depths).

**Alternatives considered**:
- *Synthetic fixture only*: rejected — would not catch the real-world parse edge cases that motivate the migration in the first place.
- *Reference-by-URL fixture*: rejected — flaky in CI, makes tests depend on network.

## R-011 — Suite selection from `--suite`/`--focus`/no-filter

**Decision**: New `SuiteSelector` class in `Spectra.CLI/Agent/Copilot/`. Resolution order:
1. `--suite <id>` exact match against `manifest.groups[].id` → load only that suite.
2. `--suite <id>` no match → emit warning listing available suites; fall back to no-filter behavior (subject to the pre-flight budget).
3. `--focus "<keywords>"` → score each suite by keyword overlap with `id` and `title`; pick top-N suites until the cumulative token estimate hits 70% of the pre-flight budget.
4. No filter → load all suites where `skip_analysis == false`, in priority order: highest-token-density first (so larger product areas win when budget is tight).

**Rationale**:
- Aligns with the spec's behavior table (§3.7) and edge-case handling (§3.9).
- Priority-ordered packing for no-filter is a small UX win — it ensures the most-likely-to-be-relevant suites get analyzed first when budget is tight.
- Keeping the selector outside `BehaviorAnalyzer` lets `RequirementsExtractor` and `GenerationContextLoader` reuse it.

**Alternatives considered**:
- *Embed selection inside `BehaviorAnalyzer`*: rejected — three call sites duplicate the logic.
- *Selection by exact path match instead of suite ID*: rejected — users type `--suite SM_GSG_Topics`, not `--suite docs/requirements/gitbook-repo/docs/SM_GSG_Topics`.

## R-012 — Default value for `coverage.analysis_exclude_patterns` and breaking-change risk

**Decision**: Ship with the patterns from spec §3.6 baked in as defaults. Existing users who upgrade and run `spectra docs index` get the same suites flagged as `skip_analysis: true` automatically — no config change required. Config's pattern list is a *replacement*, not a *merge*: if a user sets `analysis_exclude_patterns: []` they opt out of all defaults.

**Rationale**:
- The whole point of User Story 3 is "do the right thing by default" — and defaults that match real-world projects (`Old/`, `legacy/`, `archive/`, `release-notes/`, `CHANGELOG*`, `SUMMARY.md`) cover the cases that motivated the spec.
- Replace-not-merge keeps the model predictable: `[]` means "exclude nothing"; the user is in control.

**Alternatives considered**:
- *Empty defaults; require user opt-in*: rejected — wastes the migration moment when we could already classify the user's archived dirs correctly.
- *Merge user list with defaults*: rejected — surprising; users who explicitly write `[]` to disable defaults would still see them apply.

## R-013 — Performance: many small files vs one big file

**Decision**: Accept the per-file IO overhead. Verify in Phase 1 that `spectra docs index` on the 541-doc fixture completes in ≤ 1.5× the legacy time. If it doesn't, batch suite-file writes via parallel `Task.WhenAll` (each write is independent).

**Rationale**:
- Spec §6's risk row addresses this exactly: 50 suites × ~10ms per file ≈ 500ms — well within the 1.5× budget for any project that took >1s with the legacy single file.
- SSDs are universal in the user base today; mechanical-disk worst case is the edge to watch.

**Alternatives considered**:
- *Single bundle file*: rejected — defeats the entire spec.
- *In-memory ZIP*: rejected — overkill, breaks the "check artifacts into git" expectation.

## R-014 — `RequirementsExtractor` integration and "skip_analysis"

**Decision**: `RequirementsExtractor` (today's class name; functions as the criteria extractor in spec §3.7) iterates documents from the manifest. By default, documents in suites with `skip_analysis: true` are filtered out. A new `--include-archived` CLI flag passes `includeArchived: true` to the extractor, which then includes those documents.

**Rationale**:
- Today the extractor takes `IReadOnlyList<DocumentEntry>` from `DocumentMapBuilder.BuildAsync`. We change the builder to source from the manifest and to honor `skip_analysis` by default.
- The flag's name and behavior match the source spec's User Story 3 acceptance scenarios exactly.

**Alternatives considered**:
- *Pass a filter callback into the extractor*: rejected — leaks "what's archived?" semantics into the extractor itself.
- *Always include all documents and let the user filter via config*: rejected — defeats User Story 3's "do the right thing by default."

## R-015 — Phasing across PRs

**Decision**: Four phases as defined in the source spec, each shippable as one PR:

- **Phase 1 (this branch's first PR)**: New layout reading/writing + migration. No consumer changes — `BehaviorAnalyzer` etc. still read the legacy file via `DocumentIndexReader`. After this PR, running `spectra docs index` produces the new layout and the `.bak`, but `spectra ai generate` still works against the legacy reader.
- **Phase 2 (second PR)**: Consumer migration. `BehaviorAnalyzer`, `RequirementsExtractor`, `DocumentationCoverageAnalyzer`, `DataCollector`, `GenerationContextLoader` switch to manifest-driven loading. Pre-flight check goes live. `DocumentIndexReader`/`Writer` (legacy) **deleted** — no alias period per spec §3.8.
- **Phase 3 (third PR)**: Exclusion patterns, frontmatter overrides, spillover, `--include-archived` flag.
- **Phase 4 (fourth PR)**: SKILL/agent/dashboard/docs polish + introspection commands.

**Rationale**:
- Each phase is independently reviewable and rolls back cleanly.
- Phase 1 is intentionally non-breaking (consumers still see the legacy file via the old reader) — buys time to test migration in CI without a flag day.
- Phase 2 is the actual bug-fix release; gates on Phase 1.
- Phases 3 and 4 are quality polish, deferrable.

**Alternatives considered**:
- *One mega-PR*: rejected — too large to review, hard to bisect if regressions surface.
- *Phase 1 + 2 in one PR*: tempting but doubles review surface and increases risk of partial-merge mishaps.

## Open items (carry to /speckit.tasks)

- None. All `NEEDS CLARIFICATION` from `Technical Context` are resolved above.
