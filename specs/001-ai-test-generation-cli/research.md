# Research: AI Test Generation CLI

**Date**: 2026-03-13
**Feature**: 001-ai-test-generation-cli

## Research Summary

This document captures technical decisions and best practices research for the AI Test Generation CLI implementation.

---

## 1. GitHub Copilot SDK Integration

### Decision
Use `CopilotClient` + `CreateSessionAsync()` with `SessionConfig` to manage AI sessions. Register custom tools via `AIFunctionFactory.Create()` from Microsoft.Extensions.AI.

### Rationale
- The Copilot SDK provides the agent execution loop (planning, tool invocation, multi-turn conversations, streaming)
- `AIFunctionFactory.Create()` automatically generates JSON schemas from C# method signatures
- BYOK support is native via `ProviderConfig` object

### Alternatives Considered
- Direct OpenAI/Anthropic SDK integration: Rejected because Copilot SDK provides unified interface with BYOK fallback
- Custom agent loop implementation: Rejected because SDK handles complexity of tool orchestration

### Key Patterns

```csharp
// Session creation with provider configuration
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = provider.Model,
    Streaming = true,
    SystemMessage = skillContent,
    Tools = toolRegistry.GetGenerationTools(),
    Provider = provider.ProviderConfig
});

// Tool registration
var tools = new[]
{
    AIFunctionFactory.Create(GetDocumentMap, "get_document_map",
        "Lightweight listing of all docs (paths, titles, headings, sizes)"),
    AIFunctionFactory.Create(LoadSourceDocumentAsync, "load_source_document",
        "Full content of a specific doc (capped at max_file_size_kb)"),
    AIFunctionFactory.Create(BatchWriteTestsAsync, "batch_write_tests",
        "Submits batch of new tests; returns validation results")
};
```

### BYOK Provider Configuration

```csharp
// For non-Copilot providers
var providerConfig = providerName switch
{
    "anthropic" => new ProviderConfig
    {
        Type = "anthropic",
        BaseUrl = "https://api.anthropic.com/v1",
        ApiKey = Environment.GetEnvironmentVariable(apiKeyEnv)
    },
    "openai" => new ProviderConfig
    {
        Type = "openai",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = Environment.GetEnvironmentVariable(apiKeyEnv)
    },
    "azure" => new ProviderConfig
    {
        Type = "azure",  // Important: use "azure", NOT "openai"
        BaseUrl = azureEndpoint,
        ApiKey = Environment.GetEnvironmentVariable(apiKeyEnv)
    },
    _ => null  // null = use default Copilot authentication
};
```

---

## 2. System.CommandLine CLI Architecture

### Decision
Use System.CommandLine with static command builders and handler classes. Global options use `Recursive = true`.

### Rationale
- Official Microsoft library for .NET CLI apps
- Built-in help generation, tab completion, and argument parsing
- Supports both sync and async handlers
- Clean separation of command definition and execution

### Alternatives Considered
- Spectre.Console.Cli: Considered for richer UI but adds complexity; can use Spectre.Console for output only
- McMaster.Extensions.CommandLineUtils: Less active development than System.CommandLine

### Command Structure

```text
spectra
├── init
├── validate
├── index
├── list
├── show <test-id>
├── config
└── ai
    ├── generate --suite <name> --count <n> --dry-run --no-review
    ├── update --suite <name> --dry-run --no-review
    └── analyze --suite <name> --format <md|json> --output <path>
```

### Global Options Pattern

```csharp
public static class GlobalOptions
{
    public static readonly Option<VerbosityLevel> VerbosityOption = new("--verbosity", "-v")
    {
        Description = "Output verbosity: quiet, minimal, normal, detailed, diagnostic",
        Recursive = true,
        DefaultValueFactory = _ => VerbosityLevel.Normal
    };

    public static readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = "Preview changes without executing",
        Recursive = true
    };

    public static readonly Option<bool> NoReviewOption = new("--no-review")
    {
        Description = "Skip interactive review (CI mode)",
        Recursive = true
    };
}
```

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | General error |
| 2 | Validation error |
| 130 | Cancelled (SIGINT) |

---

## 3. Markdown/YAML Parsing Pipeline

### Decision
Use Markdig with `UseYamlFrontMatter()` extension for parsing, YamlDotNet for deserialization. Return structured `ParseResult<T>` rather than throwing exceptions.

### Rationale
- Markdig is the most performant .NET Markdown parser (~30% fewer allocations than alternatives)
- YamlDotNet is the standard for YAML in .NET
- Result pattern enables batch processing without early termination on errors

### Alternatives Considered
- Custom frontmatter extraction: Rejected; Markdig's built-in support is robust
- Throwing exceptions on parse errors: Rejected; need to continue processing other files

### Key Patterns

```csharp
// Cache pipelines and deserializers (thread-safe, reusable)
private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
    .UseYamlFrontMatter()
    .Build();

private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .IgnoreUnmatchedProperties()
    .Build();

// Result pattern for error handling
public abstract record ParseResult<T>;
public sealed record ParseSuccess<T>(T Value) : ParseResult<T>;
public sealed record ParseFailure<T>(IReadOnlyList<ParseError> Errors) : ParseResult<T>;
```

### Performance Considerations (500+ files)

| Optimization | Implementation |
|--------------|----------------|
| Pipeline caching | Create once, reuse (thread-safe) |
| Deserializer caching | Create once, reuse (thread-safe) |
| Parallel I/O | `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 4-8` |
| Incremental updates | Track file timestamps, only process changed files |

---

## 4. Terminal UI for Streaming and Review

### Decision
Use Spectre.Console for rich terminal output (progress bars, tables, prompts) while keeping System.CommandLine for argument parsing.

### Rationale
- Spectre.Console provides polished UX without reimplementing terminal primitives
- Handles ANSI color detection, terminal width, and piped output gracefully
- Clean separation: System.CommandLine parses, Spectre.Console renders

### Key Patterns

```csharp
// Streaming with progress indication
await AnsiConsole.Status()
    .StartAsync("Generating tests...", async ctx =>
    {
        session.On(evt =>
        {
            if (evt is AssistantMessageDeltaEvent delta)
                AnsiConsole.Write(new Text(delta.Data.DeltaContent));
        });
        await session.SendAsync(new MessageOptions { Prompt = prompt });
    });

// Batch review UX
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Review generated tests:")
        .AddChoices("Accept all valid", "Review one by one", "View duplicates", "Quit"));
```

---

## 5. Lock File Strategy for Concurrency

### Decision
Use suite-level lock files with 10-minute auto-expiry. Lock file contains process ID and timestamp.

### Rationale
- Simple file-based locking works across processes
- Auto-expiry prevents deadlock from crashed processes
- Per-suite granularity allows parallel work on different suites

### Implementation

```csharp
// Lock file format: tests/{suite}/.spectra.lock
// Content: {"pid": 12345, "started": "2026-03-13T10:00:00Z"}

public async Task<IDisposable> AcquireLockAsync(string suite, CancellationToken ct)
{
    var lockPath = Path.Combine(_config.Tests.Dir, suite, ".spectra.lock");

    // Check for stale lock (> 10 minutes old)
    if (File.Exists(lockPath))
    {
        var lockInfo = JsonSerializer.Deserialize<LockInfo>(await File.ReadAllTextAsync(lockPath, ct));
        if (DateTime.UtcNow - lockInfo.Started > TimeSpan.FromMinutes(10))
        {
            File.Delete(lockPath);  // Expired, remove
        }
        else
        {
            throw new SuiteLockedException(suite, lockInfo.Pid);
        }
    }

    // Create lock
    var newLock = new LockInfo(Environment.ProcessId, DateTime.UtcNow);
    await File.WriteAllTextAsync(lockPath, JsonSerializer.Serialize(newLock), ct);

    return new LockReleaser(lockPath);
}
```

---

## 6. Structured Logging

### Decision
Use Microsoft.Extensions.Logging with configurable sinks. Support JSON output for CI and human-readable for interactive use.

### Rationale
- Standard .NET logging abstraction
- Verbosity levels map to log levels
- JSON output enables log aggregation in CI

### Verbosity Mapping

| Flag | LogLevel | Output |
|------|----------|--------|
| (none) | Warning | Errors and warnings only |
| `-v` | Information | Normal operation info |
| `-vv` | Debug | Detailed debugging |

---

## Dependencies Summary

```xml
<ItemGroup>
  <!-- CLI Framework -->
  <PackageReference Include="System.CommandLine" Version="2.*" />

  <!-- AI Integration -->
  <PackageReference Include="GitHub.Copilot.SDK" Version="1.*" />
  <PackageReference Include="Microsoft.Extensions.AI" Version="10.*" />
  <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.*" />

  <!-- Parsing -->
  <PackageReference Include="Markdig" Version="0.37.*" />
  <PackageReference Include="YamlDotNet" Version="16.*" />

  <!-- Terminal UI -->
  <PackageReference Include="Spectre.Console" Version="0.49.*" />

  <!-- Logging -->
  <PackageReference Include="Microsoft.Extensions.Logging" Version="8.*" />
  <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.*" />
</ItemGroup>
```
