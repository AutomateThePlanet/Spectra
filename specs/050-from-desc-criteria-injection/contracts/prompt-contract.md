# Phase 1 Contract: Prompt-level criteria injection

**Branch**: `050-from-desc-criteria-injection`
**Date**: 2026-06-02

## Status

This fix introduces **no new external interface**. No CLI flag, no MCP tool, no public API, no on-disk schema. This document records the **existing internal contract** the fix relies on, so reviewers can confirm we are honoring it and not modifying it.

## Existing contract being relied on

### `IAgentRuntime.GenerateTestsAsync` — `criteriaContext` parameter

Defined in `src/Spectra.CLI/Agent/IAgentRuntime.cs`:

```csharp
Task<GenerationResult> GenerateTestsAsync(
    string prompt,
    IReadOnlyList<SourceDocument> documents,
    IReadOnlyList<TestCase> existingTests,
    int requestedCount,
    string? criteriaContext = null,        // <-- the contract we rely on
    TestimizeDataset? testimizeData = null,
    CancellationToken ct = default);
```

**Contract**: When `criteriaContext` is non-null and non-whitespace, the implementation MUST emit a MANDATORY criteria-mapping instruction into the outbound system prompt that tells the model "You MUST map each test case to matching acceptance criteria…", followed by the criteria content. When `criteriaContext` is null or whitespace, the implementation MUST omit that block entirely.

**Honored by**: `CopilotGenerationAgent` (the only `IAgentRuntime` today), at `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs:527`. This spec does not modify that code.

**Consumed by**:
- Batch flow (`GenerateHandler.ExecuteDirectModeAsync` and `ExecuteInteractiveModeAsync`) — already passes a non-empty `criteriaContext` when criteria exist for the suite. ✅
- From-description flow (`UserDescribedGenerator.GenerateAsync`) — **currently passes `null` regardless of input**. ❌ This is the bug.

**Post-fix invariant**: All three consumers honor the contract uniformly. The from-description flow forwards whatever `criteriaContext` it received from `GenerateHandler.LoadCriteriaContextAsync`, which is the same loader the batch flow uses.

### `UserDescribedGenerator.BuildPrompt` — `criteriaContext` parameter

Defined in `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs:18-24`:

```csharp
public static string BuildPrompt(
    string description,
    string? context,
    string suite,
    IReadOnlyCollection<string> existingIds,
    string? documentContext = null,
    string? criteriaContext = null)
```

**Pre-fix contract**: When `criteriaContext` is non-empty, append a `## Related Acceptance Criteria` body section to the user prompt.

**Post-fix contract**: The `criteriaContext` parameter is retained (callers compile-compat), but `BuildPrompt` no longer appends the loose body section. The parameter becomes a no-op inside `BuildPrompt`; the criteria content reaches the model via the MANDATORY block emitted by `GenerationAgent` from the `GenerateTestsAsync` call site.

**Why keep the parameter**: avoids a breaking signature change. Test code already passes `criteriaContext:` to `BuildPrompt`; removing it would force a parallel signature change in many call sites. The parameter is now informational-only on `BuildPrompt`, used to gate any future loose-section variant if one is ever justified again.

> **Alternative considered (rejected)**: remove the `criteriaContext` parameter from `BuildPrompt` entirely. Rejected — touches more files than necessary and offers no semantic benefit.

## New internal seam introduced (not an external contract)

### `UserDescribedGenerator.GenerateAsync` — optional agent factory

Defined in `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs:82-94` (proposed signature):

```csharp
public async Task<TestCase?> GenerateAsync(
    string description,
    string? context,
    string suite,
    IReadOnlyCollection<string> existingIds,
    SpectraConfig config,
    string currentDir,
    string testsPath,
    Action<string>? onStatus,
    CancellationToken ct = default,
    string? documentContext = null,
    string? criteriaContext = null,
    IReadOnlyList<string>? sourceRefPaths = null,
    Func<SpectraConfig, string, string, Action<string>?, CancellationToken, Task<AgentCreateResult>>? agentFactory = null)   // NEW, default null → AgentFactory.CreateAgentAsync
```

**Contract**: When `agentFactory` is null (production default), production behavior is identical to today plus the `criteriaContext` forwarding fix. When non-null (test use), the supplied delegate is invoked instead of `AgentFactory.CreateAgentAsync`. This is the seam the test plan needs.

**Visibility**: This is an internal contract for the test suite, not a public extension point. No CLI surface or MCP tool consumes it. It does not appear in any user-facing documentation.

**Stability**: Treated as test infrastructure. Future refactors may replace it with DI without notice. Tests inside this repo may depend on it; nothing outside this repo may.

## Surfaces NOT modified

For audit clarity, the following remain byte-identical to pre-fix:

- `spectra ai generate` CLI command (flags, exit codes, help text, JSON output schema).
- `IAgentRuntime` interface signature (no change to `GenerateTestsAsync` shape).
- `CopilotGenerationAgent.GenerateTestsAsync` implementation (line 527 already correct).
- All MCP tools (Run Management, Test Execution, Discovery, Data, Reporting).
- Generated test YAML frontmatter schema.
- `VerificationVerdict` enum.
- `_index.json` format.
- `docs/criteria/*.yaml` files (read-only consumption).
- `spectra validate` rules and exit codes.
