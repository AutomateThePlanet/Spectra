# Research: MCP Execution Server

**Feature**: 002-mcp-execution-server
**Date**: 2026-03-14

## 1. MCP Protocol Implementation

### Decision
Use JSON-RPC 2.0 over stdio with ASP.NET Core minimal API as the transport layer.

### Rationale
- MCP (Model Context Protocol) uses JSON-RPC 2.0 as the wire format
- stdio transport is the standard for local MCP servers (vs SSE for remote)
- ASP.NET Core minimal API provides lightweight hosting without MVC overhead
- .NET 8 has excellent JSON-RPC support via System.Text.Json

### Alternatives Considered
- **gRPC**: Rejected - MCP spec mandates JSON-RPC
- **SignalR**: Rejected - Adds unnecessary complexity; MCP is request-response
- **Raw TCP sockets**: Rejected - stdio is the MCP standard for local servers

### Implementation Notes
```csharp
// Minimal MCP server structure
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServices();

var app = builder.Build();
app.MapMcpTools(); // Register all 14 tools
await app.RunAsync();
```

---

## 2. SQLite Database Access

### Decision
Use Microsoft.Data.Sqlite with async operations and manual SQL (no ORM).

### Rationale
- SQLite provides atomic writes for crash resilience (FR-015, SC-007)
- Microsoft.Data.Sqlite is the official .NET SQLite provider
- Manual SQL keeps the code simple and predictable (YAGNI principle)
- Async operations enable concurrent reads without blocking

### Alternatives Considered
- **EF Core**: Rejected - Adds ORM complexity for 2 tables; overkill for simple CRUD
- **Dapper**: Considered but rejected - Adds dependency for minimal benefit
- **LiteDB**: Rejected - SQLite has better tooling and is explicitly in architecture spec

### Implementation Notes
```csharp
// Connection management
public sealed class ExecutionDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public ExecutionDb(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await CreateTablesAsync();
    }
}
```

### Schema
```sql
CREATE TABLE IF NOT EXISTS runs (
    run_id TEXT PRIMARY KEY,
    suite TEXT NOT NULL,
    status TEXT NOT NULL,
    started_at TEXT NOT NULL,
    started_by TEXT NOT NULL,
    environment TEXT,
    filters TEXT,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS test_results (
    run_id TEXT NOT NULL,
    test_id TEXT NOT NULL,
    test_handle TEXT NOT NULL UNIQUE,
    status TEXT NOT NULL,
    notes TEXT,
    started_at TEXT,
    completed_at TEXT,
    attempt INTEGER NOT NULL DEFAULT 1,
    blocked_by TEXT,
    PRIMARY KEY (run_id, test_id, attempt),
    FOREIGN KEY (run_id) REFERENCES runs(run_id)
);

CREATE INDEX IF NOT EXISTS idx_results_run ON test_results(run_id);
CREATE INDEX IF NOT EXISTS idx_results_handle ON test_results(test_handle);
```

---

## 3. State Machine Design

### Decision
Enum-based states with a validated transition matrix.

### Rationale
- Simple and explicit - easy to understand and test
- Transition validation prevents invalid state changes (FR-019)
- Constitution Principle II requires deterministic execution with validated transitions

### State Definitions

**Run States**:
```
CREATED → RUNNING → PAUSED → RUNNING → COMPLETED
                  ↘ CANCELLED
         (timeout) → ABANDONED
```

**Test States**:
```
PENDING → IN_PROGRESS → PASSED | FAILED | BLOCKED | SKIPPED
```

### Transition Matrix
```csharp
private static readonly Dictionary<(RunStatus From, RunStatus To), bool> _validTransitions = new()
{
    { (RunStatus.Created, RunStatus.Running), true },
    { (RunStatus.Running, RunStatus.Paused), true },
    { (RunStatus.Running, RunStatus.Completed), true },
    { (RunStatus.Running, RunStatus.Cancelled), true },
    { (RunStatus.Paused, RunStatus.Running), true },
    { (RunStatus.Paused, RunStatus.Cancelled), true },
    { (RunStatus.Paused, RunStatus.Abandoned), true },
};

public bool CanTransition(RunStatus from, RunStatus to)
    => _validTransitions.TryGetValue((from, to), out var valid) && valid;
```

### Alternatives Considered
- **Stateless library**: Rejected - Adds dependency for simple transitions
- **Workflow engine**: Rejected - Massive overkill; simple enum transitions suffice

---

## 4. Test Handle Generation

### Decision
Format: `{run_uuid_prefix}-{test_id}-{random_suffix}`

Example: `a3f7c291-TC104-x9k2`

### Rationale
- Opaque handles prevent context manipulation (FR-025)
- Run prefix ties handle to specific run
- Test ID included for debugging (but not relied upon)
- Random suffix prevents guessing
- Short enough to not overflow LLM context

### Implementation
```csharp
public static string GenerateHandle(string runId, string testId)
{
    var runPrefix = runId[..8]; // First 8 chars of UUID
    var suffix = Convert.ToBase64String(RandomNumberGenerator.GetBytes(3))
        .Replace("+", "x").Replace("/", "k")[..4];
    return $"{runPrefix}-{testId}-{suffix}";
}
```

### Validation
```csharp
public bool ValidateHandle(string handle, string runId, string testId)
{
    if (string.IsNullOrEmpty(handle)) return false;
    var parts = handle.Split('-');
    if (parts.Length < 3) return false;
    return runId.StartsWith(parts[0]) && parts[1] == testId;
}
```

---

## 5. Report Generation

### Decision
Generate both JSON and Markdown reports side-by-side (per spec clarification).

### Rationale
- JSON enables programmatic consumption and automation
- Markdown is immediately readable in GitHub PRs and repos
- Dual format maximizes utility without significant overhead

### JSON Structure
```json
{
  "run_id": "a3f7c291-...",
  "suite": "checkout",
  "environment": "staging",
  "started_at": "2026-03-14T10:00:00Z",
  "completed_at": "2026-03-14T11:30:00Z",
  "executed_by": "user@example.com",
  "status": "COMPLETED",
  "summary": {
    "total": 15,
    "passed": 12,
    "failed": 2,
    "skipped": 1,
    "blocked": 0
  },
  "results": [...]
}
```

### Markdown Structure
```markdown
# Execution Report: checkout

**Run ID**: a3f7c291-...
**Status**: COMPLETED
**Executed By**: user@example.com
**Started**: 2026-03-14 10:00:00
**Completed**: 2026-03-14 11:30:00

## Summary

| Status | Count |
|--------|-------|
| Passed | 12 |
| Failed | 2 |
| Skipped | 1 |
| Blocked | 0 |
| **Total** | **15** |

## Results

### Passed (12)
- TC-101: Checkout with valid Visa card
- TC-102: ...

### Failed (2)
- TC-104: Checkout with expired card
  - Notes: Card validation not working in staging

...
```

---

## 6. Logging Strategy

### Decision
Microsoft.Extensions.Logging with Serilog file sink; configurable verbosity (-v, -vv) per FR-029.

### Rationale
- Consistent with Spectra.CLI logging approach
- Structured logging enables filtering and analysis
- File sink provides persistent logs for debugging
- Verbosity levels match user expectations

### Configuration
```csharp
public enum VerbosityLevel
{
    Normal = 0,    // Errors, warnings, key milestones
    Verbose = 1,   // -v: Add info-level details
    Debug = 2      // -vv: Add debug traces
}
```

### Log Categories
- `Spectra.MCP.Tools` - Tool invocations and responses
- `Spectra.MCP.Execution` - State transitions
- `Spectra.MCP.Storage` - Database operations

---

## 7. User Identity Resolution

### Decision
Derive from environment: git config user.name, fallback to OS username (per spec clarification).

### Rationale
- No explicit flag needed for most users
- Git config is standard for development environments
- OS username provides reasonable fallback
- Matches existing team workflows

### Implementation
```csharp
public class UserIdentityResolver
{
    public async Task<string> ResolveAsync()
    {
        // Try git config first
        var gitUser = await GetGitConfigUserAsync();
        if (!string.IsNullOrEmpty(gitUser))
            return gitUser;

        // Fallback to OS username
        return Environment.UserName;
    }

    private async Task<string?> GetGitConfigUserAsync()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "config user.name",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            var output = await process!.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
```

---

## 8. Dependency Cascade Implementation

### Decision
Implement transitive blocking: blocked tests block their dependents (per spec clarification).

### Rationale
- Logical consistency - if Test B can't run because Test A was blocked, Test C (depending on B) also can't run
- Prevents confusion and wasted effort
- Clear audit trail of why tests were blocked

### Algorithm
```csharp
public void PropagateBlocks(string failedOrSkippedTestId, string reason)
{
    var queue = new Queue<string>();
    queue.Enqueue(failedOrSkippedTestId);

    while (queue.Count > 0)
    {
        var testId = queue.Dequeue();
        var dependents = GetDirectDependents(testId);

        foreach (var dependent in dependents)
        {
            if (GetTestStatus(dependent) == TestStatus.Pending)
            {
                SetTestStatus(dependent, TestStatus.Blocked, $"Blocked by {testId}: {reason}");
                queue.Enqueue(dependent); // Cascade to dependents of dependents
            }
        }
    }
}
```

---

## 9. Concurrency Model

### Decision
Suite-level isolation with user validation per run.

### Rules
- Same user, different suites: Allowed
- Same user, same suite: Blocked (must finalize/cancel first) per FR-016
- Different users, same suite: Allowed (independent runs) per FR-017

### Implementation
```csharp
public async Task<Result<Run>> StartRunAsync(string suite, string user, Filters? filters)
{
    // Check for existing active run by same user on same suite
    var existing = await _runRepo.GetActiveRunAsync(suite, user);
    if (existing != null)
    {
        return Result<Run>.Failure(
            $"Active run exists: {existing.RunId}. Resume or cancel before starting new run.");
    }

    // Create new run
    var run = new Run
    {
        RunId = Guid.NewGuid().ToString(),
        Suite = suite,
        Status = RunStatus.Created,
        StartedBy = user,
        StartedAt = DateTime.UtcNow,
        Filters = filters
    };

    await _runRepo.InsertAsync(run);
    return Result<Run>.Success(run);
}
```

---

## 10. MCP Tool Response Format

### Decision
Self-contained responses with `run_status`, `progress`, and `next_expected_action`.

### Rationale
- Constitution Principle III requires orchestrator-agnostic design
- Orchestrators should not need to remember prior calls
- Explicit next action guides the LLM without ambiguity

### Standard Response Fields
```json
{
  "data": { ... },           // Tool-specific result
  "run_status": "RUNNING",   // Current run state
  "progress": "8/15",        // Tests completed / total
  "next_expected_action": "get_test_case_details"  // What to call next
}
```

### Error Response Format
```json
{
  "error": "INVALID_TRANSITION",
  "message": "Cannot advance: run is PAUSED. Call resume_execution_run first.",
  "current_run_status": "PAUSED",
  "next_expected_action": "resume_execution_run"
}
```
