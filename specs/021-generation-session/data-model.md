# Data Model: Generation Session Flow

## Entities

### GenerationSessionState
Persisted to `.spectra/session.json`.

| Field | Type | Description |
|-------|------|-------------|
| session_id | string | Unique ID (e.g., "gen-2026-04-04-103000") |
| suite | string | Target suite name |
| started_at | DateTimeOffset | Session start time |
| expires_at | DateTimeOffset | Expiry (started_at + 1 hour) |
| analysis | AnalysisSnapshot? | Captured behavior analysis results |
| generated | string[] | Test IDs generated from docs (Phase 2) |
| suggestions | Suggestion[] | Gap-derived suggestions (Phase 3) |
| user_described | string[] | Test IDs created from descriptions (Phase 4) |

### AnalysisSnapshot
Captured from BehaviorAnalysisResult at session start.

| Field | Type | Description |
|-------|------|-------------|
| total_behaviors | int | Total testable behaviors found |
| already_covered | int | Behaviors covered by existing tests |
| breakdown | Dictionary<string, int> | Count by category (happy_path, negative, etc.) |

### Suggestion
A proposed test case from gap analysis.

| Field | Type | Description |
|-------|------|-------------|
| index | int | 1-based display index |
| title | string | Suggested test title |
| category | string | Behavior category (edge_case, negative, etc.) |
| status | SuggestionStatus | pending, generated, skipped |

### SuggestionStatus (enum)

| Value | Description |
|-------|-------------|
| Pending | Not yet acted on |
| Generated | Test was created from this suggestion |
| Skipped | User chose to skip |

## State Transitions

```
Session: Created → Active → Expired
                 ↘ Completed (user exits)

Suggestion: Pending → Generated
                    → Skipped
```

## Relationships

- One GenerationSessionState per suite (latest wins)
- GenerationSessionState contains 0..N Suggestions
- Suggestions are derived from BehaviorAnalysisResult minus generated tests
- Session is consumed by `--from-suggestions` and `--auto-complete` flags
