# Research: ISTQB Test Design Techniques in Prompt Templates

**Feature**: 037-istqb-test-techniques
**Date**: 2026-04-10
**Status**: Complete (no NEEDS CLARIFICATION outstanding)

## Decision 1: Use a fixed enumeration of six ISTQB techniques

**Decision**: Hard-code the six techniques (EP, BVA, DT, ST, EG, UC) as prompt content. Do NOT introduce a `analysis.techniques` config section.

**Rationale**:
- The six techniques are an industry-standard, stable set defined by ISTQB. They do not need per-project customization.
- Adding a config schema would force users to think about something they shouldn't have to.
- YAGNI (constitution principle V): no abstraction until the third use case demands it.
- Users who need different reasoning can still edit `.spectra/prompts/behavior-analysis.md` directly — the prompt template is the customization surface.

**Alternatives considered**:
- *Per-technique config in `spectra.config.json`*: rejected — premature flexibility, no demonstrated need.
- *Pluggable technique providers*: rejected — over-engineering for prompt content.

## Decision 2: `Technique` is a plain string, not an enum

**Decision**: `IdentifiedBehavior.Technique` is `string` (default `""`), not a C# enum.

**Rationale**:
- AI responses are non-deterministic; the AI may emit unexpected values (`"bva"`, `"Boundary"`, `null`). String is forgiving.
- Backward compatibility with legacy responses is required (FR-006). Empty string is the natural default for a missing JSON field with `JsonSerializer` defaults.
- Output rendering can normalize/group strings without needing parse-time validation.
- Matches the existing pattern for `Category`, also a string.

**Alternatives considered**:
- *Enum with `[JsonConverter]` lenient parser*: rejected — adds code, no real benefit; we'd still need an "Unknown" fallback.
- *Nullable string*: rejected — `Category` is `""`-default, not nullable; consistency wins.

## Decision 3: Technique breakdown is computed in `BehaviorAnalyzer`, not in presenters

**Decision**: After parsing the AI response, `BehaviorAnalyzer` computes both the category breakdown (existing) and the technique breakdown (new) and exposes both on `BehaviorAnalysisResult`. Presenters and result writers consume the precomputed maps.

**Rationale**:
- Single source of truth — no duplicate counting logic in terminal/HTML/JSON paths.
- Mirrors the existing `Breakdown` field for categories.
- Easy to unit-test in isolation.

**Alternatives considered**:
- *Compute in each presenter*: rejected — duplication, drift risk.
- *Compute lazily on demand*: rejected — adds property complexity for no measurable cost.

## Decision 4: 40% distribution cap is a prompt instruction, not a runtime filter

**Decision**: The "no category exceeds 40%" rule lives inside the prompt text. SPECTRA does not post-process AI responses to enforce it.

**Rationale**:
- Enforcement post-hoc would require either rejecting/retrying responses (slow, expensive, possibly infinite loop) or silently dropping behaviors (data loss).
- Treating it as guidance to the AI is consistent with how the existing prompt handles other rules (e.g., "max 80 chars title").
- The success criterion (SC-002) measures actual distribution as a quality metric; we observe it, we do not enforce it.

**Alternatives considered**:
- *Post-process and trim excess*: rejected — destroys information, hard to choose what to drop.
- *Post-process and reject + retry*: rejected — latency cost, no guarantee of convergence.

## Decision 5: Migration is opt-in via existing `spectra prompts reset` and `update-skills`

**Decision**: Existing user-facing template files in `.spectra/prompts/` are NEVER auto-overwritten by the upgrade. Users opt in by running `spectra prompts reset <name>` (or `--all`) or by accepting an `update-skills` offer for unmodified files.

**Rationale**:
- Spec 022 already established hash-tracking semantics that preserve user edits; we reuse it (FR-028, FR-030).
- Auto-overwriting would silently destroy customizations — unacceptable for a power-user tool.
- New projects (`spectra init`) get the new templates by default (FR-027), so adoption parity is achieved without forcing migration on existing projects.

**Alternatives considered**:
- *Auto-update with backup*: rejected — confusing, leaves orphan `.bak` files, surprises users.
- *Version-stamp templates and compare*: rejected — `update-skills` already does this via SHA-256.

## Decision 6: `TechniqueHint` on `AcceptanceCriterion` is `string?`, not a separate enum

**Decision**: Add `string? TechniqueHint` to `AcceptanceCriterion` with `[JsonPropertyName("technique_hint")]` and default null.

**Rationale**:
- Same reasoning as Decision 2: AI flexibility, backward compatibility, simplicity.
- `null` (instead of `""`) is correct here because:
  1. `AcceptanceCriterion` is a Core model (YAML on disk), not just an in-memory analysis result.
  2. YAML round-trip handles `null` cleanly as an absent field, which is the desired write behavior for criteria that have no hint.
  3. Consistency with other optional `string?` properties on the model.

**Alternatives considered**:
- *Required string*: rejected — would force every legacy criterion file to be rewritten on first load.
- *Enum with serializer*: same rejection as Decision 2.

## Decision 7: Technique breakdown JSON keys use the short codes

**Decision**: The `technique_breakdown` map in `.spectra-result.json` uses the short codes as keys (`"BVA": 38`, `"EP": 24`, ...), not human-readable names.

**Rationale**:
- Short codes are stable, language-neutral, and match the AI prompt instruction (`technique: BVA`).
- Tooling (SKILL parsers, dashboards) can rely on stable keys for filtering.
- Human-readable names ("Boundary Value Analysis") are still rendered in terminal/HTML for users.

**Alternatives considered**:
- *Long names as keys*: rejected — i18n risk, fragile to renaming.
- *Both*: rejected — duplication; presenters can map codes to labels at render time.

## Decision 8: Technique breakdown rendering uses a fixed display order

**Decision**: Both terminal and HTML render techniques in the order: BVA, EP, DT, ST, EG, UC. Categories continue to render in their existing order.

**Rationale**:
- Predictable visual layout aids comparison across runs.
- Order roughly matches the spec's presentation (specification-based first, experience-based last).

**Alternatives considered**:
- *Sort by count descending*: rejected — visual noise across runs.
- *Sort alphabetically*: rejected — separates closely related techniques.

## Open Questions

None. All FRs in the spec map to existing files and patterns.
