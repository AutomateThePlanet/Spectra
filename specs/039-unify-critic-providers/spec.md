# Feature Specification: Unified Critic Provider List

**Feature Branch**: `039-unify-critic-providers`
**Created**: 2026-04-11
**Status**: Draft
**Input**: User description: "Spec 025 — Align critic provider validation with the generator provider list. Both run through the Copilot SDK since spec 009, but the critic config still accepts the pre-consolidation set (google, openai, anthropic, github) instead of the post-consolidation set (github-models, azure-openai, azure-anthropic, openai, anthropic). Add legacy alias mapping for backward compatibility."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Azure-Only Billing Setup (Priority: P1)

A user with an Azure billing account wants to run SPECTRA generation against `gpt-4.1-mini` (cheap) on Azure OpenAI and verify each generated test against `gpt-4o` (more capable) on the same Azure OpenAI account. Today they cannot, because the critic config rejects `azure-openai` even though the underlying runtime supports it.

**Why this priority**: This is the only user-visible feature in the spec. Everything else is alignment/cleanup. Delivering this story alone unblocks a real billing-consolidation use case that users have asked for.

**Independent Test**: Set `ai.critic.provider` to `"azure-openai"` in `spectra.config.json`. Run `spectra ai generate --suite x`. Verify the critic accepts the provider, runs verification, and produces verdicts.

**Acceptance Scenarios**:

1. **Given** a config with `ai.critic.provider = "azure-openai"`, **When** the user runs generation, **Then** the critic initializes successfully and verifies generated tests.
2. **Given** a config with `ai.critic.provider = "azure-anthropic"`, **When** the user runs generation, **Then** the critic initializes successfully.
3. **Given** the documentation listing supported critic providers, **When** the user reads it, **Then** all five provider names match the generator provider list (`github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`).

---

### User Story 2 - Backward Compatibility for Legacy Names (Priority: P2)

A user upgrading from a pre-spec-039 SPECTRA has `"provider": "github"` in their critic config. After upgrading, the command still works — they get a deprecation warning telling them to switch to `"github-models"`, but the critic still runs.

**Why this priority**: Protects existing users from a config-format break on upgrade. Without this, every pre-039 user with `"github"` in their config sees a hard failure.

**Independent Test**: With a config containing `"provider": "github"`, run `spectra ai generate`. Verify the command succeeds, a deprecation warning is shown, and the critic uses `github-models`.

**Acceptance Scenarios**:

1. **Given** a config with the legacy `"github"` critic provider, **When** generation runs, **Then** the critic still works and a deprecation warning is shown directing the user to `"github-models"`.
2. **Given** a config with the legacy `"google"` critic provider, **When** generation runs, **Then** the user sees a clear actionable error listing the supported providers (Google is not on the list because the runtime cannot route to it).

---

### User Story 3 - Documentation Reflects Reality (Priority: P2)

A new user reads the configuration docs to understand which critic providers are supported. The list they see matches what the validator actually accepts — no mismatch between docs and behavior.

**Why this priority**: Doc/code drift is the original cause of the bug. Story 1 fixes the validator; Story 3 fixes the docs so the next reader doesn't have the same confusion.

**Independent Test**: Read `docs/configuration.md` and `docs/grounding-verification.md`. Verify both list exactly the same critic provider set as the generator provider set. Verify no `google` / `GOOGLE_API_KEY` references remain.

**Acceptance Scenarios**:

1. **Given** the configuration documentation, **When** the user looks up the accepted critic providers, **Then** they see the list `github-models, azure-openai, azure-anthropic, openai, anthropic`.
2. **Given** the grounding verification documentation, **When** the user looks at the example critic config, **Then** the example uses one of the supported providers and does not reference Google.

---

### Edge Cases

- **Unknown provider**: A typo like `"openia"` produces a clear validation error listing the supported providers.
- **Empty provider string**: Treated the same as missing — the critic falls back to the configured default (`github-models`) or is disabled.
- **Mixed case**: `"Azure-OpenAI"` is accepted (case-insensitive match) and normalized to lower-case `"azure-openai"`.
- **`google` legacy value**: Hard error (not silently mapped) — the runtime cannot route to Google, so silent fallback would mislead the user.
- **`github` legacy value**: Soft mapping to `github-models` with deprecation warning.
- **Existing tests using `openai` / `anthropic` / `github-models`**: Continue to work unchanged. This change is purely additive.

## Requirements *(mandatory)*

### Functional Requirements

#### Validation

- **FR-001**: The critic configuration MUST accept the same five provider names that the generator configuration accepts: `github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`.
- **FR-002**: Provider name matching MUST be case-insensitive; the value is normalized to lower-case before validation.
- **FR-003**: An unknown provider name MUST produce a validation error that lists the supported provider names.
- **FR-004**: An empty or missing critic provider value MUST be treated as "use default" (the same default the system uses today), not as an error.

#### Backward Compatibility

- **FR-005**: The legacy value `"github"` MUST be silently mapped to `"github-models"` and a one-line deprecation warning MUST be emitted to stderr advising the user to update their config.
- **FR-006**: The legacy value `"google"` MUST produce a hard error (not a silent mapping) explaining that the value is no longer supported and listing the supported providers.

#### Documentation

- **FR-007**: The configuration documentation MUST list the same five critic provider names as the validator accepts.
- **FR-008**: The grounding-verification documentation MUST not reference Google or `GOOGLE_API_KEY` for the critic.
- **FR-009**: At least one example in the documentation MUST show an Azure-only billing setup (generator and critic both on Azure).

#### Behavior Preservation

- **FR-010**: Existing configurations using `openai`, `anthropic`, or `github-models` as the critic provider MUST continue to work unchanged.
- **FR-011**: This feature MUST NOT change the runtime AI implementation, the generation pipeline, or the critic verdict format. It is purely a validation + documentation alignment.

### Key Entities

- **Critic provider name**: A short string identifying which AI provider the critic uses to verify generated tests. The valid set is now identical to the generator provider list.
- **Legacy alias mapping**: A small lookup table that maps pre-spec-039 values (`github`, `google`) to current behavior (rewrite, warn, or error).

## Assumptions

- The runtime critic implementation already supports all five providers via the Copilot SDK consolidation in spec 009. This is a verified assumption — the underlying class accepts any provider name the SDK understands.
- The default critic provider (when not explicitly set) is `github-models` and remains so. This spec does not change the default.
- "Soft mapping" for `github` (warn + continue) is acceptable because the user's intent is unambiguous. "Hard error" for `google` is required because the runtime literally cannot honor the request and silent mapping would surprise users with cross-vendor behavior.
- Deprecation warnings go to stderr, not stdout, so they don't pollute machine-parseable JSON output.
- The documentation files referenced (`configuration.md`, `grounding-verification.md`) exist in `docs/` and are user-facing. CLAUDE.md is auto-updated.
- No new model classes, no new dependencies, no schema migrations. The on-disk config format is unchanged — only the validator's accepted set changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Setting `ai.critic.provider` to `"azure-openai"` or `"azure-anthropic"` in `spectra.config.json` and running `spectra ai generate` succeeds in 100% of attempts (was 0% before this spec).
- **SC-002**: Setting `ai.critic.provider` to the legacy value `"github"` and running generation succeeds in 100% of attempts and emits exactly one deprecation warning to stderr.
- **SC-003**: Setting `ai.critic.provider` to the legacy value `"google"` and running generation fails fast with an error message that lists all five supported providers.
- **SC-004**: Setting `ai.critic.provider` to an unknown value (e.g., `"openia"`) fails fast with the same actionable error message.
- **SC-005**: 100% of references to critic providers in `docs/configuration.md` and `docs/grounding-verification.md` use the new five-name list. Zero references to `google` or `GOOGLE_API_KEY` for the critic.
- **SC-006**: Existing test suite passes with at least 5 net new tests covering the new validation and legacy alias paths. No existing tests are broken.
- **SC-007**: A user with a fresh `spectra init` config does not see any change in behavior (the default `github-models` critic configuration is unchanged).
