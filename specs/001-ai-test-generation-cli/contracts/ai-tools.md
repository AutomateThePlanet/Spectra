# AI Tool Contracts

**Date**: 2026-03-13
**Feature**: 001-ai-test-generation-cli

This document defines the tool interfaces exposed to the AI agent via the Copilot SDK.

---

## Source Navigation Tools

### get_document_map

Returns a lightweight listing of all documentation files.

**Parameters**: None

**Returns**:
```json
{
  "doc_count": 12,
  "total_size_kb": 340,
  "documents": [
    {
      "path": "docs/features/checkout/checkout-flow.md",
      "title": "Checkout Flow",
      "size_kb": 28,
      "headings": ["Overview", "Happy Path", "Error Handling", "Edge Cases"],
      "first_200_chars": "The checkout flow handles..."
    }
  ]
}
```

---

### load_source_document

Loads the full content of a specific documentation file.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `path` | string | yes | Path relative to docs/ |

**Returns**:
```json
{
  "path": "docs/features/checkout/checkout-flow.md",
  "content": "# Checkout Flow\n\n## Overview\n...",
  "size_kb": 28,
  "truncated": false
}
```

If file exceeds `max_file_size_kb`:
```json
{
  "path": "docs/api/full-reference.md",
  "content": "# API Reference\n\n## Authentication\n... [truncated]",
  "size_kb": 120,
  "truncated": true,
  "truncated_at_kb": 50
}
```

**Errors**:
```json
{
  "error": "FILE_NOT_FOUND",
  "message": "Document not found: docs/features/invalid.md"
}
```

---

### search_source_docs

Keyword search across document titles and headings.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | string | yes | Search keywords |
| `limit` | int | no | Max results (default: 10) |

**Returns**:
```json
{
  "query": "payment",
  "results": [
    {
      "path": "docs/features/checkout/payment-methods.md",
      "title": "Payment Methods",
      "match_type": "title",
      "relevance": 0.95
    },
    {
      "path": "docs/features/checkout/checkout-flow.md",
      "title": "Checkout Flow",
      "match_type": "heading",
      "matched_heading": "Payment Processing",
      "relevance": 0.78
    }
  ]
}
```

---

## Test Index Tools

### read_test_index

Returns the `_index.json` metadata for a suite.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `suite` | string | yes | Suite name |

**Returns**:
```json
{
  "suite": "checkout",
  "generated_at": "2026-03-13T10:00:00Z",
  "test_count": 42,
  "tests": [
    {
      "id": "TC-101",
      "file": "checkout-happy-path.md",
      "title": "Checkout with valid Visa card",
      "priority": "high",
      "tags": ["smoke", "payments"],
      "component": "checkout",
      "depends_on": null,
      "source_refs": ["docs/features/checkout/checkout-flow.md"]
    }
  ]
}
```

**Errors**:
```json
{
  "error": "SUITE_NOT_FOUND",
  "message": "Suite 'invalid' does not exist"
}
```

---

### batch_read_tests

Returns full content of all tests in a suite (or chunk for large suites).

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `suite` | string | yes | Suite name |
| `offset` | int | no | Start index (default: 0) |
| `limit` | int | no | Max tests (default: 30) |

**Returns**:
```json
{
  "suite": "checkout",
  "total": 42,
  "offset": 0,
  "limit": 30,
  "returned": 30,
  "has_more": true,
  "tests": [
    {
      "id": "TC-101",
      "file": "checkout-happy-path.md",
      "content": "---\nid: TC-101\npriority: high\n...",
      "metadata": {
        "priority": "high",
        "tags": ["smoke", "payments"],
        "source_refs": ["docs/features/checkout/checkout-flow.md"]
      }
    }
  ]
}
```

---

### get_next_test_ids

Allocates N sequential test IDs for a suite.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `suite` | string | yes | Suite name |
| `count` | int | yes | Number of IDs to allocate |

**Returns**:
```json
{
  "suite": "checkout",
  "ids": ["TC-143", "TC-144", "TC-145", "TC-146", "TC-147"],
  "next_available": "TC-148"
}
```

---

### check_duplicates_batch

Checks an array of test summaries against existing tests for duplicates.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `suite` | string | yes | Suite name |
| `tests` | array | yes | Array of test summaries |

Test summary format:
```json
{
  "id": "TC-143",
  "title": "Checkout with expired card",
  "steps_summary": "Navigate to checkout, enter expired card, click pay"
}
```

**Returns**:
```json
{
  "checked": 5,
  "duplicates": 1,
  "results": [
    {
      "id": "TC-143",
      "is_duplicate": false
    },
    {
      "id": "TC-144",
      "is_duplicate": true,
      "similar_to": "TC-108",
      "similarity": 0.85,
      "reason": "Title and steps closely match existing test"
    }
  ]
}
```

---

## Write Tools

### batch_write_tests

Submits a batch of generated test cases for validation and writing.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `suite` | string | yes | Target suite |
| `tests` | array | yes | Array of test cases |

Test case format:
```json
{
  "id": "TC-143",
  "priority": "high",
  "tags": ["payments", "negative"],
  "component": "checkout",
  "source_refs": ["docs/features/checkout/payment-methods.md"],
  "title": "Checkout with expired card",
  "preconditions": "User is logged in, cart has items",
  "steps": [
    "Navigate to checkout",
    "Enter expired card details (exp: 01/2020)",
    "Click 'Pay Now'"
  ],
  "expected_result": "Payment is rejected with 'Card expired' error",
  "test_data": "Card: 4111 1111 1111 1111, Exp: 01/2020"
}
```

**Returns**:
```json
{
  "submitted": 12,
  "valid": 10,
  "duplicates": 1,
  "invalid": 1,
  "details": [
    { "id": "TC-143", "status": "valid" },
    { "id": "TC-144", "status": "valid" },
    { "id": "TC-145", "status": "duplicate", "similar_to": "TC-108" },
    { "id": "TC-146", "status": "invalid", "reason": "Missing expected result" }
  ]
}
```

**Notes**:
- Tests are validated but NOT written to disk yet
- The CLI presents results for user review
- Only after user acceptance are files written

---

### batch_propose_updates

Submits a batch of update proposals for existing tests.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `suite` | string | yes | Target suite |
| `updates` | array | yes | Array of update proposals |

Update proposal format:
```json
{
  "id": "TC-105",
  "classification": "OUTDATED",
  "reason": "Documentation changed: checkout flow now includes address validation step",
  "changes": {
    "steps": {
      "old": ["Navigate to checkout", "Enter payment", "Click Pay"],
      "new": ["Navigate to checkout", "Enter address", "Enter payment", "Click Pay"]
    },
    "expected_result": {
      "old": "Order confirmed",
      "new": "Order confirmed with address validation success"
    }
  }
}
```

Classification values:
- `UP_TO_DATE`: No changes needed
- `OUTDATED`: Documentation changed, test needs update
- `ORPHANED`: Source documentation removed
- `REDUNDANT`: Duplicates another test

**Returns**:
```json
{
  "proposed": 4,
  "updates": [
    { "id": "TC-105", "classification": "OUTDATED", "accepted": true },
    { "id": "TC-108", "classification": "OUTDATED", "accepted": true },
    { "id": "TC-112", "classification": "OUTDATED", "accepted": true },
    { "id": "TC-115", "classification": "ORPHANED", "suggested_action": "delete" }
  ]
}
```

---

## Error Handling

All tools return errors in a consistent format:

```json
{
  "error": "ERROR_CODE",
  "message": "Human-readable description",
  "details": { ... }
}
```

Common error codes:
| Code | Description |
|------|-------------|
| `FILE_NOT_FOUND` | Requested file does not exist |
| `SUITE_NOT_FOUND` | Requested suite does not exist |
| `VALIDATION_ERROR` | Test case failed validation |
| `DUPLICATE_ID` | Test ID already exists |
| `LOCKED` | Suite is locked by another process |
| `PERMISSION_DENIED` | Cannot write to target location |

---

## Tool Registration

Tools are registered via Microsoft.Extensions.AI:

```csharp
var tools = new[]
{
    AIFunctionFactory.Create(GetDocumentMap, "get_document_map",
        "Lightweight listing of all docs (paths, titles, headings, sizes)"),

    AIFunctionFactory.Create(LoadSourceDocumentAsync, "load_source_document",
        "Full content of a specific doc (capped at max_file_size_kb)"),

    AIFunctionFactory.Create(SearchSourceDocsAsync, "search_source_docs",
        "Keyword search across doc titles and headings"),

    AIFunctionFactory.Create(ReadTestIndexAsync, "read_test_index",
        "Returns _index.json metadata for a suite"),

    AIFunctionFactory.Create(BatchReadTestsAsync, "batch_read_tests",
        "Full content of all tests in a suite (or chunk)"),

    AIFunctionFactory.Create(GetNextTestIdsAsync, "get_next_test_ids",
        "Allocates N sequential test IDs"),

    AIFunctionFactory.Create(CheckDuplicatesBatchAsync, "check_duplicates_batch",
        "Checks array of titles/steps against index"),

    AIFunctionFactory.Create(BatchWriteTestsAsync, "batch_write_tests",
        "Submits batch of new tests; returns validation results"),

    AIFunctionFactory.Create(BatchProposeUpdatesAsync, "batch_propose_updates",
        "Submits batch of update proposals for existing tests")
};
```
