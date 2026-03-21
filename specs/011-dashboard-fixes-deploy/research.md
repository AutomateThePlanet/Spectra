# Research: Dashboard Improvements and Cloudflare Pages Deployment

**Date**: 2026-03-21 | **Branch**: `011-dashboard-fixes-deploy`

## R1: Coverage Analyzer Fragment Bug — Root Cause

**Decision**: Fix path normalization in DataCollector.BuildCoverageSummaryAsync() and GapAnalyzer.NormalizePath()

**Rationale**: Investigation revealed two separate issues:

1. **DocumentationCoverageAnalyzer.cs** (Spectra.Core) — Already correctly strips fragment anchors via `StripFragment()` method (line 23, 54-58). Unit tests confirm this works (`Analyze_SourceRefsWithFragmentAnchors_MatchesDocPath`).

2. **DataCollector.cs** (Spectra.CLI) — Strips fragments on line 646, but the dictionary lookup on line 664 uses exact string matching between the stored path and the filesystem-derived relative path. Path normalization differences (slash direction, leading slashes, case) cause the lookup to fail silently, resulting in 0% documentation coverage in the dashboard.

3. **GapAnalyzer.cs** (Spectra.CLI) — `NormalizePath()` helper (lines 273-276) normalizes slashes but does NOT strip fragment anchors, causing gap analysis to also miss matches.

**Fix approach**:
- Create a shared `PathNormalizer` utility that strips fragments AND normalizes slashes/casing
- Apply consistently in DataCollector, GapAnalyzer, and anywhere else source_refs are compared to filesystem paths
- Use `StringComparer.OrdinalIgnoreCase` for dictionary lookups

**Alternatives considered**:
- Fix only DataCollector inline — rejected because GapAnalyzer has the same bug, indicating a systemic issue needing a shared solution
- Normalize at parse time (when reading frontmatter) — rejected because fragment anchors carry semantic meaning for documentation cross-references

## R2: Treemap Data Source Mismatch

**Decision**: Fix treemap to read automation percentages from `coverageSummary.automation.details` using normalized suite name matching

**Rationale**: The treemap code in `coverage-map.js` (lines 10-28) reads suite data from `window.dashboardData.suites` and automation percentages from `coverageSummary.automation.details`. The mapping uses `d.suite.toLowerCase()` as the key in `autoPctMap`, but the suite names from `automation.details` may not match the suite names from `suites` due to casing or formatting differences. When no match is found, `autoPctMap[s.name.toLowerCase()] ?? 0` defaults to 0, making all suites appear red.

**Fix approach**: Normalize both the keys from `automation.details` and the lookup from `suites` to use consistent lowercase trimmed names. Add debug logging if no match is found.

**Alternatives considered**:
- Pre-compute treemap data server-side — rejected as over-engineering; the client-side join just needs consistent keys

## R3: Coverage Tree Visualization

**Decision**: Enhance existing hierarchical tree implementation in app.js rather than building a new D3.js force-directed replacement

**Rationale**: Investigation revealed the dashboard already has a hierarchical tree implementation (app.js lines 1006-1120) with expand/collapse, domain→feature→area→test hierarchy. The spec describes a "force-directed graph" as the current state, but the codebase already migrated to a tree layout. The existing tree needs enhancement:
- Add D3.js horizontal tree layout with proper spacing (current tree is CSS-based, not D3)
- Add zoom/pan support via D3.zoom
- Add document node badges (test count, automation %, progress bar)
- Add "Unlinked Tests" group
- Add Expand All / Collapse All buttons

**Alternatives considered**:
- Full D3 force-directed to tree rewrite — partially done already; enhance what exists
- Keep CSS-based tree and just add badges — rejected because D3 tree provides better layout control for hundreds of nodes

## R4: OAuth Middleware State

**Decision**: Update existing middleware to support ALLOWED_REPOS (plural, comma-separated) and reduce session duration to 24 hours

**Rationale**: The middleware (`functions/_middleware.js`) is functionally complete with proper HMAC-SHA256 signing, GitHub OAuth flow, repo access checking, and session cookies. Two changes needed:
- `ALLOWED_REPO` (singular) → `ALLOWED_REPOS` (comma-separated) with iteration over each repo
- Session duration from 7 days (604,800s) → 24 hours (86,400s)

The callback handler (`functions/auth/callback.js`) is a placeholder — all logic is already in `_middleware.js`.

**Alternatives considered**:
- Add a separate auth service — rejected per YAGNI; middleware is the right pattern for Cloudflare Pages Functions

## R5: Dashboard Config Extension

**Decision**: Add `cloudflare_project_name` to existing DashboardConfig model

**Rationale**: DashboardConfig already has `output_dir`, `title`, `template_dir`, `include_coverage`, `include_runs`, `max_trend_points`. Only `cloudflare_project_name` is missing. Default: `"spectra-dashboard"`.

**Alternatives considered**:
- Separate deployment config section — rejected; this is dashboard-specific and belongs in the dashboard section

## R6: Init Handler Extension

**Decision**: Add workflow file creation to InitHandler after existing GitHub integration file creation

**Rationale**: InitHandler already creates `.github/skills/` and `.github/agents/` files (lines 61-68, 589-595). Adding `.github/workflows/deploy-dashboard.yml` follows the same pattern. A template already exists at `.github/workflows/dashboard.yml.template`.

**Alternatives considered**:
- Separate `spectra deploy init` command — rejected per YAGNI; init is the right place for scaffolding

## R7: Dashboard Existing UI State

**Decision**: Several UI fixes the spec describes may already be partially implemented; verify each during implementation

**Rationale**: Research found:
- Trend chart SVG already uses `h = 220` and has single-point compact fallback (app.js lines 451-469)
- Sidebar hiding on coverage tab already exists (app.js lines 176-183)
- These may be working but with CSS issues, or may be recent additions that aren't fully functional

**Approach**: During implementation, verify each bug fix against the actual rendered output before making changes. Some "fixes" may be CSS-only adjustments rather than logic changes.
