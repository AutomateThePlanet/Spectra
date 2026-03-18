# Research: Bundled Execution Agent & MCP Data Tools

**Branch**: `007-execution-agent-mcp-tools` | **Date**: 2026-03-19 | **Status**: Complete

## Research Tasks

### 1. GitHub Copilot Agent/Skill File Format

**Decision**: Use `.github/agents/*.agent.md` for Copilot Chat and `.github/skills/*/SKILL.md` for Copilot CLI

**Rationale**: These are the standard locations recognized by GitHub Copilot. The `.agent.md` extension signals to Copilot Chat that this is an invokable agent. The `SKILL.md` in a named subdirectory is the Copilot CLI convention.

**Agent File Format**:
```markdown
---
name: spectra-execution
description: >
  Execute manual test suites interactively using SPECTRA MCP tools.
---

# SPECTRA Test Execution Agent

[Agent prompt content...]
```

**Alternatives Considered**:
- Single file for both: Rejected because Copilot Chat and CLI look in different locations
- JSON configuration: Rejected because Markdown provides better prompt authoring experience
- External URL reference: Rejected because requires network and adds complexity

### 2. Embedding Resources in .NET CLI

**Decision**: Use MSBuild `<EmbeddedResource>` to bundle agent prompt files into the CLI assembly

**Rationale**: Embedded resources ship with the binary, no external files needed. The existing `SkillLoader` already handles embedded resource fallback. Pattern established in the codebase.

**Implementation Pattern**:
```xml
<!-- In Spectra.CLI.csproj -->
<ItemGroup>
  <EmbeddedResource Include="Agent\Resources\spectra-execution.agent.md" />
  <EmbeddedResource Include="Agent\Resources\SKILL.md" />
</ItemGroup>
```

**Loading Pattern**:
```csharp
using var stream = Assembly.GetExecutingAssembly()
    .GetManifestResourceStream("Spectra.CLI.Agent.Resources.spectra-execution.agent.md");
```

**Alternatives Considered**:
- Filesystem copy from install directory: Rejected because requires tracking install location
- Network download: Rejected because offline use is important
- Source generator: Overkill for static markdown files

### 3. MCP Tool Implementation Pattern

**Decision**: Follow existing `IMcpTool` interface pattern in `src/Spectra.MCP/Tools/`

**Rationale**: Consistent with existing tools like `StartExecutionRunTool`, `ValidateTestsTool`. Uses established `McpToolResponse<T>` format.

**Tool Structure**:
```csharp
public sealed class ValidateTestsTool : IMcpTool
{
    public string Description => "Validate test files against SPECTRA schema";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new { type = "string", description = "Suite name (optional)" }
        }
    };

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        // Deserialize, validate, execute, return McpToolResponse
    }
}
```

**Alternatives Considered**:
- New tool interface: Rejected because existing interface works well
- Separate tool library: Rejected because tools belong with MCP server

### 4. Test File Validation Rules

**Decision**: Reuse existing `TestValidator` and `TestCaseParser` from Spectra.Core

**Rationale**: Validation logic already implemented and tested. Includes ID format, required fields, priority validation, step count warnings.

**Validation Rules Applied**:
| Rule | Source | Severity |
|------|--------|----------|
| ID format matches regex | `spectra.config.json` validation.idPattern | Error |
| Title is non-empty | TestCaseParser | Error |
| Expected result is non-empty | TestCaseParser | Error |
| Priority is valid enum | TestValidator | Error |
| YAML frontmatter is valid | MarkdownFrontmatterParser | Error |
| Step count > 0 | TestValidator | Warning |
| Step count < max | TestValidator | Warning |

**Alternatives Considered**:
- JSON Schema validation: Rejected because test files are Markdown with YAML frontmatter, not pure JSON
- New validation engine: Rejected because existing engine is sufficient

### 5. Index Rebuild Strategy

**Decision**: Full rebuild using existing `IndexGenerator.GenerateAsync()` and `IndexWriter.WriteAsync()`

**Rationale**: Existing implementation handles scanning test files, extracting metadata, and writing `_index.json`. Incremental update adds complexity with marginal benefit for typical suite sizes.

**Implementation**:
```csharp
// Scan all test files in suite
var testFiles = Directory.EnumerateFiles(suitePath, "*.md")
    .Where(f => !Path.GetFileName(f).StartsWith("_"));

// Parse each test file
foreach (var file in testFiles)
{
    var result = await TestCaseParser.ParseAsync(file);
    if (result.IsSuccess) entries.Add(ToIndexEntry(result.Value));
}

// Write index atomically
await IndexWriter.WriteAsync(indexPath, new MetadataIndex { Tests = entries });
```

**Alternatives Considered**:
- Incremental update: Rejected because adds complexity, full rebuild is fast for 500 files
- File watcher: Rejected because MCP tools are on-demand, not continuous

### 6. Coverage Gap Detection Algorithm

**Decision**: Compare documentation files against aggregated `source_refs` from all test files

**Rationale**: Existing `GapAnalyzer` in CLI uses this approach. Simple set difference operation.

**Algorithm**:
```
1. Enumerate all docs in docs/ folder
2. Collect all source_refs from all test files
3. Normalize paths (relative, forward slashes)
4. gaps = docs - source_refs
5. Return gaps with path, title, severity
```

**Severity Calculation**:
| Criteria | Severity |
|----------|----------|
| Document > 10KB or > 5 headings | High |
| Document > 5KB or > 2 headings | Medium |
| Default | Low |

**Alternatives Considered**:
- Section-level tracking: Future enhancement, document-level sufficient for MVP
- AI-based similarity: Rejected because must be deterministic (no AI dependency)

### 7. Init Command Agent File Handling

**Decision**: Add `--force` flag to overwrite existing agent files; skip by default

**Rationale**: Protects user customizations. Consistent with config file handling pattern already in `InitHandler`.

**Paths to Create**:
| Path | Purpose |
|------|---------|
| `.github/agents/spectra-execution.agent.md` | Copilot Chat agent |
| `.github/skills/spectra-execution/SKILL.md` | Copilot CLI skill |

**Implementation**:
```csharp
var agentPath = Path.Combine(repoRoot, ".github", "agents", "spectra-execution.agent.md");
var skillPath = Path.Combine(repoRoot, ".github", "skills", "spectra-execution", "SKILL.md");

if (!File.Exists(agentPath) || force)
{
    Directory.CreateDirectory(Path.GetDirectoryName(agentPath)!);
    await File.WriteAllTextAsync(agentPath, GetEmbeddedResource("spectra-execution.agent.md"));
}
```

**Alternatives Considered**:
- Always overwrite: Rejected because destroys user customizations
- Prompt for overwrite: Rejected because `init` supports `--no-interactive` mode

### 8. Documentation Structure

**Decision**: Create `docs/execution-agent/` with separate guides per orchestrator

**Rationale**: Different orchestrators have different setup steps. Separate files allow focused, scannable documentation.

**Files**:
- `copilot-chat.md` - VS Code extension setup, @agent invocation
- `copilot-cli.md` - CLI installation, skill discovery
- `claude.md` - MCP configuration, project instructions
- `generic-mcp.md` - MCP protocol overview, tool reference

**Alternatives Considered**:
- Single README: Rejected because too long for multiple orchestrators
- Wiki: Rejected because should be in-repo for discoverability

## Dependencies

| Dependency | Purpose | Already in Project? |
|------------|---------|---------------------|
| Spectra.Core | Parsing, validation, indexing | Yes |
| Spectra.MCP | MCP protocol, tool registry | Yes |
| System.Text.Json | JSON serialization | Yes |
| System.CommandLine | CLI parsing | Yes |
| xUnit | Testing | Yes |

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Agent prompt breaks with Copilot updates | Keep prompt minimal, test with each Copilot release |
| Large repos slow validation | Parallelize file parsing (existing pattern) |
| Index rebuild races with execution | Use file locking (existing LockManager) |
| Coverage analysis misses nested docs | Use recursive glob pattern `docs/**/*.md` |
