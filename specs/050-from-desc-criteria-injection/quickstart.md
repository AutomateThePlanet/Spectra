# Quickstart: Verify Spec 050 Locally

**Branch**: `050-from-desc-criteria-injection`
**Audience**: Developer reviewing or hand-testing the fix
**Time**: ~5 minutes after the branch is built

## Pre-fix repro (run this first to see the bug)

In a Spectra-managed repo with extracted acceptance criteria:

```powershell
spectra ai analyze --extract-criteria         # ensure docs/criteria is populated
spectra ai generate --suite checkout `
    --from-description "Verify guest checkout works when cart contains a single item" `
    --context "Default shipping address pre-filled"
```

Open the newly written `test-cases/checkout/TC-XXX.md`. The `criteria:` frontmatter field is empty or partial — that is the bug.

## Post-fix verification (after this branch lands)

Rerun the same command. The same `test-cases/checkout/TC-XXX.md` now has its `criteria:` field populated with the IDs the model mapped — for example:

```yaml
---
id: TC-042
title: Guest checkout — single item
priority: medium
component: checkout
criteria:
  - AC-CHECKOUT-003
  - AC-CHECKOUT-007
grounding:
  verdict: manual                # <-- stays manual, by deliberate design
  generator: gpt-5-mini
  critic: user-described
  verified_at: 2026-06-02T14:22:18Z
---
```

Then confirm coverage participation:

```powershell
spectra ai analyze --coverage --format json
```

The new `TC-042` MUST appear under acceptance-criteria coverage and MUST NOT appear under grounded statistics (verdict is `manual`).

## What to assert during code review

1. **One source line forwards `criteriaContext`.** Open `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs`. At the `agent.GenerateTestsAsync(...)` call (line ~113), confirm the call now reads `criteriaContext: criteriaContext` (not `null`).
2. **The loose body block is gone.** In the same file, `BuildPrompt` no longer appends a `## Related Acceptance Criteria` section. The `criteriaContext` parameter on `BuildPrompt` is retained for signature stability but is a no-op there.
3. **Verdict is still hard-coded `Manual`.** Line ~149: `Verdict = VerificationVerdict.Manual`. Unchanged. This is deliberate; see `research.md` Decision D3.
4. **No enum, no schema, no flag.** Confirm `VerificationVerdict.cs`, the YAML frontmatter writer, `Program.cs`/`GenerateCommand.cs`, and all MCP tool files show **zero modifications** in the diff.
5. **Tests cover the contract.** `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs` adds the six tests from spec § Test Plan, using a fake `IAgentRuntime` injected via the new optional factory parameter. The pre-existing `BuildPrompt_WithCriteriaContext_IncludesAcceptanceCriteriaHeader` and `BuildPrompt_WithBothContexts_IncludesBoth` tests are adapted because the loose body section is now gone.

## What to assert at runtime (no AI required)

The repository's xUnit suite covers everything that does not require a live model:

```powershell
dotnet test tests/Spectra.CLI.Tests
```

All existing tests MUST still pass. The six new tests MUST pass. The two adapted prompt-content tests MUST pass with their updated assertions.

## Negative case (no criteria match the suite)

In a workspace with no extracted criteria, or with a target suite whose criteria index is empty:

```powershell
spectra ai generate --suite untested-area --from-description "..."
```

The generated test's `criteria:` field MUST be empty (omitted or `[]`). The outbound prompt MUST contain no `ACCEPTANCE CRITERIA — MANDATORY` block and no `## Related Acceptance Criteria` section. This matches pre-fix behavior exactly and is asserted by `FromDescription_NoCriteria_OmitsBlock` and `FromDescription_DoesNotDuplicateCriteriaSection` in the test suite.

## Documentation spot-check

Open these and confirm the from-description note matches what this fix does:

- `docs/skills-integration.md` — criteria injected as MANDATORY instruction; `criteria:` populated; verdict stays `manual` by design.
- `docs/coverage.md` — from-description tests count toward acceptance-criteria coverage but not grounded statistics.
- `spectra-generate` SKILL (in `src/Spectra.CLI/Skills/`) — the from-description result presentation reflects that criteria are now linked.
- `PROJECT-KNOWLEDGE.md` — Spec 050 row added; verdict-stays-`manual` decision recorded.
