# Research: Testimize Integration

**Feature**: 038-testimize-integration
**Date**: 2026-04-10
**Status**: Complete (no NEEDS CLARIFICATION outstanding)

## Decision 1: MCP-only integration, no NuGet dependency

**Decision**: Talk to Testimize exclusively over the MCP/JSON-RPC protocol, by spawning the `Testimize.MCP.Server` global tool as a child process. Do NOT take a NuGet reference on any Testimize package.

**Rationale**:
- Zero coupling to Testimize internals or version churn.
- Follows the same pattern the SystemTests project already uses for BELLATRIX desktop tools.
- The user can swap Testimize versions independently of SPECTRA via `dotnet tool update --global Testimize.MCP.Server`.
- The integration becomes invisible to users who don't enable it — no extra DLLs in the SPECTRA distribution.
- Constitution principle V (YAGNI/Simplicity): no abstraction layer, no service interfaces, no DI registration. Just one process wrapper class.

**Alternatives considered**:
- *NuGet reference + in-process call*: rejected — couples SPECTRA's release cadence to Testimize's, adds DLL bloat for users who don't use the feature, makes the dependency mandatory at the package level even when disabled at runtime.
- *HTTP server*: rejected — Testimize.MCP.Server already supports stdio mode, which is simpler and avoids port management.

## Decision 2: Conditional tool registration in `GenerationAgent`

**Decision**: Register the two Testimize AI tools (`GenerateTestData`, `AnalyzeFieldSpec`) immediately after the existing 7 tools in `GenerationAgent.GenerateTestsAsync`, gated by `config.Testimize.Enabled` AND a successful health probe. The MCP client is created and disposed within the scope of a single `GenerateTestsAsync` call.

**Rationale**:
- The existing site (line ~94 of `GenerationAgent.cs`) is a single `var tools = ...ToList()` followed by session creation. Adding `tools.AddRange(testimizeTools)` is a one-line surgical change.
- Per-call lifecycle keeps the child process scoped — no global singleton, no shared state between concurrent runs.
- Disposal happens in a `try/finally` (or `await using` for the new client). Constitution principle II (deterministic execution): even on Ctrl-C the process is killed.

**Alternatives considered**:
- *Singleton client at app startup*: rejected — would keep the child process alive between commands and complicates teardown across multiple `spectra` invocations.
- *Lazy registration the first time the AI calls a tool*: rejected — the AI needs to see the tool schemas in its initial system prompt; lazy registration would require regenerating the session.

## Decision 3: Per-call timeout of 30 seconds

**Decision**: Each `CallToolAsync` call enforces a 30-second timeout. Health check uses 5 seconds.

**Rationale**:
- Generation latency is dominated by the upstream AI provider (often 30–120s per response). A 30s ceiling on Testimize calls keeps it from being the bottleneck while leaving room for ABC algorithm runs that genuinely take 10–20s.
- Health check is short-running (probe one method) — 5s is plenty and keeps `spectra testimize check` snappy (SC-007 demands <5s overall).
- These are constants, not config — premature flexibility (constitution principle V).

**Alternatives considered**:
- *Configurable per-call timeout*: rejected — no demonstrated need; if a user hits it, they can disable Testimize and file an issue.
- *No timeout*: rejected — a hung child process would freeze generation indefinitely.

## Decision 4: Health probe via a tool call, not a custom handshake

**Decision**: `IsHealthyAsync` invokes the Testimize MCP server's standard `tools/list` (or equivalent) call and considers the server healthy if the call succeeds within 5s. No custom `health_check` tool required on the Testimize side.

**Rationale**:
- Reuses the same JSON-RPC pipe `CallToolAsync` already needs.
- Works with any future Testimize version without coordinating a special endpoint.
- Failure modes (process crashed, JSON parse error, timeout) are all caught by the same try/catch the real tool calls use.

**Alternatives considered**:
- *Dedicated `health_check` tool on Testimize*: rejected — would require a Testimize change and pin SPECTRA to a Testimize version that exposes it.

## Decision 5: Graceful degradation everywhere

**Decision**: Every failure mode (tool not installed, process crashes, server returns garbage, server times out, server returns empty results) maps to a single behavior: log a non-fatal warning, return null from the tool wrapper, let the AI fall back to its own approximated values for that one request. The generation run continues.

**Rationale**:
- US3 is a P1: enabling Testimize must never make `spectra ai generate` worse than disabling it.
- A single uniform failure path makes the wrapper code small and the test matrix tractable.
- "Continue with AI fallback" is exactly what happens when Testimize is disabled, so the fallback is well-tested.

**Alternatives considered**:
- *Fail the run on any Testimize error*: rejected — turns the opt-in feature into a footgun.
- *Retry with backoff*: rejected — adds latency for no clear win; the AI fallback is a perfectly valid alternative.

## Decision 6: `testimize_enabled` placeholder is a string ("true" / "")

**Decision**: The new template placeholder follows the existing convention used by spec 030's `{{#if}}` blocks: an empty string is falsy, any non-empty string is truthy. The value is the literal `"true"` or the literal `""`.

**Rationale**:
- Reuses the existing `PlaceholderResolver` `{{#if}}` semantics with zero engine changes.
- Mirrors how `focus_areas` and `acceptance_criteria` are already gated.
- Simpler than introducing a typed boolean placeholder.

**Alternatives considered**:
- *New typed boolean placeholder*: rejected — engine change for no benefit.

## Decision 7: Init detection via `dotnet tool list -g`

**Decision**: `spectra init` shells out to `dotnet tool list -g`, parses the output for `testimize.mcp.server`, and if found, prompts the user (in interactive mode only) whether to enable Testimize. In non-interactive mode (`--no-interaction`), `enabled` stays `false` regardless.

**Rationale**:
- `dotnet tool list -g` is the canonical way to detect installed global tools — no Windows registry probing or filesystem walking.
- Honors the existing `--no-interaction` contract from spec 020 (CI mode never prompts).
- If the shell-out fails (no `dotnet` on PATH, command times out), detection silently fails and `enabled` stays `false` — same end state as if the tool weren't there.

**Alternatives considered**:
- *Always-on detection without prompt*: rejected — surprising, opts users in without consent.
- *Hardcoded path probing*: rejected — fragile across OSes and tool installation locations.

## Decision 8: ABC settings live under `testimize.abc_settings`, are nullable

**Decision**: `TestimizeConfig.AbcSettings` is `TestimizeAbcSettings?` (nullable). When null, SPECTRA does not pass any ABC tuning to the Testimize tool, and Testimize uses its built-in defaults. When set, the values are forwarded as-is.

**Rationale**:
- Most users will never tune ABC. A nullable subtree keeps default config files clean (only `enabled`, `mode`, `strategy` are written by `spectra init`).
- Testimize's built-in ABC defaults are already sensible — no need for SPECTRA to duplicate them in C# defaults.

**Alternatives considered**:
- *Always-present ABC settings with C# defaults*: rejected — duplicates Testimize's own defaults, drifts on Testimize updates.

## Decision 9: New CLI parent command `spectra testimize` with one subcommand `check`

**Decision**: Introduce a `spectra testimize` parent command with a single subcommand `check` for Phase 1. Future Testimize-related commands (e.g., `spectra testimize generate-data`) can be added under the same parent without API churn.

**Rationale**:
- Matches existing patterns (`spectra ai generate`, `spectra docs index`, `spectra prompts list`).
- Reserves the namespace cleanly.
- Keeps `spectra` top-level command list short.

**Alternatives considered**:
- *Top-level `spectra testimize-check`*: rejected — pollutes the top-level command list.
- *Subcommand under `spectra config`*: rejected — `check` is operational, not configurational.

## Decision 10: Out of scope for Phase 1

Confirming the spec's "Out of Scope" list:

- Custom `IInputParameter` types from SPECTRA — Testimize already has 17 built-in types; SPECTRA passes string type names through.
- Testimize C# attribute output generators — SPECTRA writes Markdown test cases, not annotated test methods.
- Auto-generation of `testimizeSettings.json` from docs — manual config for now.
- Testimize integration in `spectra ai update` — only `generate`.
- Dashboard visualization of Testimize coverage — observed via the analysis breakdown only.
- Parallel Testimize calls for multi-suite generation — single sequential call site.

## Open Questions

None. All FRs in the spec map to existing patterns or the decisions above.
