# Quickstart: Dashboard and Coverage Analysis

**Feature**: 003-dashboard-coverage-analysis
**Date**: 2026-03-15

## Overview

This guide covers how to use the Dashboard Generator and Coverage Analysis features added in Phase 3.

## Prerequisites

- SPECTRA CLI installed (`spectra` command available)
- Repository with test suites (created via `spectra ai generate`)
- Valid `_index.json` files in each suite folder

## Dashboard Generation

### Basic Usage

Generate a static dashboard from your test data:

```bash
spectra dashboard --output ./site
```

This creates a complete static website in `./site/` containing:
- `index.html` - Main dashboard page
- `styles/` - CSS files
- `scripts/` - JavaScript for filtering and visualization

### View Locally

Open the generated dashboard in your browser:

```bash
# macOS
open ./site/index.html

# Linux
xdg-open ./site/index.html

# Windows
start ./site/index.html
```

### Dashboard Features

1. **Suite Browser**: View all test suites with test counts
2. **Test Filtering**: Filter by priority, tags, component
3. **Test Search**: Search by test ID or title
4. **Test Details**: Click any test to see full content
5. **Run History**: View past execution runs
6. **Coverage Map**: Visualize doc→test→automation relationships

## Coverage Analysis

### Basic Usage

Analyze automation coverage for your test suite:

```bash
spectra ai analyze --coverage
```

Output is written to stdout as Markdown.

### Output Formats

```bash
# JSON output to file
spectra ai analyze --coverage --format json --output coverage.json

# Markdown output to file
spectra ai analyze --coverage --format markdown --output coverage.md
```

### What It Reports

1. **Coverage Summary**: Total tests, automated count, percentage
2. **By Suite**: Coverage breakdown per suite
3. **By Component**: Coverage breakdown per component
4. **Unlinked Tests**: Manual tests without automation
5. **Orphaned Automation**: Automation referencing non-existent tests
6. **Broken Links**: Tests referencing non-existent automation files
7. **Link Mismatches**: Inconsistencies between test→automation and automation→test

## Configuration

Add to your `spectra.config.json`:

```json
{
  "dashboard": {
    "output_dir": "./site",
    "title": "My Test Dashboard"
  },
  "coverage": {
    "automation_dirs": ["tests/automation/"],
    "attribute_patterns": [
      "\\[TestCase\\(\"({id})\"\\)\\]",
      "\\[Test\\].*//\\s*({id})"
    ]
  }
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| dashboard.output_dir | `./site` | Output directory for dashboard |
| dashboard.title | `SPECTRA Dashboard` | Dashboard page title |
| coverage.automation_dirs | `["tests/automation/"]` | Directories to scan |
| coverage.attribute_patterns | `[TestCase pattern]` | Regex patterns for test IDs |

### Automation Pattern Syntax

Patterns use regex with `{id}` placeholder for test ID:

```json
"attribute_patterns": [
  "\\[TestCase\\(\"({id})\"\\)\\]",  // C# [TestCase("TC-101")]
  "@Test.*({id})",                    // Java @Test with TC-xxx
  "// Covers: ({id})"                 // Comment-based linking
]
```

## Linking Tests to Automation

### From Test to Automation

Add `automated_by` field to test frontmatter:

```yaml
---
id: TC-101
priority: high
automated_by: tests/automation/PaymentTests.cs
---
```

### From Automation to Test

Add test case attributes in your automation code:

```csharp
[TestCase("TC-101")]
public void CheckoutWithValidVisa()
{
    // ...
}
```

The coverage analysis reconciles both directions and reports mismatches.

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Generate Dashboard

on:
  push:
    paths:
      - 'tests/**'
      - 'reports/**'

jobs:
  dashboard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'

      - name: Install SPECTRA
        run: dotnet tool install -g spectra-cli

      - name: Generate Dashboard
        run: spectra dashboard --output ./site

      - name: Deploy to Pages
        uses: cloudflare/pages-action@v1
        with:
          apiToken: ${{ secrets.CF_API_TOKEN }}
          accountId: ${{ secrets.CF_ACCOUNT_ID }}
          projectName: my-test-dashboard
          directory: ./site
```

### Coverage Analysis in PR

```yaml
- name: Coverage Analysis
  run: |
    spectra ai analyze --coverage --format markdown > coverage.md
    cat coverage.md >> $GITHUB_STEP_SUMMARY
```

## Troubleshooting

### "No test suites found"

Ensure:
1. Tests exist in `tests/` directory
2. Each suite has `_index.json`
3. Run `spectra index` to rebuild indexes

### "No automation directories configured"

Add `coverage.automation_dirs` to config:

```json
{
  "coverage": {
    "automation_dirs": ["tests/automation/", "src/tests/"]
  }
}
```

### Dashboard shows stale data

Regenerate the dashboard:

```bash
spectra dashboard --output ./site
```

Dashboard is static; regenerate when tests or reports change.

## Next Steps

- Configure authentication for hosted dashboard (see User Story 6 in spec)
- Set up automated deployment workflow
- Customize attribute patterns for your test framework
