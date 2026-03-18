# Feature Specification: AI Provider Completion

## Overview

Complete the AI provider implementation for Spectra CLI, enabling functional test generation with multiple AI providers including GitHub Models, OpenAI, and Anthropic.

## Problem Statement

The current implementation has critical stubs that prevent AI test generation from working:
- `CopilotAgent.cs` returns an error instead of generating tests
- `OpenAiAgent.cs` is implemented but not registered in AgentFactory
- Required NuGet packages are missing
- Test editing functionality is stubbed

## Goals

1. Enable functional AI test generation with at least one working provider
2. Support multiple AI providers with automatic fallback
3. Implement test editing in the interactive review flow
4. Provide sensible defaults that work out of the box

## Non-Goals

- Streaming responses (future enhancement)
- Provider-specific advanced features (function calling, vision, etc.)
- Cost tracking or rate limiting

## Requirements

### Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-1 | GitHub Models provider uses GITHUB_TOKEN for authentication | High |
| FR-2 | OpenAI provider uses OPENAI_API_KEY for authentication | High |
| FR-3 | Anthropic provider uses ANTHROPIC_API_KEY for authentication | High |
| FR-4 | Provider fallback follows priority order when primary fails | Medium |
| FR-5 | Test editing allows modifying title, priority, steps, expected result | Medium |
| FR-6 | Default config uses github-models as primary provider | High |

### Non-Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| NFR-1 | Provider initialization fails gracefully if API key missing | High |
| NFR-2 | Generation timeout configurable per provider | Low |
| NFR-3 | Token usage reported for all providers | Medium |

## Provider Configuration

```json
{
  "ai": {
    "providers": [
      { "name": "github-models", "model": "gpt-4o", "enabled": true, "priority": 1 },
      { "name": "openai", "model": "gpt-4o", "enabled": false, "priority": 2, "api_key_env": "OPENAI_API_KEY" },
      { "name": "anthropic", "model": "claude-sonnet-4-5", "enabled": false, "priority": 3, "api_key_env": "ANTHROPIC_API_KEY" }
    ],
    "fallback_strategy": "auto"
  }
}
```

## Success Criteria

1. `spectra ai generate` works with GITHUB_TOKEN set
2. `spectra ai generate --provider openai` works with OPENAI_API_KEY set
3. `spectra ai generate --provider anthropic` works with ANTHROPIC_API_KEY set
4. Interactive 'e' key opens test editor and saves changes
5. Provider fallback activates when primary provider fails
