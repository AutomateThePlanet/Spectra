# Contract: .spectra-result.json Schema

**Version**: 1.0  
**Consumer**: VS Code Copilot Chat SKILLs (via `readFile .spectra-result.json`)

## Base Schema (all commands)

```json
{
  "command": "string",
  "status": "string",
  "timestamp": "string (ISO 8601 UTC)",
  "message": "string | null"
}
```

## Per-Command Extensions

### generate
```json
{
  "command": "generate",
  "status": "analyzing | analyzed | generating | completed | failed",
  "suite": "string",
  "analysis": { "total_behaviors": 0, "already_covered": 0, "recommended": 0 },
  "generation": { "tests_requested": 0, "tests_generated": 0, "tests_written": 0 }
}
```

### update
```json
{
  "command": "update",
  "status": "classifying | updating | verifying | completed | failed",
  "suite": "string",
  "testsUpdated": 0,
  "testsRemoved": 0,
  "testsUnchanged": 0,
  "classification": { "upToDate": 0, "outdated": 0, "orphaned": 0, "redundant": 0 }
}
```

### docs-index
```json
{
  "command": "docs-index",
  "status": "scanning | indexing | extracting-criteria | completed | failed",
  "documentsIndexed": 0,
  "documentsSkipped": 0,
  "documentsTotal": 0,
  "criteriaExtracted": 0
}
```

### analyze-coverage
```json
{
  "command": "analyze-coverage",
  "status": "scanning-tests | analyzing-docs | analyzing-criteria | analyzing-automation | completed | failed",
  "documentationCoverage": { "percentage": 0, "covered": 0, "total": 0 },
  "acceptanceCriteriaCoverage": { "percentage": 0, "covered": 0, "total": 0 },
  "automationCoverage": { "percentage": 0, "covered": 0, "total": 0 }
}
```

### extract-criteria
```json
{
  "command": "extract-criteria",
  "status": "scanning-docs | extracting | building-index | completed | failed",
  "documentsProcessed": 0,
  "documentsSkipped": 0,
  "criteriaExtracted": 0,
  "criteriaNew": 0,
  "criteriaUpdated": 0
}
```

### dashboard
```json
{
  "command": "dashboard",
  "status": "collecting-data | generating-html | completed | failed",
  "outputPath": "string",
  "suitesIncluded": 0,
  "testsIncluded": 0
}
```

### validate
```json
{
  "command": "validate",
  "status": "completed | failed",
  "totalFiles": 0,
  "valid": 0,
  "errors": []
}
```

## Guarantees

1. File is written atomically (flush to disk) — never partially written
2. File is deleted at command start — stale data never persists
3. File exists after command completion (success or failure)
4. `status` field always present — SKILL can poll for "completed" or "failed"
5. On failure: `"success": false` and `"error": "message"` fields present
