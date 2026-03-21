# Data Model: Dashboard Improvements and Cloudflare Pages Deployment

**Date**: 2026-03-21 | **Branch**: `011-dashboard-fixes-deploy`

## Modified Entities

### DashboardConfig (existing — extend)

**Location**: `src/Spectra.Core/Models/Config/DashboardConfig.cs`

| Field | Type | Default | Status |
|-------|------|---------|--------|
| output_dir | string | "./site" | Existing |
| title | string? | null | Existing |
| template_dir | string? | null | Existing |
| include_coverage | bool | true | Existing |
| include_runs | bool | true | Existing |
| max_trend_points | int | 30 | Existing |
| **cloudflare_project_name** | **string** | **"spectra-dashboard"** | **New** |

**Validation**: `cloudflare_project_name` must be a valid Cloudflare Pages project name (lowercase alphanumeric and hyphens, 1-63 chars).

### CoverageTreeNode (new — client-side only)

Used in the D3.js hierarchical tree visualization. Built client-side from existing `DashboardData` JSON.

| Field | Type | Description |
|-------|------|-------------|
| name | string | Display name (folder name, doc filename, component, or test ID + title) |
| type | enum | "domain" / "feature" / "area" / "test" |
| path | string? | Full path for document nodes |
| testCount | number | Aggregate test count for this node and descendants |
| automationPct | number | Percentage of automated tests (0-100) |
| automated | boolean? | For test nodes: has automated_by field |
| automatedBy | string? | For test nodes: automation file path |
| testId | string? | For test nodes: the test case ID |
| children | CoverageTreeNode[] | Child nodes (expandable) |

**Hierarchy construction** (client-side from existing data):
1. Group docs by parent folder → domain nodes
2. Each doc file → feature node
3. Tests linked via source_refs → grouped by component → area nodes
4. Individual tests → leaf test nodes
5. Tests without source_refs → under synthetic "Unlinked Tests" domain node

**Color derivation**:
- Domain/Feature/Area nodes: computed from children's automation status
  - All automated → green
  - Some automated → yellow
  - Tests exist, none automated → orange
  - No tests → red
- Test nodes: green if `automated_by` set, yellow if manual

### Session Token Payload (existing — modify)

**Location**: `dashboard-site/functions/_middleware.js`

| Field | Type | Change |
|-------|------|--------|
| user | string | Existing — GitHub login |
| name | string | Existing — display name |
| avatar | string | Existing — avatar URL |
| exp | number | **Modified** — Unix timestamp, now current_time + 86400 (24h, was 604800/7d) |

### Middleware Environment Variables (existing — modify)

| Variable | Type | Change |
|----------|------|--------|
| AUTH_ENABLED | string | Existing |
| GITHUB_CLIENT_ID | string | Existing |
| GITHUB_CLIENT_SECRET | string | Existing |
| SESSION_SECRET | string | Existing |
| ~~ALLOWED_REPO~~ | ~~string~~ | **Renamed** |
| **ALLOWED_REPOS** | **string** | **New** — comma-separated `org/repo` values, replaces singular ALLOWED_REPO |

**Access check logic change**: Iterate over comma-separated repos, grant access if user has read access to ANY of them.

## Unchanged Entities

These existing entities are consumed but not modified:

- **DashboardData** — root JSON object embedded in dashboard HTML
- **CoverageSummaryData** — three-section coverage (documentation, requirements, automation)
- **SuiteStats** — per-suite statistics
- **TestEntry** — denormalized test with source_refs, component, automated_by
- **DocumentationCoverageDetail** — per-document coverage detail
- **AutomationSectionData** — per-suite automation breakdown
