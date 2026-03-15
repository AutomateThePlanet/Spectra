# Dashboard Data Contract

**Version**: 1.0.0
**Feature**: 003-dashboard-coverage-analysis

## Overview

This document defines the JSON data format embedded in generated dashboard HTML files. The dashboard generator produces this JSON structure, and the client-side JavaScript consumes it for rendering, filtering, and visualization.

## Root Schema: `dashboard-data.json`

```json
{
  "$schema": "dashboard-data.schema.json",
  "version": "1.0.0",
  "generated_at": "2026-03-15T10:00:00Z",
  "repository": "owner/repo",
  "suites": [...],
  "runs": [...],
  "tests": [...],
  "coverage": {...}
}
```

### Top-Level Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| version | string | Yes | Schema version (semver) |
| generated_at | string (ISO 8601) | Yes | Generation timestamp |
| repository | string | Yes | Repository identifier |
| suites | SuiteStats[] | Yes | Suite statistics |
| runs | RunSummary[] | Yes | Execution history |
| tests | TestEntry[] | Yes | All test entries |
| coverage | CoverageData | Yes | Coverage visualization data |

## SuiteStats

```json
{
  "name": "checkout",
  "test_count": 42,
  "by_priority": {
    "high": 15,
    "medium": 20,
    "low": 7
  },
  "by_component": {
    "payment": 18,
    "cart": 12,
    "shipping": 12
  },
  "tags": ["smoke", "regression", "payments"],
  "last_run": { ... },
  "automation_coverage": 65.5
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | Yes | Suite folder name |
| test_count | integer | Yes | Total tests in suite |
| by_priority | object | Yes | Count by priority level |
| by_component | object | Yes | Count by component |
| tags | string[] | Yes | Unique tags in suite |
| last_run | RunSummary | No | Most recent run |
| automation_coverage | number | Yes | Percentage (0-100) |

## TestEntry

```json
{
  "id": "TC-101",
  "suite": "checkout",
  "title": "Checkout with valid Visa card",
  "file": "checkout-happy-path.md",
  "priority": "high",
  "tags": ["smoke", "payments"],
  "component": "payment",
  "source_refs": ["docs/features/checkout/payment-methods.md"],
  "automated_by": "tests/automation/PaymentTests.cs",
  "has_automation": true
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| id | string | Yes | Test ID |
| suite | string | Yes | Parent suite name |
| title | string | Yes | Test title |
| file | string | Yes | Source file path |
| priority | string | Yes | high/medium/low |
| tags | string[] | Yes | Test tags |
| component | string | No | Component under test |
| source_refs | string[] | Yes | Documentation references |
| automated_by | string | No | Automation file path |
| has_automation | boolean | Yes | Computed from automated_by or attribute scan |

## RunSummary

```json
{
  "run_id": "a3f7c291-1234-5678-9abc-def012345678",
  "suite": "checkout",
  "status": "completed",
  "started_at": "2026-03-15T09:00:00Z",
  "completed_at": "2026-03-15T10:30:00Z",
  "started_by": "anton@example.com",
  "duration_seconds": 5400,
  "total": 42,
  "passed": 38,
  "failed": 3,
  "skipped": 1,
  "blocked": 0
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| run_id | string | Yes | Unique identifier |
| suite | string | Yes | Suite executed |
| status | string | Yes | completed/cancelled/abandoned |
| started_at | string (ISO 8601) | Yes | Start timestamp |
| completed_at | string (ISO 8601) | No | End timestamp |
| started_by | string | Yes | Executor identity |
| duration_seconds | integer | No | Total duration |
| total | integer | Yes | Tests in run |
| passed | integer | Yes | Pass count |
| failed | integer | Yes | Fail count |
| skipped | integer | Yes | Skip count |
| blocked | integer | Yes | Blocked count |

## CoverageData

```json
{
  "nodes": [
    {
      "id": "doc:checkout-flow",
      "type": "document",
      "name": "Checkout Flow",
      "path": "docs/features/checkout/checkout-flow.md",
      "status": "covered"
    },
    {
      "id": "test:TC-101",
      "type": "test",
      "name": "Checkout with valid Visa card",
      "path": "tests/checkout/checkout-happy-path.md",
      "status": "covered"
    },
    {
      "id": "auto:PaymentTests",
      "type": "automation",
      "name": "PaymentTests.cs",
      "path": "tests/automation/PaymentTests.cs",
      "status": "covered"
    }
  ],
  "links": [
    {
      "source": "doc:checkout-flow",
      "target": "test:TC-101",
      "type": "document_to_test"
    },
    {
      "source": "test:TC-101",
      "target": "auto:PaymentTests",
      "type": "test_to_automation"
    }
  ]
}
```

### CoverageNode

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| id | string | Yes | Prefixed unique ID |
| type | string | Yes | document/test/automation |
| name | string | Yes | Display name |
| path | string | Yes | File path |
| status | string | Yes | covered/partial/uncovered |

### CoverageLink

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| source | string | Yes | Source node ID |
| target | string | Yes | Target node ID |
| type | string | Yes | Link type |

**Link Types**:
- `document_to_test`: source_refs relationship
- `test_to_automation`: automated_by relationship
- `automation_to_test`: Attribute reference (reverse)

## Embedding in HTML

The dashboard generator embeds this data in the HTML file:

```html
<!DOCTYPE html>
<html>
<head>
  <title>SPECTRA Dashboard</title>
  <link rel="stylesheet" href="styles/main.css">
</head>
<body>
  <div id="app"></div>

  <script id="dashboard-data" type="application/json">
    { /* Full dashboard-data structure */ }
  </script>

  <script src="scripts/app.js"></script>
</body>
</html>
```

Client-side JavaScript loads data via:
```javascript
const data = JSON.parse(
  document.getElementById('dashboard-data').textContent
);
```

## Versioning

- **Major**: Breaking changes to required fields
- **Minor**: New optional fields added
- **Patch**: Documentation/description updates

Clients MUST check `version` field and handle unknown fields gracefully.
