# Data Model: AI Test Generation CLI

**Date**: 2026-03-13
**Feature**: 001-ai-test-generation-cli

## Overview

This document defines the data models for the AI Test Generation CLI. All models are implemented in `Spectra.Core` as plain C# classes/records with no framework dependencies.

---

## Core Entities

### TestCase

A manual test stored as a Markdown file with YAML frontmatter.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Represents a parsed test case from a Markdown file.
/// </summary>
public sealed class TestCase
{
    // Identity
    public required string Id { get; init; }           // e.g., "TC-102"
    public required string FilePath { get; init; }     // Relative path from tests/

    // Metadata (from frontmatter)
    public required Priority Priority { get; init; }   // high, medium, low
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? Component { get; init; }
    public string? Preconditions { get; init; }
    public IReadOnlyList<string> Environment { get; init; } = [];
    public TimeSpan? EstimatedDuration { get; init; }
    public string? DependsOn { get; init; }            // Test ID dependency
    public IReadOnlyList<string> SourceRefs { get; init; } = [];
    public IReadOnlyList<string> RelatedWorkItems { get; init; } = [];
    public IReadOnlyDictionary<string, object>? Custom { get; init; }

    // Content (from Markdown body)
    public required string Title { get; init; }        // First H1
    public IReadOnlyList<string> Steps { get; init; } = [];
    public required string ExpectedResult { get; init; }
    public string? TestData { get; init; }
}

public enum Priority
{
    High,
    Medium,
    Low
}
```

### TestCaseFrontmatter

YAML frontmatter DTO for deserialization (YamlDotNet requires classes, not records).

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// DTO for YAML frontmatter deserialization.
/// Use TestCase for business logic.
/// </summary>
public sealed class TestCaseFrontmatter
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "priority")]
    public string Priority { get; set; } = string.Empty;

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; set; } = [];

    [YamlMember(Alias = "component")]
    public string? Component { get; set; }

    [YamlMember(Alias = "preconditions")]
    public string? Preconditions { get; set; }

    [YamlMember(Alias = "environment")]
    public List<string> Environment { get; set; } = [];

    [YamlMember(Alias = "estimated_duration")]
    public string? EstimatedDuration { get; set; }

    [YamlMember(Alias = "depends_on")]
    public string? DependsOn { get; set; }

    [YamlMember(Alias = "source_refs")]
    public List<string> SourceRefs { get; set; } = [];

    [YamlMember(Alias = "related_work_items")]
    public List<string> RelatedWorkItems { get; set; } = [];

    [YamlMember(Alias = "custom")]
    public Dictionary<string, object>? Custom { get; set; }
}
```

### TestSuite

A collection of tests in a folder.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Represents a test suite (folder of tests with index).
/// </summary>
public sealed class TestSuite
{
    public required string Name { get; init; }         // Folder name (e.g., "checkout")
    public required string Path { get; init; }         // Relative path (e.g., "tests/checkout")
    public required MetadataIndex Index { get; init; }
    public int TestCount => Index.TestCount;
}
```

### MetadataIndex

The `_index.json` file structure.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Represents the _index.json metadata for a suite.
/// </summary>
public sealed class MetadataIndex
{
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("generated_at")]
    public required DateTime GeneratedAt { get; init; }

    [JsonPropertyName("test_count")]
    public int TestCount => Tests.Count;

    [JsonPropertyName("tests")]
    public required IReadOnlyList<TestIndexEntry> Tests { get; init; }
}

public sealed class TestIndexEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("priority")]
    public required string Priority { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("component")]
    public string? Component { get; init; }

    [JsonPropertyName("depends_on")]
    public string? DependsOn { get; init; }

    [JsonPropertyName("source_refs")]
    public IReadOnlyList<string> SourceRefs { get; init; } = [];
}
```

### DocumentMap

Lightweight listing of documentation files.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Lightweight map of all documentation files for AI context selection.
/// </summary>
public sealed class DocumentMap
{
    [JsonPropertyName("doc_count")]
    public int DocCount => Documents.Count;

    [JsonPropertyName("total_size_kb")]
    public required int TotalSizeKb { get; init; }

    [JsonPropertyName("documents")]
    public required IReadOnlyList<DocumentEntry> Documents { get; init; }
}

public sealed class DocumentEntry
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("size_kb")]
    public required int SizeKb { get; init; }

    [JsonPropertyName("headings")]
    public required IReadOnlyList<string> Headings { get; init; }

    [JsonPropertyName("first_200_chars")]
    public required string Preview { get; init; }
}
```

---

## Configuration Model

### SpectraConfig

Root configuration from `spectra.config.json`.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Root configuration loaded from spectra.config.json.
/// </summary>
public sealed class SpectraConfig
{
    [JsonPropertyName("source")]
    public required SourceConfig Source { get; init; }

    [JsonPropertyName("tests")]
    public required TestsConfig Tests { get; init; }

    [JsonPropertyName("ai")]
    public required AiConfig Ai { get; init; }

    [JsonPropertyName("generation")]
    public GenerationConfig Generation { get; init; } = new();

    [JsonPropertyName("update")]
    public UpdateConfig Update { get; init; } = new();

    [JsonPropertyName("suites")]
    public IReadOnlyDictionary<string, SuiteConfig> Suites { get; init; } =
        new Dictionary<string, SuiteConfig>();

    [JsonPropertyName("git")]
    public GitConfig Git { get; init; } = new();

    [JsonPropertyName("validation")]
    public ValidationConfig Validation { get; init; } = new();
}

public sealed class SourceConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "local";

    [JsonPropertyName("local_dir")]
    public string LocalDir { get; init; } = "docs/";

    [JsonPropertyName("space_name")]
    public string? SpaceName { get; init; }

    [JsonPropertyName("doc_index")]
    public string? DocIndex { get; init; }

    [JsonPropertyName("max_file_size_kb")]
    public int MaxFileSizeKb { get; init; } = 50;

    [JsonPropertyName("include_patterns")]
    public IReadOnlyList<string> IncludePatterns { get; init; } = ["**/*.md"];

    [JsonPropertyName("exclude_patterns")]
    public IReadOnlyList<string> ExcludePatterns { get; init; } = ["**/CHANGELOG.md"];
}

public sealed class TestsConfig
{
    [JsonPropertyName("dir")]
    public string Dir { get; init; } = "tests/";

    [JsonPropertyName("id_prefix")]
    public string IdPrefix { get; init; } = "TC";

    [JsonPropertyName("id_start")]
    public int IdStart { get; init; } = 100;
}

public sealed class AiConfig
{
    [JsonPropertyName("providers")]
    public required IReadOnlyList<ProviderConfig> Providers { get; init; }

    [JsonPropertyName("fallback_strategy")]
    public string FallbackStrategy { get; init; } = "auto";
}

public sealed class ProviderConfig
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("priority")]
    public int Priority { get; init; } = 1;

    [JsonPropertyName("api_key_env")]
    public string? ApiKeyEnv { get; init; }

    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }
}

public sealed class GenerationConfig
{
    [JsonPropertyName("default_count")]
    public int DefaultCount { get; init; } = 15;

    [JsonPropertyName("require_review")]
    public bool RequireReview { get; init; } = true;

    [JsonPropertyName("duplicate_threshold")]
    public double DuplicateThreshold { get; init; } = 0.6;

    [JsonPropertyName("categories")]
    public IReadOnlyList<string> Categories { get; init; } =
        ["happy_path", "negative", "boundary", "integration"];
}

public sealed class UpdateConfig
{
    [JsonPropertyName("chunk_size")]
    public int ChunkSize { get; init; } = 30;

    [JsonPropertyName("require_review")]
    public bool RequireReview { get; init; } = true;
}

public sealed class SuiteConfig
{
    [JsonPropertyName("component")]
    public string? Component { get; init; }

    [JsonPropertyName("relevant_docs")]
    public IReadOnlyList<string> RelevantDocs { get; init; } = [];

    [JsonPropertyName("default_tags")]
    public IReadOnlyList<string> DefaultTags { get; init; } = [];

    [JsonPropertyName("default_priority")]
    public string DefaultPriority { get; init; } = "medium";
}

public sealed class GitConfig
{
    [JsonPropertyName("auto_branch")]
    public bool AutoBranch { get; init; } = true;

    [JsonPropertyName("branch_prefix")]
    public string BranchPrefix { get; init; } = "spectra/";

    [JsonPropertyName("auto_commit")]
    public bool AutoCommit { get; init; } = true;

    [JsonPropertyName("auto_pr")]
    public bool AutoPr { get; init; } = false;
}

public sealed class ValidationConfig
{
    [JsonPropertyName("required_fields")]
    public IReadOnlyList<string> RequiredFields { get; init; } = ["id", "priority"];

    [JsonPropertyName("allowed_priorities")]
    public IReadOnlyList<string> AllowedPriorities { get; init; } = ["high", "medium", "low"];

    [JsonPropertyName("max_steps")]
    public int MaxSteps { get; init; } = 20;

    [JsonPropertyName("id_pattern")]
    public string IdPattern { get; init; } = @"^TC-\d{3,}$";
}
```

---

## Result Types

### ParseResult

For error handling without exceptions.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Base result type for operations that can fail.
/// </summary>
public abstract record ParseResult<T>;

public sealed record ParseSuccess<T>(T Value) : ParseResult<T>;

public sealed record ParseFailure<T>(IReadOnlyList<ParseError> Errors) : ParseResult<T>;

public sealed record ParseError(
    string Code,
    string Message,
    string? FilePath = null,
    int? Line = null,
    int? Column = null);
```

### ValidationResult

For test case validation.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Result of validating test cases.
/// </summary>
public sealed record ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public required IReadOnlyList<ValidationError> Errors { get; init; }
    public required IReadOnlyList<ValidationWarning> Warnings { get; init; }
}

public sealed record ValidationError(
    string Code,
    string Message,
    string FilePath,
    string? TestId = null);

public sealed record ValidationWarning(
    string Code,
    string Message,
    string FilePath,
    string? TestId = null);
```

### BatchWriteResult

For AI tool responses.

```csharp
namespace Spectra.Core.Models;

/// <summary>
/// Result of batch_write_tests tool call.
/// </summary>
public sealed record BatchWriteResult
{
    [JsonPropertyName("submitted")]
    public required int Submitted { get; init; }

    [JsonPropertyName("valid")]
    public required int Valid { get; init; }

    [JsonPropertyName("duplicates")]
    public required int Duplicates { get; init; }

    [JsonPropertyName("invalid")]
    public required int Invalid { get; init; }

    [JsonPropertyName("details")]
    public required IReadOnlyList<TestWriteDetail> Details { get; init; }
}

public sealed record TestWriteDetail
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }  // "valid", "duplicate", "invalid"

    [JsonPropertyName("similar_to")]
    public string? SimilarTo { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
```

---

## State Transitions

### Test Update Classification

```
Documentation Changed
        │
        ▼
┌───────────────────────────────────────┐
│           Compare Test vs Docs         │
└───────────────────────────────────────┘
        │
        ├─── No changes ──────► UP_TO_DATE
        │
        ├─── Doc updated ─────► OUTDATED
        │
        ├─── Doc removed ─────► ORPHANED
        │
        └─── Matches another ─► REDUNDANT
```

### Lock File Lifecycle

```
Process Start
     │
     ▼
[Check Lock File]
     │
     ├─── Not exists ────► Create lock ──► Execute ──► Release lock
     │
     ├─── Exists, < 10min ► FAIL (locked)
     │
     └─── Exists, > 10min ► Delete (stale) ──► Create lock ──► Execute ──► Release lock
```

---

## Relationships

```
SpectraConfig
     │
     ├── SourceConfig (docs/ settings)
     │
     ├── TestsConfig (tests/ settings)
     │
     ├── AiConfig
     │       └── ProviderConfig[]
     │
     └── SuiteConfig{} (per-suite overrides)

TestSuite
     │
     └── MetadataIndex (_index.json)
              │
              └── TestIndexEntry[]

DocumentMap
     │
     └── DocumentEntry[]

TestCase (parsed from .md file)
     │
     └── TestCaseFrontmatter (YAML section)
```
