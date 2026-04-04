# Research: Generation Session Flow

## R1: Session State Storage

**Decision**: Store session state in `.spectra/session.json` as a single JSON file per suite
**Rationale**: Simple file-based storage aligns with SPECTRA's file-first approach. One file is sufficient since sessions are single-user and short-lived (1h TTL). The `.spectra/` directory is already gitignored.
**Alternatives considered**: SQLite (rejected — overkill for ephemeral state), in-memory only (rejected — can't support `--from-suggestions` across invocations)

## R2: Fuzzy Title Matching Algorithm

**Decision**: Use normalized Levenshtein distance for duplicate detection, with >80% similarity threshold
**Rationale**: Levenshtein is well-understood, deterministic, and requires no external dependencies. Normalization by max string length gives a 0-1 similarity score. The 80% threshold balances catching near-duplicates without false positives.
**Alternatives considered**: Jaccard similarity on word tokens (rejected — less precise for short titles), embedding-based similarity (rejected — requires AI call, too slow for local check)

## R3: User-Described Test Generation

**Decision**: Use the existing Copilot SDK agent to generate structured test cases from free-text descriptions, with a specialized prompt that includes suite context and existing test patterns
**Rationale**: Reuses the existing generation infrastructure. The prompt is different (description-focused vs doc-focused) but the underlying agent and test parsing are the same.
**Alternatives considered**: Template-based generation without AI (rejected — can't infer steps/expected results from a one-line description)

## R4: Session Expiry Strategy

**Decision**: Check `expires_at` timestamp on session load; if expired, return null (start fresh). TTL is 1 hour from session creation.
**Rationale**: Simple timestamp check. No background cleanup needed — expired sessions are just ignored on next read. The file gets overwritten when a new session starts.
**Alternatives considered**: Explicit cleanup command (rejected — unnecessary complexity for a single file)

## R5: Suggestions Source

**Decision**: Derive suggestions from the BehaviorAnalysisResult by comparing identified behaviors against generated tests. Each uncovered behavior becomes a suggestion.
**Rationale**: The behavior analysis already identifies testable behaviors by category. After generation, we diff against what was generated to find remaining gaps. This is more structured than generic gap analysis.
**Alternatives considered**: Separate AI call to generate suggestions (rejected — wasteful when we already have the analysis data)

## R6: --auto-complete Flow

**Decision**: Sequential execution: analyze → generate all recommended → generate all suggestions → finalize. No user-described phase in auto-complete (requires human input).
**Rationale**: Auto-complete is for CI/automation where all decisions are automatic. User-described tests inherently require human input, so they're excluded.
**Alternatives considered**: Including user-described via `--from-description` in auto-complete (rejected — `--from-description` is a separate flag that can be combined independently)
