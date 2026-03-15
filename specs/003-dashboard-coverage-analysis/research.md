# Research: Dashboard and Coverage Analysis

**Feature**: 003-dashboard-coverage-analysis
**Date**: 2026-03-15

## Research Tasks

### 1. Static Site Generation Approach

**Question**: How should the dashboard generate static HTML from test data?

**Decision**: Template-based generation with embedded HTML templates

**Rationale**:
- Simple string interpolation with HTML templates keeps dependencies minimal
- No build toolchain required (no Node.js, no bundler)
- Templates can be embedded as resources or copied from a `dashboard-site/` directory
- Client-side JavaScript handles filtering/search (data embedded as JSON in HTML)

**Alternatives Considered**:
- Razor templates: Adds ASP.NET Core dependency for static generation (overkill)
- React/Next.js build: Requires Node.js toolchain, complicates CLI distribution
- Pure JSON + separate HTML: Simpler but requires hosting both files together

### 2. Dashboard Data Format

**Question**: How should test and execution data be structured for the dashboard?

**Decision**: Single `dashboard-data.json` file embedded in generated HTML

**Rationale**:
- Single file simplifies deployment (just copy the output directory)
- JSON embedded as `<script>` tag avoids CORS issues when opening locally
- Client-side JavaScript can filter/search without server
- Keeps page load fast for typical repository sizes (<500 tests)

**Alternatives Considered**:
- Multiple JSON files per suite: More complex, requires fetch() calls
- SQLite in browser (sql.js): Heavy dependency, overkill for read-only browsing
- Server-side rendering: Violates static-site constraint

### 3. Coverage Visualization Library

**Question**: What approach for the coverage mind map visualization?

**Decision**: D3.js tree/hierarchy visualization

**Rationale**:
- D3.js is the industry standard for data visualization
- Tree layout naturally represents doc → test → automation hierarchy
- Can be included via CDN or bundled (single file)
- Extensive documentation and examples available

**Alternatives Considered**:
- Mermaid.js: Good for diagrams but limited interactivity
- Vis.js: Good network graphs but tree hierarchy less natural
- Custom SVG: More work, reinventing the wheel

### 4. Reading .execution Database

**Question**: How to read SQLite data from the MCP execution engine?

**Decision**: Use Microsoft.Data.Sqlite directly (already a dependency in Spectra.MCP)

**Rationale**:
- Spectra.MCP already uses Microsoft.Data.Sqlite
- Can reuse connection patterns from existing Storage classes
- Dashboard generation reads runs/results tables directly
- Falls back gracefully if .execution directory doesn't exist

**Alternatives Considered**:
- Expose API in Spectra.MCP: Adds coupling, MCP may not be running
- Copy SQLite file: Unnecessary complexity
- Only read exported reports: Misses runs that weren't finalized

### 5. Automation Code Scanning Pattern

**Question**: How to find test case references in automation code efficiently?

**Decision**: Regex-based scanning with configurable patterns

**Rationale**:
- Simple regex handles common patterns: `[TestCase("TC-xxx")]`, `// TC-xxx`, `@Test("TC-xxx")`
- Configuration allows teams to customize for their frameworks
- File scanning is fast for typical automation codebases (<10k files)
- No need for full AST parsing (too complex for marginal benefit)

**Pattern Configuration**:
```json
{
  "coverage": {
    "automation_dirs": ["tests/automation/"],
    "attribute_patterns": [
      "\\[TestCase\\(\"({id})\"\\)\\]",
      "\\[TestCaseSource\\(.*({id}).*\\)\\]",
      "@Test.*({id})",
      "// Covers: ({id})"
    ]
  }
}
```

**Alternatives Considered**:
- Roslyn analysis: C#-specific, heavy dependency, slow
- Tree-sitter: Multi-language but complex setup
- Simple string search: Too many false positives

### 6. Link Reconciliation Logic

**Question**: How to reconcile bidirectional automation links?

**Decision**: Build maps from both directions, compute set differences

**Rationale**:
- Build `testId → automationFile` from `automated_by` fields
- Build `automationFile → testIds` from attribute scanning
- Cross-reference to find:
  - Unlinked: tests with no entry in either direction
  - Orphaned: automation files with no matching test
  - Mismatches: links in one direction but not the other
  - Broken: referenced files that don't exist

**Algorithm**:
```
1. Parse all test indexes → Map<TestId, AutomatedByPath?>
2. Scan automation dirs → Map<FilePath, Set<TestId>>
3. Invert automation map → Map<TestId, Set<FilePath>>
4. For each test:
   - If no automated_by AND not in automation map → Unlinked
   - If automated_by but file doesn't exist → Broken
   - If automated_by but automation doesn't reference back → Mismatch
5. For each automation file:
   - If references tests that don't exist → Orphaned
```

### 7. Authentication Approach (P2)

**Question**: How to implement optional authentication for hosted dashboard?

**Decision**: Cloudflare Pages Functions with GitHub OAuth

**Rationale**:
- Cloudflare Pages is free for static sites with edge functions
- GitHub OAuth checks repository access natively
- No server to maintain; functions run at edge
- Configuration via environment variables

**Flow**:
1. Middleware intercepts requests
2. Checks for valid session cookie
3. If missing, redirects to GitHub OAuth
4. On callback, verifies user has read access to configured repo
5. Sets session cookie, redirects to dashboard

**Alternatives Considered**:
- Netlify Functions: Similar but less GitHub-native
- AWS Lambda@Edge: More complex setup
- HTTP Basic Auth: No granular repo access checking
- No auth (public): Doesn't meet security requirement

### 8. Report Output Formats

**Question**: What structure for coverage analysis reports?

**Decision**: Consistent schema for both JSON and Markdown

**JSON Structure**:
```json
{
  "generated_at": "2026-03-15T10:00:00Z",
  "summary": {
    "total_tests": 100,
    "automated": 60,
    "manual_only": 35,
    "broken_links": 3,
    "orphaned_automation": 2,
    "coverage_percentage": 60.0
  },
  "by_suite": [...],
  "by_component": [...],
  "unlinked_tests": [...],
  "orphaned_automation": [...],
  "broken_links": [...],
  "mismatches": [...]
}
```

**Markdown Structure**:
```markdown
# Coverage Analysis Report
Generated: 2026-03-15

## Summary
- Total Tests: 100
- Automated: 60 (60%)
- Manual Only: 35
- Issues: 5 (3 broken links, 2 orphaned)

## By Suite
| Suite | Tests | Automated | Coverage |
|-------|-------|-----------|----------|
...

## Issues
### Unlinked Tests
...
```

## Dependencies Confirmed

| Dependency | Version | Purpose | Already Used |
|------------|---------|---------|--------------|
| Microsoft.Data.Sqlite | 8.0.x | Read .execution DB | Yes (Spectra.MCP) |
| System.Text.Json | 8.0.x | JSON parsing/generation | Yes (Core) |
| Markdig | 0.34.x | Render test Markdown | Yes (Core) |
| D3.js | 7.x | Coverage visualization | No (CDN include) |

## Configuration Extensions

Add to `spectra.config.json`:

```json
{
  "dashboard": {
    "output_dir": "./site",
    "title": "SPECTRA Dashboard",
    "auth": {
      "enabled": false,
      "provider": "github",
      "allowed_repos": []
    }
  },
  "coverage": {
    "automation_dirs": ["tests/automation/"],
    "attribute_patterns": ["\\[TestCase\\(\"({id})\"\\)\\]"]
  }
}
```
