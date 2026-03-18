# Research: Conversational Test Generation

**Branch**: `006-conversational-generation` | **Date**: 2026-03-18 | **Status**: ✅ Applied

## Research Tasks

### 1. Mode Detection Pattern for System.CommandLine

**Decision**: Make `suite` argument optional with `Arity.ZeroOrOne`; detect mode based on presence

**Rationale**: System.CommandLine supports optional arguments via arity configuration. When suite is not provided, the handler enters interactive mode. This is cleaner than having separate commands.

**Alternatives Considered**:
- Separate commands (`spectra ai generate` vs `spectra ai converse`): Rejected because it duplicates logic and confuses users
- Global `--interactive` flag: Rejected because the no-argument trigger is more natural

**Implementation Pattern**:
```csharp
var suiteArg = new Argument<string?>("suite", () => null, "Suite name")
{
    Arity = ArgumentArity.ZeroOrOne
};

// In handler:
var isInteractive = string.IsNullOrEmpty(suite) && !noInteraction;
```

### 2. Interactive Prompts with Spectre.Console

**Decision**: Use Spectre.Console's `SelectionPrompt<T>` and `TextPrompt<string>` for interactive flows

**Rationale**: Already used in `ReviewPresenter.cs` and `ProfileQuestionnaire.cs`. Provides consistent UX across the CLI. Supports keyboard navigation, styling, and validation.

**Alternatives Considered**:
- Raw Console.ReadLine(): Rejected because no rich formatting, no selection, poor UX
- System.CommandLine interactive mode: Rejected because it's not designed for multi-turn flows

**Key Patterns from Codebase**:
```csharp
// Selection prompt (from ReviewPresenter.cs:102-105)
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("[cyan]◆ Which suite?[/]")
        .AddChoices(suites.Select(s => $"{s.Name} ({s.TestCount} tests)")));

// Text input (from ProfileQuestionnaire.cs:282-302)
var focus = AnsiConsole.Prompt(
    new TextPrompt<string>("[cyan]◆ Describe what you need:[/]")
        .AllowEmpty());
```

### 3. Coverage Gap Analysis Approach

**Decision**: Compare documentation files against test `source_refs` to identify uncovered areas

**Rationale**: Existing `AnalyzeHandler.cs:99-142` implements this pattern. Tests with `source_refs` establish coverage. Documents without covering tests are gaps.

**Alternatives Considered**:
- AI-based gap detection: Rejected for Phase 1 because it's expensive and non-deterministic
- Section-level tracking: Future enhancement, but document-level sufficient for MVP

**Implementation Pattern**:
```csharp
// From AnalyzeHandler.cs pattern
var coveredDocs = existingTests
    .SelectMany(t => t.SourceRefs ?? [])
    .Distinct()
    .ToHashSet();

var gaps = documents
    .Where(d => !coveredDocs.Contains(d.Path))
    .Select(d => new CoverageGap(d.Path, d.Title, EstimateSeverity(d)));
```

### 4. Test Classification for Updates

**Decision**: Four-state classification: UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT

**Rationale**: Spec defines these four states (FR-011). Each maps to a specific action:
- UP_TO_DATE: No action
- OUTDATED: Update test content in place
- ORPHANED: Add `status: orphaned` to frontmatter
- REDUNDANT: Flag in index (don't modify test file)

**Alternatives Considered**:
- Three states (valid/invalid/orphaned): Rejected because redundant tests need separate handling
- Five states (adding STALE): Rejected because OUTDATED covers time-based staleness

**Classification Logic**:
```csharp
public enum TestClassification
{
    UpToDate,    // source_refs exist and content matches
    Outdated,    // source_refs exist but content has changed
    Orphaned,    // source_refs point to deleted documents
    Redundant    // high similarity to another test (>90% content match)
}
```

### 5. Non-TTY Detection for CI Mode

**Decision**: Use `Console.IsInputRedirected` and `Console.IsOutputRedirected` to detect non-interactive environment

**Rationale**: Standard .NET approach. When stdin is piped, prompts would hang. Auto-fallback to `--no-interaction` behavior.

**Alternatives Considered**:
- Environment variable check: Rejected because not portable across CI systems
- Explicit `--ci` flag: Rejected because auto-detection is more user-friendly

**Implementation Pattern**:
```csharp
var isNonInteractive = noInteraction ||
    Console.IsInputRedirected ||
    !Console.IsOutputRedirected; // No TTY

if (isNonInteractive && string.IsNullOrEmpty(suite))
{
    AnsiConsole.MarkupLine("[red]✗ --suite is required in non-interactive mode[/]");
    return 1;
}
```

### 6. Rich Output Symbols and Colors

**Decision**: Use Unicode symbols with ANSI color codes via Spectre.Console markup

**Rationale**: Spec defines specific symbols (FR-016) and colors (FR-017). Spectre.Console handles terminal capability detection and fallback.

**Symbol Mapping**:
| Symbol | Meaning | Spectre.Console Markup |
|--------|---------|------------------------|
| ◆ | Interactive prompt | `[cyan]◆[/]` |
| ◐ | Loading/spinner | Via `Status()` or `Progress()` |
| ✓ | Success | `[green]✓[/]` |
| ✗ | Error | `[red]✗[/]` |
| ⚠ | Warning | `[yellow]⚠[/]` |
| ℹ | Info | `[cyan]ℹ[/]` |

**Color Scheme**:
- Green: Success states (`[green]...[/]`)
- Yellow: Warnings (`[yellow]...[/]`)
- Red: Errors (`[red]...[/]`)
- Cyan: Info/prompts (`[cyan]...[/]`)

### 7. Table Formatting for Test Listings

**Decision**: Use Spectre.Console `Table` with columns: ID, Title, Priority, Tags

**Rationale**: FR-018 specifies table format. Spectre.Console tables support column alignment, borders, and colored cells.

**Implementation Pattern**:
```csharp
var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("ID")
    .AddColumn("Title")
    .AddColumn("Priority")
    .AddColumn("Tags");

foreach (var test in generatedTests)
{
    table.AddRow(
        $"[cyan]{test.Id}[/]",
        test.Title.Truncate(50),
        PriorityColor(test.Priority),
        string.Join(", ", test.Tags ?? []));
}

AnsiConsole.Write(table);
```

### 8. Profile Auto-Loading

**Decision**: Load profile early in handler, merge with interactive selections

**Rationale**: FR-020 requires auto-loading `spectra.profile.md`. FR-021 requires interactive selections to layer on top. Existing `ProfileLoader` handles this.

**Pattern from GenerateHandler.cs:99-113**:
```csharp
var profileLoader = new ProfileLoader();
var effectiveProfile = await profileLoader.LoadAsync(
    currentDir,
    suitePath,
    cancellationToken);

// Interactive selections override/extend profile
if (!string.IsNullOrEmpty(userFocus))
{
    effectiveProfile = effectiveProfile with
    {
        FocusAreas = [..effectiveProfile.FocusAreas, userFocus]
    };
}
```

### 9. Direct Write to Disk (No Review Step)

**Decision**: Tests written immediately after generation; git is the review tool

**Rationale**: Core principle from spec. Simplifies flow, reduces cognitive load. Users review via IDE or `git diff`. Revert with `git checkout .` if unhappy.

**Implementation**:
```csharp
// No pending queue, no review loop
var writer = new TestFileWriter();
foreach (var test in generatedTests)
{
    await writer.WriteAsync(test, suitePath, ct);
}

// Update index immediately
var index = indexGenerator.Update(existingIndex, generatedTests);
await indexWriter.WriteAsync(indexPath, index, ct);

AnsiConsole.MarkupLine($"[green]✓ Written {generatedTests.Count} tests to {suitePath}[/]");
```

### 10. Exit Codes for CI Integration

**Decision**: Exit 0 for success, exit 1 for errors, with structured error messages to stderr

**Rationale**: FR-004 requires `--no-interaction` support. CI pipelines rely on exit codes.

**Exit Code Mapping**:
| Scenario | Exit Code |
|----------|-----------|
| Tests generated successfully | 0 |
| No gaps found (nothing to generate) | 0 |
| Missing required flags in CI mode | 1 |
| AI generation failed | 1 |
| No documentation found | 1 |
| Partial success (some tests written, some failed) | 0 (with warning) |

## Dependencies

| Dependency | Purpose | Already in Project? |
|------------|---------|---------------------|
| Spectre.Console | Rich terminal UX | Yes (via Review/) |
| System.CommandLine | CLI parsing | Yes |
| Microsoft.Extensions.AI | AI provider chain | Yes |
| YamlDotNet | Frontmatter parsing | Yes |
| System.Text.Json | Index serialization | Yes |

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Unicode symbols not rendering in some terminals | Spectre.Console auto-detects capabilities; fallback to ASCII |
| AI generation fails mid-way | Tests already written remain on disk; error message shows count |
| Large documentation sets cause context overflow | Existing limit of 5 docs per prompt; gap analysis prioritizes uncovered |
| Interactive mode hangs in CI | Non-TTY auto-detection + `--no-interaction` flag |
