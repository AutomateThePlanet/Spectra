# Implementation Plan: Parallel Critic Verification & Error Log

**Branch**: `042-parallel-critic-error-log` | **Date**: 2026-04-11 | **Spec**: [spec.md](./spec.md)

## Summary

Parallelize the critic verification loop with a user-configurable semaphore (`ai.critic.max_concurrent`), add a dedicated error log (`.spectra-errors.log`) that captures full exception context only when errors occur, and surface error + rate-limit counts + active critic concurrency in both the debug `RUN TOTAL` line and the terminal Run Summary panel. Default `max_concurrent=1` preserves today's sequential behavior for existing users.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: GitHub Copilot SDK, Spectre.Console, System.Text.Json
**Storage**: File-based (`.spectra-debug.log`, `.spectra-errors.log`); no DB involvement
**Testing**: xUnit (`Spectra.Core.Tests`, `Spectra.CLI.Tests`)
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI + shared library (`src/Spectra.CLI`, `src/Spectra.Core`)
**Performance Goals**: 4–5× reduction in critic phase time at `max_concurrent: 5` on a 200+ test suite
**Constraints**: Parallel path must preserve original result ordering; failures in one task must not abort others; sequential path (`max_concurrent: 1`) must be identical to current behavior
**Scale/Scope**: Runs of ~250 tests with ~6s/call critic; file-backed logs of at most a few MB per run

## Constitution Check

No project constitution file present. Informal gates:

- ✅ No new external dependencies.
- ✅ Backward compatible (default `max_concurrent=1`).
- ✅ Feature is additive to existing config + logger; no breaking schema changes.
- ✅ Matches existing static-class logger pattern (`DebugLogger`) for consistency — no DI refactor required.

## Project Structure

### Documentation (this feature)

```text
specs/042-parallel-critic-error-log/
├── plan.md                  # this file
├── spec.md                  # feature spec
├── tasks.md                 # task list (/speckit.tasks output)
└── checklists/
    └── requirements.md      # spec quality checklist
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/Config/
│       ├── CriticConfig.cs          # + MaxConcurrent property
│       └── DebugConfig.cs           # + ErrorLogFile property
└── Spectra.CLI/
    ├── Infrastructure/
    │   ├── DebugLogger.cs           # existing; add ErrorLogFile cross-reference helper
    │   └── ErrorLogger.cs           # NEW: static thread-safe error log writer
    ├── Services/
    │   ├── RunErrorTracker.cs       # NEW: errors + rate_limits counters
    │   └── RunSummaryDebugFormatter.cs  # extend RUN TOTAL line
    ├── Output/
    │   └── RunSummaryPresenter.cs   # add Errors / Rate limits / Critic concurrency rows
    ├── Commands/Generate/
    │   └── GenerateHandler.cs       # parallelize VerifyTestsAsync + wire ErrorLogger/Tracker
    ├── Commands/Update/
    │   └── UpdateHandler.cs         # wire ErrorLogger/Tracker
    └── Agent/Copilot/
        ├── BehaviorAnalyzer.cs      # catch → ErrorLogger.Write
        ├── GenerationAgent.cs       # catch → ErrorLogger.Write
        ├── GroundingAgent.cs        # catch → ErrorLogger.Write + rate-limit detection
        └── CriteriaExtractor.cs     # catch → ErrorLogger.Write

tests/
├── Spectra.Core.Tests/
│   └── Config/
│       └── CriticConfigClampTests.cs             # NEW
└── Spectra.CLI.Tests/
    ├── Infrastructure/
    │   └── ErrorLoggerTests.cs                    # NEW
    ├── Services/
    │   ├── RunErrorTrackerTests.cs                # NEW
    │   └── RunSummaryDebugFormatterTests.cs       # extend
    └── Commands/Generate/
        └── ParallelCriticTests.cs                 # NEW
```

**Structure Decision**: Continue the existing static-logger + manual-composition pattern (no DI container). `ErrorLogger` mirrors `DebugLogger` (static properties for `Enabled`, `LogFile`, `Mode`; thread-safe `Write` method). `RunErrorTracker` is instance-scoped like `TokenUsageTracker` and is created per `GenerateHandler` / `UpdateHandler` instance, passed into agents that may emit errors.

## Design Decisions

### Parallelization Approach

- `SemaphoreSlim(maxConcurrent)` gates each per-test task inside `VerifyTestsAsync`.
- Tasks are created via `tests.Select(async (test, idx) => ...)` and awaited with `Task.WhenAll`.
- Results are collected into a pre-sized `(TestCase, VerificationResult)[]` indexed by original position so the returned list order is deterministic regardless of completion order.
- Manual-verdict short-circuit (existing behavior at `VerifyTestsAsync:1865`) is preserved inside the task body.
- Progress status updates use `_progress.StatusAsync` currently; this is a sequential UI construct. For parallel mode we switch to a shared progress counter and update a single status line (`Verifying N/total...`).

### Rate Limit Detection

`CopilotCritic.VerifyTestAsync` catches `Exception`. We extend that catch block to classify the exception:

- `HttpRequestException` with `StatusCode == 429`, OR
- Exception message contains "rate limit" / "too many requests" / "429" (case-insensitive fallback for providers that bury the status code)

These increment `RunErrorTracker.RateLimits`. All caught exceptions increment `RunErrorTracker.Errors`.

### ErrorLogger Contract

```csharp
public static class ErrorLogger
{
    public static bool Enabled { get; set; }
    public static string LogFile { get; set; } = ".spectra-errors.log";
    public static string Mode { get; set; } = "append";

    public static void Write(string phase, string context, Exception ex, string? responseBody = null, string? retryAfter = null);
}
```

- File is created lazily on first `Write` call. If `Mode == "overwrite"` we truncate on the first write in a run (tracked via a `_firstWriteDone` flag that resets with `BeginRun`).
- All writes go through a single `lock (_gate)` for thread safety.
- File-write failures are caught, a single stderr warning is emitted, then `Enabled` is flipped to false for the remainder of the run (graceful degradation).
- `Write` returns `void`; callers don't branch on logging success.

### Debug Log Cross-Reference

When an error is logged, the corresponding debug log line gets a new `see=<error_log_file>` suffix. We add a convenience helper `DebugLogger.AppendError(string component, string message)` that automatically includes the cross-reference when `ErrorLogger.Enabled`.

### Run Summary Extensions

- `RunSummaryDebugFormatter.FormatRunTotal` — appends ` rate_limits=<n> errors=<n>` (always present, even on zero).
- `RunSummaryPresenter.Render` — new rows in the top grid: `Critic concurrency`, `Errors`, `Rate limits`. Rate-limit row appends the hint when count > 0.

### CriticConfig Clamping

Clamping happens in a new `CriticConfig.GetEffectiveMaxConcurrent()` method called from `GenerateHandler` just before the critic loop:

```csharp
public int GetEffectiveMaxConcurrent()
{
    if (MaxConcurrent < 1) return 1;
    if (MaxConcurrent > 20) return 20;
    return MaxConcurrent;
}
```

The handler logs a one-line stderr warning when the raw value is >20 (clamped) or >10 (allowed but risky).

## Complexity Tracking

No constitution violations. No new projects, no new external dependencies.
