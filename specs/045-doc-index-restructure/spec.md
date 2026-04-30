# Feature Specification: Document Index Restructure

**Feature Branch**: `045-doc-index-restructure`
**Created**: 2026-04-29
**Status**: Draft
**Input**: User description: "Spec 040 — Document Index Restructure: Replace monolithic `docs/_index.md` with structured per-suite layout under `docs/_index/` (manifest + per-suite index files + separated checksums) to fix BehaviorAnalyzer 204K-token prompt overflow on 500+ doc projects. Includes automatic migration from legacy single-file index, default exclusion patterns for archived docs, and consumer updates across BehaviorAnalyzer, CriteriaExtractor, coverage analyzer, dashboard data collector. Prerequisite for Spec 041 iterative behavior analysis."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate tests on a large documentation corpus without context-window failure (Priority: P1)

A QA engineer working on a project with 500+ documentation files runs `spectra ai generate --suite SM_GSG_Topics` to generate tests for one product area. Today this fails with a hard `400 prompt token count of 204224 exceeds the limit of 128000` error because the analyzer loads the full document index regardless of which suite was requested. After this change, the analyzer loads only the index entries for the requested suite, the request fits well inside the model's context window, and the command completes end-to-end.

**Why this priority**: This is the headline bug. Large-corpus projects cannot use AI test generation at all today. Fixing it unblocks the primary user workflow.

**Independent Test**: Run `spectra ai generate --suite POS_UG_Topics --analyze-only` against the reported 541-document project and observe that (a) no token-limit error occurs and (b) the prompt size logged for the analyzer is well under the 128K context window.

**Acceptance Scenarios**:

1. **Given** a project with 541 documents indexed across 12 suites, **When** the user runs `spectra ai generate --suite POS_UG_Topics --analyze-only`, **Then** the command completes without retries, the analyzer prompt stays under 30,000 tokens, and a behavior analysis is produced.
2. **Given** the same project, **When** the user runs `spectra ai generate` without any suite filter and the combined load exceeds the configured prompt budget, **Then** the command fails fast with an actionable message that lists the available suites with their token estimates and suggests narrowing with `--suite` — never with the raw 400 token-limit error.
3. **Given** a small project (under 100 documents), **When** the user runs `spectra ai generate` without a suite filter, **Then** the manifest plus all suites still fit in budget and the command behaves identically to today.

---

### User Story 2 - Upgrade to the new index without manual migration (Priority: P1)

An existing SPECTRA user with a populated `docs/_index.md` from a prior release pulls the new version and runs `spectra docs index`. The first run automatically detects the legacy file, splits its entries into per-suite files, writes the new manifest and checksum store, and renames the legacy file to `_index.md.bak` for safekeeping. The user does nothing special — no flags, no opt-in. A subsequent `spectra docs index` run does an incremental update against the new layout.

**Why this priority**: Without seamless migration every existing user breaks on upgrade. This must ship together with User Story 1 to be releasable.

**Independent Test**: Take a project that has only the legacy `docs/_index.md` (no `docs/_index/` directory), run `spectra docs index`, and verify the new layout exists, the legacy file is preserved as `_index.md.bak`, and re-running the command does not re-migrate.

**Acceptance Scenarios**:

1. **Given** a project with a legacy `docs/_index.md` and no `docs/_index/` directory, **When** the user runs `spectra docs index`, **Then** the new layout is written, the legacy file is renamed to `docs/_index.md.bak`, and a migration summary is logged (document count, suite count, largest suite).
2. **Given** a migration that fails partway through (disk full, permissions, parse error), **When** the user inspects the project, **Then** the legacy `docs/_index.md` is untouched and no partial `docs/_index/` directory remains.
3. **Given** a project that has already been migrated, **When** the user runs `spectra docs index` again, **Then** the migrator does not re-run, and the command performs a normal incremental update.

---

### User Story 3 - Keep archived and release-notes docs out of analyzer input by default (Priority: P2)

A team has historical documentation under `RD_Topics/Old/` (release notes from prior versions) and `legacy/` directories. They want those docs to stay in the project corpus and be visible to coverage reports, but they don't want the AI analyzer wasting context budget scanning them for testable behaviors. With the new defaults, those documents are still indexed and counted, but the suites containing them are flagged as "skip analysis" and excluded from analyzer-facing prompts. A team that disagrees with the defaults can opt in per-document via frontmatter or globally by editing config.

**Why this priority**: Reduces noise in real projects and recovers a meaningful slice of the context budget. Important for the long-term value of the feature, but the bug-fix in P1 ships even without this.

**Independent Test**: Create a project containing both an active suite and an `Old/` suite, run `spectra docs index`, then run `spectra ai generate` without any filter. Verify the analyzer prompt only contains the active suite's entries, and that adding `analyze: true` to a frontmatter inside `Old/` re-includes that document.

**Acceptance Scenarios**:

1. **Given** a project with documents in `RD_Topics/Old/`, `legacy/`, `archive/`, and `release-notes/` directories, **When** the indexer runs with defaults, **Then** those suites are present in the manifest with a "skip analysis" flag and a recorded reason, and the analyzer does not load them.
2. **Given** a document inside an excluded directory that the user explicitly wants analyzed, **When** the user adds `analyze: true` to its frontmatter and re-indexes, **Then** that document is included in analyzer input on the next run.
3. **Given** a user who wants to disable a default exclusion entirely, **When** they remove the pattern from `coverage.analysis_exclude_patterns` in config and re-index, **Then** the previously-excluded suite drops its skip flag and starts feeding the analyzer.
4. **Given** any indexer run, **When** archived suites are skipped from analysis, **Then** coverage analysis still considers those documents (they may still have linked tests), so coverage metrics are unaffected.

---

### User Story 4 - Inspect what's in the index without reading raw files (Priority: P3)

When a generation run fails with the new "narrow with `--suite`" error, the user needs a quick way to see which suites exist and which one to pick. Likewise, when debugging coverage results, the user wants to see which documents belong to a given suite. Two introspection commands surface this information without requiring the user to open YAML or markdown files by hand.

**Why this priority**: Quality-of-life improvement that makes the new error messages actionable. Defers cleanly to a follow-up phase if needed; the core fix works without it.

**Independent Test**: After indexing, run `spectra docs list-suites` and verify each suite is listed with its document count, token estimate, and analysis status. Run `spectra docs show-suite <id>` and verify the suite's index file content is printed.

**Acceptance Scenarios**:

1. **Given** an indexed project, **When** the user runs `spectra docs list-suites`, **Then** all suites are displayed with their ID, document count, token estimate, and whether they are skipped from analysis.
2. **Given** the same project, **When** the user runs `spectra docs list-suites --output-format json`, **Then** the same information is emitted as machine-readable JSON.
3. **Given** a known suite ID, **When** the user runs `spectra docs show-suite SM_GSG_Topics`, **Then** the suite's index file content is printed to stdout.

---

### Edge Cases

- A document lives directly in the configured `local_dir` root (no parent suite directory) — assigned to a fallback `_root` suite.
- A directory under `local_dir` contains only a single document — that document rolls up to the next directory level that has multiple documents (avoids one-doc suites for unique deep-nested files).
- A user-supplied frontmatter `suite:` value contains slashes, leading dots, or spaces — rejected with a clear error pointing to the offending file.
- A single suite is itself larger than the prompt budget — the analyzer fails fast with the actionable error rather than silently truncating; finer-grained loading is the subject of the follow-up Spec 041.
- The user passes `--suite <id>` for an ID that does not exist in the manifest — the command emits a warning listing available suites and falls back to today's full-corpus behavior (still subject to the pre-flight budget check).
- The legacy `docs/_index.md` exists but is malformed or empty — migration fails atomically with a clear error; no partial new layout is written.
- A document is moved from one directory to another between runs — its suite assignment changes, and the indexer rewrites only the affected suite files plus the checksum store.
- A document is deleted from disk — its entry is removed from the relevant suite file and from the checksum store; the manifest counts update accordingly.
- A suite ends up with zero analyzable documents (all members have `analyze: false`) — the suite still appears in the manifest with the skip flag set and contributes nothing to analyzer prompts.

## Requirements *(mandatory)*

### Functional Requirements

#### Index layout

- **FR-001**: The system MUST replace the monolithic single-file documentation index with a structured layout consisting of a small always-loaded manifest, one index file per suite, and a separate checksum store.
- **FR-002**: The manifest MUST list every suite with its identifier, human-readable title, source directory, document count, estimated token cost, analysis-skip flag, and a pointer to the corresponding suite index file.
- **FR-003**: Each suite index file MUST contain the per-document entries (title, size, key entities, section summaries) for the documents assigned to that suite, in the same shape as today's per-document entries, and MUST NOT contain a checksum block.
- **FR-004**: The checksum store MUST contain content hashes for every indexed document and MUST be readable for incremental-update detection without ever being included in AI prompts.

#### Suite identity

- **FR-005**: The system MUST assign every indexed document to exactly one suite, applying these rules in priority order: per-document frontmatter override, project-level config override, directory-based default, root fallback.
- **FR-006**: The directory-based default MUST walk the relative path beneath the configured documentation root and pick the first directory segment that contains more than one document as the suite identifier.
- **FR-007**: The system MUST sanitize suite identifiers (replace separators, trim leading/trailing dots) so they are usable as filenames and as `--suite` argument values.
- **FR-008**: The system MUST reject frontmatter-supplied suite identifiers that contain slashes, spaces, or leading dots, and MUST report the offending file path in the error.

#### Exclusion from analyzer input

- **FR-009**: The system MUST support a configurable list of glob patterns whose matched documents are still indexed but whose suites are flagged to be excluded from analyzer-facing prompts.
- **FR-010**: The default exclusion patterns MUST cover archived directories (`Old/`, `legacy/`, `archive/`), release-notes directories (`release-notes/`), changelog files (`CHANGELOG*`), and table-of-contents files (`SUMMARY.md`).
- **FR-011**: A document MUST be able to opt back into analysis by declaring `analyze: true` in its frontmatter, overriding the pattern-based exclusion.
- **FR-012**: Excluded documents MUST still participate in coverage analysis (linked tests, coverage metrics) — the exclusion only applies to AI-analyzer prompt input.

#### Consumers

- **FR-013**: The behavior analyzer MUST load the manifest, resolve which suite(s) are relevant from the user's filter (`--suite`, `--focus`, or no filter), and load only the corresponding suite index files into the prompt.
- **FR-014**: Before sending any analysis request to the AI model, the system MUST estimate total prompt size and, if the estimate exceeds the configured budget, MUST fail fast with an actionable error that names the candidate suites and their token estimates and suggests narrowing the scope.
- **FR-015**: The criteria extractor MUST iterate documents from the manifest (not from the filesystem directly) and MUST skip documents whose suite is flagged as analysis-skipped, unless the user explicitly opts in via a flag.
- **FR-016**: The generation context loader (used when the user describes a test from scratch) MUST resolve the manifest, pick relevant suites by best-effort match against the user's description, and load only those suite index files.
- **FR-017**: The dashboard data collector MUST read top-level statistics from the manifest and MUST lazy-load per-suite index content only when a suite is actually rendered.
- **FR-018**: The documentation coverage analyzer MUST iterate documents from the manifest and MUST treat skip-analysis documents as still requiring coverage.

#### Migration

- **FR-019**: On startup, the system MUST detect the presence of a legacy single-file index and the absence of the new manifest, and MUST automatically run a one-time migration before continuing.
- **FR-020**: Migration MUST parse the legacy file's per-document entries and checksum block, group them by resolved suite, apply default exclusion patterns, and write the new layout.
- **FR-021**: Migration MUST be atomic: on success the legacy file is renamed to `_index.md.bak` and the new layout is in place; on failure the legacy file is untouched and no partial new layout is left behind.
- **FR-022**: Migration MUST be idempotent: subsequent runs after a successful migration MUST NOT re-migrate.
- **FR-023**: Migration MUST log a human-readable summary including total documents migrated, suites created, the largest suite by token cost, and a hint that the `.bak` file can be deleted after verification.
- **FR-024**: A user MUST be able to refuse migration via an explicit flag, in which case the command exits with a clear error describing the legacy file and the flag's effect.

#### CLI surface

- **FR-025**: The existing `spectra docs index` command MUST continue to work without changes to its primary invocation; the new layout is written under the hood.
- **FR-026**: The system MUST support narrowing the indexer to specific suites for incremental re-indexing without rewriting unrelated suite files.
- **FR-027**: The system MUST extend the JSON result of `spectra docs index` to include per-suite breakdowns (id, document count, token estimate, skip flag, index file path), the manifest path, and migration metadata when migration occurred.
- **FR-028**: When the user passes `--suite <id>` to `spectra ai generate` and no doc-suite of that name exists in the manifest, the system MUST emit a warning listing available suites and proceed with full-corpus behavior (still subject to the pre-flight budget check).

#### Introspection (deferrable)

- **FR-029**: The system MUST expose a command to list every suite in the manifest with document count, token estimate, and analysis-skip status, in both human-readable and JSON output formats.
- **FR-030**: The system MUST expose a command to print the contents of a single suite's index file by suite identifier.

### Key Entities

- **Manifest**: The lightweight always-loaded directory of suites. Holds suite identity, location, sizing, and analysis flags. The only index artifact that is sent to AI prompts on every operation.
- **Suite**: A logical grouping of documents derived from the documentation directory structure (with optional overrides). Each suite has a stable identifier, a human-readable title, a source directory path, an analysis-skip flag, and a dedicated index file.
- **Suite Index File**: The per-suite markdown file containing the indexed entries (title, summary, key entities, sections) for documents assigned to that suite.
- **Checksum Store**: A separate machine-readable file mapping document paths to content hashes. Used for incremental-update detection. Never sent to AI prompts.
- **Exclusion Pattern**: A glob expression matched against document paths. Documents whose paths match are still indexed but their containing suite is flagged to be skipped from analyzer-facing input.
- **Document Frontmatter Overrides**: Per-document metadata fields (`suite: <id>`, `analyze: true|false`) that override the default suite assignment and analysis-inclusion behavior.
- **Migration Record**: Metadata produced by the one-time legacy-to-new migration: total documents migrated, suites created, the legacy backup file path, summary statistics. Surfaced in command output and the JSON result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A 541-document project running `spectra ai generate --suite POS_UG_Topics --analyze-only` completes end-to-end with zero retries and a logged analyzer prompt size under 30,000 tokens (down from the current 204,000-token failure).
- **SC-002**: For a typical-sized suite (50–150 documents), the analyzer prompt overhead from index content drops by at least 70% compared to today's single-file index.
- **SC-003**: Indexing the same project completes in no more than 1.5× the wall-clock time of the legacy single-file indexer (small per-file IO overhead is acceptable).
- **SC-004**: Migration of a legacy single-file index to the new layout is fully automatic on first run after upgrade, atomic on failure (legacy file untouched, no partial layout), idempotent on success, and reversible by restoring the preserved backup file.
- **SC-005**: When a user invokes generation in a way that would exceed the configured prompt budget, the command fails with an actionable error naming the available suites and their token costs — never with the raw model-side 400 token-limit error.
- **SC-006**: Documents in default-excluded directories (`Old/`, `legacy/`, `archive/`, `release-notes/`) do not contribute to analyzer prompts on a fresh project unless the user opts in, but those same documents still appear in coverage reports.
- **SC-007**: A user can scope a single re-index to a subset of suites and the command rewrites only those suites' index files plus the manifest and checksum store, leaving unrelated suite files unchanged on disk.

## Assumptions

- Legacy `docs/_index.md` files are auto-generated artifacts, so no user has hand-edits worth preserving; a single-version cutover (with a backup file for safety) is acceptable, mirroring how Spec 026 handled the `docs/requirements/` → `docs/criteria/` rename.
- The default prompt-budget cap (used by the pre-flight check) sits comfortably below the model's 128K context window to leave room for the response, the prompt template, and ancillary content (existing-test frontmatter, coverage snapshot, technique guidance).
- Suite identifiers preserve directory casing (e.g., `SM_GSG_Topics` rather than `sm_gsg_topics`) to match filesystem conventions and existing user habits with the `--suite` flag.
- Documentation suites and test suites are independent concepts: the indexer does not reject a documentation suite identifier that has no matching test suite under `tests/`. Coverage analysis surfaces the mismatch naturally.
- The introspection commands (`list-suites`, `show-suite`) are deferrable to a later phase; the headline bug fix and migration ship without them.
- Per-document spillover (additional fine-grained index files for suites that individually exceed the prompt budget) is included in this spec as a structural design point so the follow-up Spec 041 (iterative behavior analysis) does not need to introduce new file shapes — but Spec 041 itself is out of scope here.

## Dependencies

- Spec 010 (Document Index): the file shape and per-document entry structure of today's `_index.md` is the input to migration.
- Spec 023 (Criteria Extraction Overhaul): provides the proven "small manifest + per-unit files + separate hashes" template used by the criteria index — this spec applies the same shape to the document index.
- Spec 024 (Docs Index SKILL): the JSON result schema this spec extends.
- Spec 026 (Criteria Folder Rename): provides the migration ergonomics (auto-detect, single-version cutover, no breaking change, `.bak` backup).

## Out of Scope

- Iterative per-batch behavior analysis with cross-batch deduplication for suites that individually exceed the prompt budget. That is the subject of follow-up Spec 041, which builds on the manifest-driven loading and spillover format defined here.
- Restructuring the test index (`tests/{suite}/_index.json`) — separate concern, separate spec.
- Changing how generation profiles or prompt templates are stored.
- Pulling documentation indexes from external systems (Confluence, GitBook, Notion) into the manifest as additional suites — potential future spec.
- Surfacing test-suite ↔ documentation-suite linkage in the dashboard — potential future spec.
