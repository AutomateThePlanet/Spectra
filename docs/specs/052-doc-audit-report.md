# Spec 052 — Documentation Audit Report (047–051)

**Date**: 2026-06-03
**Scope**: every documentation file and SKILL/agent file that could describe behavior changed by specs 047–051.
**Method**: each file was read and checked against the post-051 behavior (resilient extraction, default-on criteria warning, from-description index parity + criteria injection, filter schema alignment + actionable errors). Disposition is one of **confirmed-current** (no stale content), **updated** (changed in this PR), or **superseded**.

## Behavior baseline the docs must match

| Spec | Observable behavior docs must reflect |
|------|---------------------------------------|
| 047 | Parse/empty extraction failures are retried and never permanently cached; `docs index` uses a 2-min **per-document** deadline (no 60s corpus-wide abort). |
| 048 | `docs index` emits a non-blocking `criteria_warning` on a zero-criteria corpus; `ai generate` attaches a `notes` entry when no criteria match; `CriteriaSource.outcome` (default `extracted`). |
| 049 | From-description tests are registered in `_index.json` immediately and discoverable by all MCP tools. |
| 050 | From-description generation populates the `criteria:` field when criteria exist. |
| 051 | `start_execution_run` accepts top-level `priorities`/`tags`/`components`; legacy nested `filters` deprecated but honored; misshapen params return an actionable error. |

## Documentation files

| File | Disposition | Notes |
|------|-------------|-------|
| `docs/usage.md` | confirmed-current | 047 resilient extraction (≈L69–71) and 048 guards (≈L72) already described correctly. No 60s/corpus-deadline or manual-extraction language. |
| `docs/coverage.md` | confirmed-current | 047 `CriteriaExtractionResult`/retry/cache + 048 `outcome` field (≈L138–150); 050 from-description criteria (≈L107–114). Coherent. |
| `docs/test-format.md` | confirmed-current | 049 index-population invariant for all generation paths incl. `--from-description` (≈L140–144) present and correct. |
| `docs/cli-reference.md` | confirmed-current | 047 per-document deadline (≈L129–131), 048 `criteria_warning` (≈L130–132), 051 canonical filter shape + `INVALID_PARAMS` (≈L206–215), 048 no-match note (≈L233). All current. |
| `docs/skills-integration.md` | confirmed-current | 050 from-description criteria injection + `manual` verdict rationale (≈L126–130). Coherent. |
| `docs/getting-started.md` | confirmed-current | Installation/setup only; no behavior touched by 047–051. |
| `docs/execution-agent/generic-mcp.md` | confirmed-current | This is the "generic MCP" doc. 051 canonical filter shape, deprecated nested `filters`, and `INVALID_PARAMS`-with-suggestion already documented (≈L97–109). |
| `PROJECT-KNOWLEDGE.md` | **updated** | Spec table (`## Completed Feature Specs`) had no rows for 047–052 — added them. Added the **silent-failure-pattern** learning section. (Test-count table refreshed separately.) |
| `CHANGELOG.md` | **updated** | The changelog jumped from 1.52.0 to 1.48.3 with no 047–051 entries. Added one **consolidated** `[1.52.6]` entry (Fixed / Added / Changed, user-facing, per-spec attribution). |

## SKILL / agent files (`src/Spectra.CLI/Skills/Content/`)

| File | Disposition | Notes |
|------|-------------|-------|
| `Skills/spectra-generate.md` | confirmed-current | Renders the `notes` array (Spec 048, ≈L123); from-description criteria injection + indexing (Spec 050, ≈L148–152). |
| `Skills/spectra-docs.md` | confirmed-current | Surfaces `criteria_warning` verbatim (Spec 048, ≈L52–61); incremental/default-on extraction described. |
| `Skills/spectra-coverage.md` | confirmed-current | CLI wrapper; the `outcome` field semantics live in `coverage.md`, which is current. No stale content. |
| `Skills/spectra-criteria.md` | confirmed-current | Resilient extraction + `--force`/recovery paths; no "run separately after every index" stale wording. |
| `Agents/spectra-execution.agent.md` | confirmed-current | Shows ONE filter shape (top-level `priorities`/`tags`/`components`), explicitly "no nested `filters` object" (Spec 051, ≈L32–34). |
| `Skills/spectra-quickstart.md`, `spectra-list.md`, `spectra-update.md`, `spectra-validate.md`, `spectra-help.md`, `spectra-dashboard.md`, `spectra-delete.md`, `spectra-suite.md`, `spectra-init-profile.md`, `spectra-prompts.md` | confirmed-current | Out of the 047–051 behavior surface; reviewed, no stale references to removed escape hatches or pre-047 behavior. |
| `Agents/spectra-generation.agent.md` | confirmed-current | Delegates to `spectra-generate` SKILL; no independent stale behavior. |

## Outcome

- **2 files updated**: `PROJECT-KNOWLEDGE.md`, `CHANGELOG.md`. Both are actually modified in this PR (FR-022 / SC-005).
- **All other in-scope files confirmed-current**: the per-spec documentation checklists for 047–051 already kept the user-facing docs and SKILLs coherent; this audit verified the *whole narrative* and found no stale pre-047 guidance, no removed-escape-hatch references, and no incoherence between docs describing the same behavior.

## Follow-ups (recorded, not fixed here — out of scope per FR-028)

1. **Command-level agent seam for from-description.** `GenerateHandler.ExecuteFromDescriptionAsync` constructs its agent internally with no injection point, so a fully hermetic test cannot drive the literal `ai generate --from-description` CLI entrypoint; Spec 052's cross-spec tests instead compose the same production services one level below the command (via the `agentFactory` seam on `UserDescribedGenerator.GenerateAsync`). A future spec could add a command-level seam to enable end-to-end CLI invocation in tests.
2. **Two extractor implementations** (`RequirementsExtractor` vs `CriteriaExtractor`) remain unmerged (noted in Spec 047); unrelated to this hardening pass.
3. **PROJECT-KNOWLEDGE test-count table** previously carried two conflicting `Total` rows; refreshed in this PR after the full-suite run.
4. **Test-isolation hardening (applied here, test-only).** `IndexHandlerRebuildTests` mutated the process-wide current directory but was missing the `[Collection("WorkingDirectory")]` attribute that serializes every other CWD-mutating test. It passed in the full run but flaked under a filtered `Category!=Scale` schedule (the fast-feedback command this spec documents). Added the attribute — a test-only isolation fix, not a production change — so the recommended filtered run is reliably green (SC-008).
