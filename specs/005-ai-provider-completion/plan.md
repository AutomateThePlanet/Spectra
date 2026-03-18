# Implementation Plan: AI Provider Completion

## Summary

Implement missing AI provider functionality by adding NuGet packages, creating provider agents, updating the factory, and implementing test editing.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  GenerateCmd    │────▶│  AgentFactory   │────▶│  IAgentRuntime  │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                        │
                        ┌───────────────────────────────┼───────────────────────────────┐
                        │                               │                               │
                        ▼                               ▼                               ▼
               ┌─────────────────┐             ┌─────────────────┐             ┌─────────────────┐
               │ GitHubModels    │             │    OpenAI       │             │   Anthropic     │
               │     Agent       │             │     Agent       │             │     Agent       │
               └─────────────────┘             └─────────────────┘             └─────────────────┘
                        │                               │                               │
                        ▼                               ▼                               ▼
               ┌─────────────────┐             ┌─────────────────┐             ┌─────────────────┐
               │ Azure.AI.       │             │ Microsoft.Ext.  │             │   Anthropic     │
               │ Inference SDK   │             │ AI.OpenAI SDK   │             │      SDK        │
               └─────────────────┘             └─────────────────┘             └─────────────────┘
```

## Implementation Phases

### Phase 1: Infrastructure

1. Add NuGet packages to Spectra.CLI.csproj:
   - `Microsoft.Extensions.AI.OpenAI` (10.4.0)
   - `Anthropic` (12.8.0)
   - `Azure.AI.Inference` (1.0.0-beta.5)

### Phase 2: GitHub Models Provider

Create `GitHubModelsAgent.cs`:
- Uses `Azure.AI.Inference` SDK
- Base URL: `https://models.github.ai`
- Auth: `GITHUB_TOKEN` environment variable
- Default model: `gpt-4o`

### Phase 3: Anthropic Provider

Create `AnthropicAgent.cs`:
- Uses official `Anthropic` SDK
- Auth: `ANTHROPIC_API_KEY` environment variable
- Default model: `claude-sonnet-4-5`

### Phase 4: Factory Updates

Update `AgentFactory.cs`:
- Register `github-models`, `copilot` → GitHubModelsAgent
- Register `openai` → OpenAiAgent
- Register `anthropic` → AnthropicAgent
- Delete `CopilotAgent.cs`

### Phase 5: Test Editing

Create `TestEditor.cs`:
- Inline CLI editor for test cases
- Edit title, priority, steps, expected result
- Integration with GenerateHandler and TestReviewer

### Phase 6: Configuration

Update `spectra.config.json` template:
- Set github-models as default
- Include openai and anthropic as disabled alternatives

## Files Modified

| File | Action |
|------|--------|
| `Spectra.CLI.csproj` | Add packages |
| `Agent/GitHubModelsAgent.cs` | Create |
| `Agent/AnthropicAgent.cs` | Create |
| `Agent/AgentFactory.cs` | Update |
| `Agent/CopilotAgent.cs` | Delete |
| `Review/TestEditor.cs` | Create |
| `Commands/Generate/GenerateHandler.cs` | Update |
| `Review/TestReviewer.cs` | Update |
| `Templates/spectra.config.json` | Update |

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| SDK version incompatibility | Pin specific versions |
| API key exposure in logs | Never log API keys |
| Rate limiting | Implement retry with backoff (future) |
