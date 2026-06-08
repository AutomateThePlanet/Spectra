# Phase 0 Research: Boundary-coverage gap detection

All Technical Context items resolved from direct inspection of the existing analysis seam. No open NEEDS CLARIFICATION items.

## Decision 1 — Carry boundary gaps as an additive top-level `boundary_gaps` array

- **Decision**: The in-session model emits a top-level `boundary_gaps` array in the same JSON object that already carries `behaviors` (and, when Testimize is enabled, `field_specs`). The CLI parses it into a typed list on `AnalysisRecommendation`.
- **Rationale**: `BehaviorAnalysisResult` already proves the pattern — `behaviors` + `field_specs` coexist as sibling top-level arrays parsed from one agent JSON object (`BehaviorAnalysisResult.cs:32,62`). Re-using it means zero new transport mechanism, automatic backward compatibility (a missing key → empty list), and it satisfies FR-003 (carried alongside `technique_breakdown`) without restructuring the result shape.
- **Alternatives considered**:
  - *Per-behavior field on `IdentifiedBehavior`*: rejected — a boundary gap is the **absence** of a behavior, so it has no behavior row to hang off; modeling it per-behavior is semantically wrong and would pollute `technique_breakdown` counts.
  - *Separate command / separate JSON file*: rejected — violates Simplicity (YAGNI) and FR-002 ("extend the existing BVA reasoning, not bolt on a parallel path").

## Decision 2 — Fail-loud validation lives in `AnalysisRecommendationBuilder`, reusing `AnalysisIngestOutcome.ParseFailure`

- **Decision**: Parse `boundary_gaps` in `AnalysisRecommendationBuilder.Build`. If the key is **absent** → empty list, success. If the key is **present but malformed** (not a JSON array, or an element missing a required field) → return `AnalysisRecommendation.ParseFail("<boundary-gap-specific message>")`, which the existing `IngestAnalysisCommand` maps to exit code **6** and prints the specific error.
- **Rationale**: The fail-loud boundary already exists and is typed (`AnalysisIngestOutcome.{Recommendation,EmptyResponse,ParseFailure}`, `AnalysisRecommendation.cs:10-20`). Routing malformed boundary-gap payloads through `ParseFailure` with a **distinct, attributable message** satisfies FR-003 ("specific error, not silent drop") without inventing a new outcome enum value or new exit code. A missing section maps to "no gaps," satisfying backward compatibility (FR-003 last sentence + edge case "legacy analysis output").
- **Alternatives considered**:
  - *New `AnalysisIngestOutcome.BoundaryGapInvalid` + new exit code*: rejected — YAGNI; the message already attributes the failure; adding an exit code is a CLI contract change with no caller benefit.
  - *Tolerant parse (skip malformed gaps silently)*: rejected — directly violates FR-003 (never silently dropped). Note the **behaviors** parse is intentionally tolerant (truncation recovery); the **boundary_gaps** parse is intentionally strict. They differ because behaviors drive a count that degrades gracefully, whereas a dropped gap is an invisible false-negative — exactly the failure this feature exists to prevent.

## Decision 3 — `BoundaryGap` shape: `field`, `kind`, `description`, `source`

- **Decision**: New record `Spectra.CLI.Agent.Analysis.BoundaryGap` with JSON-bound fields: `field` (the parameter/field or behavior the boundary concerns), `kind` (free-form string, expected vocabulary: `min-max`, `off-by-one`, `empty-null`, `overflow`, `timeout`, `max-length`), `description` (the missing edge, short), `source` (document/criterion implying it). **Required for validity**: `field`, `kind`, `description`. `source` optional (defaults to `""`, not malformed if absent).
- **Rationale**: FR-006 mandates each gap identify the field/behavior, the boundary kind, and the source. `description` makes it actionable. Keeping `kind` a free-form string mirrors `IdentifiedBehavior.Technique` (`IdentifiedBehavior.cs:35`) — avoids brittle enum rejection (Simplicity) while the prompt steers the model toward the canonical vocabulary. Blank `field`/`kind`/`description` on a present element is the **malformed** trigger for Decision 2.
- **Alternatives considered**:
  - *Strict `kind` enum with deserialization rejection*: rejected — an unknown-but-reasonable `kind` (e.g., "underflow") should not fail ingest; conservatism (FR-004) is about *whether to report a gap*, not about rejecting valid-but-novel kinds.
  - *Make `source` required*: rejected — models legitimately infer a boundary from combined context; forcing a single source would push the model to fabricate one (anti-FR-004). Empty source is acceptable, not malformed.

## Decision 4 — Prompt extends Technique 2 (BVA), output instructions, distribution guidelines

- **Decision**: In `behavior-analysis.md`, add boundary-gap instructions tied to the existing **BVA** section and **OUTPUT INSTRUCTIONS**: instruct the analyst to list, in a top-level `boundary_gaps` array, every boundary condition implied by the docs/criteria that is **not** covered by the existing tests (supplied via `coverage_context`) or the behaviors it just emitted — and to emit nothing when no boundary is implied (FR-004 conservatism).
- **Rationale**: FR-002 requires extending existing BVA reasoning rather than a parallel path. The prompt already enumerates boundary kinds in BVA (`behavior-analysis.md:38-43`) and Error Guessing (overflow/underflow/empty/null, `:60-68`) and already receives `coverage_context` for gap-only analysis (`:104-106`). The new section reuses both.
- **Alternatives considered**: A standalone "Technique 7" — rejected; boundary-gap detection is the *completeness check over* BVA/EG, not a seventh independent technique, and a new technique code would leak into `technique_breakdown`.

## Decision 5 — Presentation in `spectra-generation.agent.md`

- **Decision**: Add one paragraph instructing the agent to surface `boundary_gaps` alongside the category and technique breakdowns when presenting the analyze recommendation, framed as advisory ("these edges look untested — generate them?").
- **Rationale**: FR-007 (visible at the review point) and FR-005 (advisory). Mirrors the existing line that already tells the agent to show both breakdowns (`spectra-generation.agent.md:13`).

## Decision 6 — Critic stays untouched; regression net is the proof

- **Decision**: Make zero edits to `spectra-critic.agent.md`, `CriticPromptBuilder.cs`, `VerdictIngestor.cs`, and anything in `Spectra.Core`. Add a guard test asserting the critic verdict vocabulary is still `{grounded, partial, hallucinated, manual}` and that `VerdictIngestor` rejects a "completeness"/"boundary" verdict.
- **Rationale**: FR-001 and SC-005. The grounding investigation's whole point is that choosing seam (b) keeps these green; a red critic test would mean the wrong seam was touched.
- **Alternatives considered**: seam (a) (critic gains a completeness dimension) — explicitly rejected by the spec and the investigation (erodes the grounding/completeness separation; contradicts the critic's isolation contract).
