# MCP Tool Contracts: Data Tools

**Branch**: `007-execution-agent-mcp-tools` | **Date**: 2026-03-19 | **Status**: Draft

## Tool: `validate_tests`

Validates test files against the SPECTRA schema.

### Parameters

```json
{
  "type": "object",
  "properties": {
    "suite": {
      "type": "string",
      "description": "Suite name to validate (optional, validates all if omitted)"
    }
  },
  "required": []
}
```

### Response

**Success:**
```json
{
  "data": {
    "is_valid": true,
    "total_files": 42,
    "valid_files": 42,
    "errors": [],
    "warnings": [
      {
        "code": "NO_STEPS",
        "message": "Test has no steps defined",
        "file_path": "tests/auth/tc-105.md",
        "test_id": "TC-105"
      }
    ]
  },
  "error": null
}
```

**With Errors:**
```json
{
  "data": {
    "is_valid": false,
    "total_files": 42,
    "valid_files": 40,
    "errors": [
      {
        "code": "MISSING_ID",
        "message": "Test file missing required 'id' field in frontmatter",
        "file_path": "tests/auth/login-test.md",
        "line_number": 1,
        "field_name": "id",
        "test_id": null
      },
      {
        "code": "INVALID_PRIORITY",
        "message": "Priority 'critical' is not valid. Must be: high, medium, low",
        "file_path": "tests/auth/tc-106.md",
        "line_number": 3,
        "field_name": "priority",
        "test_id": "TC-106"
      }
    ],
    "warnings": []
  },
  "error": null
}
```

**Tool Error:**
```json
{
  "data": null,
  "error": {
    "code": "SUITE_NOT_FOUND",
    "message": "Suite 'checkout' not found in tests/ directory"
  }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| MISSING_ID | Required 'id' field not present in frontmatter |
| INVALID_ID_FORMAT | ID does not match configured pattern (default: TC-XXX) |
| MISSING_TITLE | No H1 heading found in test file |
| MISSING_EXPECTED_RESULT | Required expected result section not found |
| INVALID_PRIORITY | Priority value not in allowed enum |
| YAML_PARSE_ERROR | YAML frontmatter syntax error |
| DUPLICATE_ID | Same test ID exists in another file |

### Warning Codes

| Code | Description |
|------|-------------|
| NO_STEPS | Test has no numbered steps defined |
| TOO_MANY_STEPS | Step count exceeds configured maximum |
| NO_TAGS | Test has no tags for categorization |
| LONG_TITLE | Title exceeds 100 characters |

---

## Tool: `rebuild_indexes`

Regenerates `_index.json` files from test files on disk.

### Parameters

```json
{
  "type": "object",
  "properties": {
    "suite": {
      "type": "string",
      "description": "Suite name to rebuild (optional, rebuilds all if omitted)"
    }
  },
  "required": []
}
```

### Response

**Success:**
```json
{
  "data": {
    "suites_processed": 3,
    "tests_indexed": 67,
    "files_added": 5,
    "files_removed": 2,
    "index_paths": [
      "tests/auth/_index.json",
      "tests/checkout/_index.json",
      "tests/orders/_index.json"
    ]
  },
  "error": null
}
```

**Single Suite:**
```json
{
  "data": {
    "suites_processed": 1,
    "tests_indexed": 42,
    "files_added": 3,
    "files_removed": 0,
    "index_paths": [
      "tests/checkout/_index.json"
    ]
  },
  "error": null
}
```

**Tool Error:**
```json
{
  "data": null,
  "error": {
    "code": "TESTS_DIR_NOT_FOUND",
    "message": "No tests/ directory found in repository root"
  }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| TESTS_DIR_NOT_FOUND | No tests/ directory exists |
| SUITE_NOT_FOUND | Specified suite directory not found |
| INDEX_WRITE_ERROR | Failed to write index file (permissions, disk full) |
| PARSE_ERROR | One or more test files could not be parsed |

---

## Tool: `analyze_coverage_gaps`

Compares documentation against test `source_refs` to identify uncovered areas.

### Parameters

```json
{
  "type": "object",
  "properties": {
    "suite": {
      "type": "string",
      "description": "Suite to analyze coverage for (optional, analyzes all if omitted)"
    },
    "docs_path": {
      "type": "string",
      "description": "Documentation directory (optional, defaults to 'docs/')"
    }
  },
  "required": []
}
```

### Response

**Success:**
```json
{
  "data": {
    "docs_scanned": 15,
    "docs_covered": 12,
    "coverage_percent": 80,
    "gaps": [
      {
        "document_path": "docs/features/refunds/partial-refund.md",
        "document_title": "Partial Refund Processing",
        "severity": "high",
        "size_kb": 12,
        "heading_count": 7
      },
      {
        "document_path": "docs/features/checkout/3d-secure.md",
        "document_title": "3D Secure Authentication",
        "severity": "medium",
        "size_kb": 6,
        "heading_count": 4
      },
      {
        "document_path": "docs/api/webhooks.md",
        "document_title": "Webhook Events",
        "severity": "low",
        "size_kb": 2,
        "heading_count": 1
      }
    ]
  },
  "error": null
}
```

**No Gaps:**
```json
{
  "data": {
    "docs_scanned": 10,
    "docs_covered": 10,
    "coverage_percent": 100,
    "gaps": []
  },
  "error": null
}
```

**Tool Error:**
```json
{
  "data": null,
  "error": {
    "code": "DOCS_DIR_NOT_FOUND",
    "message": "No docs/ directory found in repository root"
  }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| DOCS_DIR_NOT_FOUND | Documentation directory does not exist |
| TESTS_DIR_NOT_FOUND | Tests directory does not exist |
| SUITE_NOT_FOUND | Specified suite not found |

### Severity Calculation

| Criteria | Severity |
|----------|----------|
| size_kb > 10 OR heading_count > 5 | high |
| size_kb > 5 OR heading_count > 2 | medium |
| otherwise | low |

---

## Common Response Envelope

All tools use `McpToolResponse<T>` pattern:

```csharp
public sealed record McpToolResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorInfo? Error { get; init; }
}

public sealed record ErrorInfo
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
```

---

## JSON-RPC Invocation

Tools are called via standard MCP JSON-RPC 2.0:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "validate_tests",
  "params": {
    "suite": "checkout"
  }
}
```

Response:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "data": { ... },
    "error": null
  }
}
```
