# Contract: Dashboard Coverage Data

**Feature**: 009-coverage-dashboard-viz | **Date**: 2026-03-20

## Overview

The dashboard reads coverage data from an embedded JSON block (`<script id="dashboard-data">`). This contract defines the `coverage_summary` shape that the CLI's `DataCollector` produces and the dashboard JavaScript consumes.

## JSON Schema

```json
{
  "coverage_summary": {
    "documentation": {
      "covered": 3,
      "total": 4,
      "percentage": 75.00,
      "details": [
        {
          "doc": "docs/authentication.md",
          "test_count": 28,
          "covered": true,
          "test_ids": ["TC-001", "TC-002"]
        },
        {
          "doc": "docs/admin.md",
          "test_count": 0,
          "covered": false,
          "test_ids": []
        }
      ]
    },
    "requirements": {
      "covered": 3,
      "total": 5,
      "percentage": 60.00,
      "has_requirements_file": true,
      "details": [
        {
          "id": "REQ-042",
          "title": "Payment with expired card must be rejected",
          "tests": ["TC-134"],
          "covered": true
        },
        {
          "id": "REQ-043",
          "title": "Expired card error message",
          "tests": [],
          "covered": false
        }
      ]
    },
    "automation": {
      "covered": 12,
      "total": 40,
      "percentage": 30.00,
      "details": [
        {
          "suite": "authentication",
          "total": 28,
          "automated": 5,
          "percentage": 17.86
        },
        {
          "suite": "checkout",
          "total": 12,
          "automated": 7,
          "percentage": 58.33
        }
      ],
      "unlinked_tests": [
        {
          "test_id": "TC-100",
          "suite": "authentication",
          "title": "Login with valid credentials",
          "priority": "high"
        }
      ]
    }
  }
}
```

## Field Contracts

### All sections

| Field | Type | Nullable | Contract |
|-------|------|----------|----------|
| `covered` | integer | No | >= 0, <= total |
| `total` | integer | No | >= 0 |
| `percentage` | decimal | No | 0.00–100.00, two decimal places |

### Documentation details

| Field | Type | Contract |
|-------|------|----------|
| `doc` | string | Relative path from repo root |
| `test_count` | integer | >= 0 |
| `covered` | boolean | true if test_count > 0 |
| `test_ids` | string[] | Sorted by TC number, may be empty |

### Requirements details

| Field | Type | Contract |
|-------|------|----------|
| `id` | string | Requirement ID (e.g., "REQ-042") |
| `title` | string | From YAML file or empty string if discovered from tests |
| `tests` | string[] | Test IDs covering this requirement, sorted |
| `covered` | boolean | true if tests.length > 0 |

### Automation details

| Field | Type | Contract |
|-------|------|----------|
| `suite` | string | Suite directory name |
| `total` | integer | Total tests in suite |
| `automated` | integer | Tests with automated_by, <= total |
| `percentage` | decimal | 0.00–100.00 |

### Unlinked tests

| Field | Type | Contract |
|-------|------|----------|
| `test_id` | string | Test ID |
| `suite` | string | Suite directory name |
| `title` | string | Test title from _index.json |
| `priority` | string | "high", "medium", or "low" |

## Backward Compatibility

- If `coverage_summary` is null/missing, the dashboard falls back to computing from `coverage.nodes` and `coverage.links` (legacy path, already implemented).
- New detail arrays are additive — old dashboards that don't read them continue to work.

## Consumer: Dashboard JavaScript

The dashboard reads `coverage_summary` in `renderCoverage()` (`app.js` line 741). Each section's `details` array is iterated in `renderThreeSectionCoverage()` to build detail lists.

## Producer: DataCollector (C#)

`DataCollector.BuildCoverageSummaryAsync()` in `src/Spectra.CLI/Dashboard/DataCollector.cs` populates `CoverageSummaryData` which is serialized via System.Text.Json into the dashboard JSON payload.
