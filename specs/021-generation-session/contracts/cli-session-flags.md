# CLI Session Flags Contract

## New Flags on `spectra ai generate`

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--from-suggestions` | string? (optional indices) | null | Generate from previous session's suggestions. Optionally pass comma-separated indices (e.g., `1,3`) |
| `--from-description` | string | null | Create a test from a plain-language behavior description |
| `--context` | string? | null | Additional context for `--from-description` |
| `--auto-complete` | bool | false | Run all phases without prompts (analyze → generate → suggestions → finalize) |

## Session JSON Schema (`.spectra/session.json`)

```json
{
  "session_id": "gen-2026-04-04-103000",
  "suite": "checkout",
  "started_at": "2026-04-04T10:30:00Z",
  "expires_at": "2026-04-04T11:30:00Z",
  "analysis": {
    "total_behaviors": 18,
    "already_covered": 8,
    "breakdown": { "happy_path": 4, "negative": 3, "edge_case": 2, "security": 1 }
  },
  "generated": ["TC-201", "TC-202"],
  "suggestions": [
    { "index": 1, "title": "Payment timeout after 30s", "category": "edge_case", "status": "pending" },
    { "index": 2, "title": "Concurrent checkout sessions", "category": "edge_case", "status": "generated" }
  ],
  "user_described": ["TC-215"]
}
```

## Extended GenerateResult JSON (--output-format json)

Additional fields when session features are used:

```json
{
  "suggestions": [
    { "title": "Payment timeout after 30s", "category": "edge_case" }
  ],
  "duplicate_warnings": [
    { "new_test": "TC-220", "similar_to": "TC-118", "similarity": 0.85, "title": "Payment form validation" }
  ],
  "session": {
    "from_docs": 9,
    "from_suggestions": 3,
    "from_description": 1,
    "total": 13
  }
}
```
