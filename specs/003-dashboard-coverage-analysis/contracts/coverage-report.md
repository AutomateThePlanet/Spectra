# Coverage Report Contract

**Version**: 1.0.0
**Feature**: 003-dashboard-coverage-analysis

## Overview

This document defines the output format for the `spectra ai analyze --coverage` command. The report is produced in both JSON (for tooling) and Markdown (for humans).

## JSON Schema: `coverage-report.json`

```json
{
  "version": "1.0.0",
  "generated_at": "2026-03-15T10:00:00Z",
  "summary": {
    "total_tests": 100,
    "automated": 60,
    "manual_only": 35,
    "coverage_percentage": 60.0,
    "issues": {
      "broken_links": 3,
      "orphaned_automation": 2,
      "mismatches": 0
    }
  },
  "by_suite": [...],
  "by_component": [...],
  "unlinked_tests": [...],
  "orphaned_automation": [...],
  "broken_links": [...],
  "mismatches": [...]
}
```

## Top-Level Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| version | string | Yes | Schema version |
| generated_at | string (ISO 8601) | Yes | When analysis ran |
| summary | CoverageSummary | Yes | Aggregate stats |
| by_suite | SuiteCoverage[] | Yes | Per-suite breakdown |
| by_component | ComponentCoverage[] | Yes | Per-component breakdown |
| unlinked_tests | UnlinkedTest[] | Yes | Tests without automation |
| orphaned_automation | OrphanedAutomation[] | Yes | Orphaned auto files |
| broken_links | BrokenLink[] | Yes | Invalid references |
| mismatches | LinkMismatch[] | Yes | Inconsistent links |

## CoverageSummary

```json
{
  "total_tests": 100,
  "automated": 60,
  "manual_only": 35,
  "coverage_percentage": 60.0,
  "issues": {
    "broken_links": 3,
    "orphaned_automation": 2,
    "mismatches": 0
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| total_tests | integer | Total manual tests found |
| automated | integer | Tests with valid automation link |
| manual_only | integer | Tests without automation |
| coverage_percentage | number | (automated / total) * 100 |
| issues.broken_links | integer | Count of broken links |
| issues.orphaned_automation | integer | Count of orphaned files |
| issues.mismatches | integer | Count of link mismatches |

## SuiteCoverage

```json
{
  "suite": "checkout",
  "total": 42,
  "automated": 30,
  "manual_only": 12,
  "coverage_percentage": 71.4
}
```

## ComponentCoverage

```json
{
  "component": "payment",
  "total": 25,
  "automated": 20,
  "manual_only": 5,
  "coverage_percentage": 80.0
}
```

## UnlinkedTest

Tests that have no automation (neither `automated_by` nor attribute reference).

```json
{
  "test_id": "TC-105",
  "suite": "checkout",
  "title": "Checkout with expired card",
  "priority": "high",
  "component": "payment"
}
```

## OrphanedAutomation

Automation files that reference test IDs that don't exist.

```json
{
  "file": "tests/automation/OldPaymentTests.cs",
  "referenced_ids": ["TC-999", "TC-998"],
  "line_numbers": [45, 67]
}
```

## BrokenLink

Tests with `automated_by` pointing to non-existent files.

```json
{
  "test_id": "TC-110",
  "suite": "checkout",
  "automated_by": "tests/automation/DeletedTests.cs",
  "reason": "File not found"
}
```

## LinkMismatch

Inconsistencies between test→automation and automation→test links.

```json
{
  "test_id": "TC-115",
  "test_automated_by": "tests/automation/PaymentTests.cs",
  "automation_references": [],
  "issue": "Test claims automation but automation doesn't reference back"
}
```

Or the reverse:

```json
{
  "test_id": "TC-116",
  "test_automated_by": null,
  "automation_references": ["tests/automation/PaymentTests.cs:89"],
  "issue": "Automation references test but test has no automated_by"
}
```

## Markdown Output Format

```markdown
# Coverage Analysis Report

**Generated**: 2026-03-15T10:00:00Z

## Summary

| Metric | Value |
|--------|-------|
| Total Tests | 100 |
| Automated | 60 (60.0%) |
| Manual Only | 35 |
| Issues Found | 5 |

## Coverage by Suite

| Suite | Total | Automated | Coverage |
|-------|-------|-----------|----------|
| checkout | 42 | 30 | 71.4% |
| auth | 33 | 25 | 75.8% |
| orders | 25 | 5 | 20.0% |

## Coverage by Component

| Component | Total | Automated | Coverage |
|-----------|-------|-----------|----------|
| payment | 25 | 20 | 80.0% |
| cart | 15 | 10 | 66.7% |
| shipping | 10 | 5 | 50.0% |

## Issues

### Unlinked Tests (35)

Tests without any automation:

| Test ID | Suite | Title | Priority |
|---------|-------|-------|----------|
| TC-105 | checkout | Checkout with expired card | high |
| TC-106 | checkout | Checkout with insufficient funds | medium |
...

### Broken Links (3)

Tests referencing non-existent automation:

| Test ID | automated_by | Reason |
|---------|--------------|--------|
| TC-110 | tests/automation/DeletedTests.cs | File not found |
...

### Orphaned Automation (2)

Automation referencing non-existent tests:

| File | Referenced IDs |
|------|----------------|
| tests/automation/OldPaymentTests.cs | TC-999, TC-998 |
...

### Link Mismatches (0)

None found.
```

## CLI Usage

```bash
# JSON output (default)
spectra ai analyze --coverage --format json --output coverage.json

# Markdown output
spectra ai analyze --coverage --format markdown --output coverage.md

# Console output (markdown to stdout)
spectra ai analyze --coverage
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Analysis complete, no issues found |
| 0 | Analysis complete, issues found (report includes them) |
| 1 | Analysis failed (invalid config, no tests found) |

Note: Finding coverage issues is not an error; the report documents them.
