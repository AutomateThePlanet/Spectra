# Tasks: AI Provider Completion

## Task List

### Infrastructure

- [ ] **TASK-001**: Add NuGet packages to Spectra.CLI.csproj
  - Microsoft.Extensions.AI.OpenAI (10.4.0)
  - Anthropic (12.8.0)
  - Azure.AI.Inference (1.0.0-beta.5)

### Provider Implementation

- [ ] **TASK-002**: Create GitHubModelsAgent.cs
  - Implement IAgentRuntime using Azure.AI.Inference
  - Use GITHUB_TOKEN for authentication
  - Default model: gpt-4o
  - Base URL: https://models.github.ai

- [ ] **TASK-003**: Create AnthropicAgent.cs
  - Implement IAgentRuntime using Anthropic SDK
  - Use ANTHROPIC_API_KEY for authentication
  - Default model: claude-sonnet-4-5

- [ ] **TASK-004**: Update AgentFactory.cs
  - Add switch cases for github-models, openai, anthropic
  - Map copilot/github-copilot to GitHubModelsAgent
  - Update GetAvailableProviders()

- [ ] **TASK-005**: Delete CopilotAgent.cs
  - Remove stubbed implementation

### Test Editing

- [ ] **TASK-006**: Create Review/TestEditor.cs
  - Inline CLI editor for TestCase
  - Edit title, priority, steps, expected result
  - Return null if user cancels

- [ ] **TASK-007**: Fix GenerateHandler.cs edit case
  - Replace TODO at line 341
  - Use TestEditor for inline editing

- [ ] **TASK-008**: Fix TestReviewer.EditTestAsync
  - Replace stub at line 156
  - Use TestEditor for editing

### Configuration

- [ ] **TASK-009**: Update Templates/spectra.config.json
  - Set github-models as default provider
  - Add openai and anthropic as disabled alternatives

### Verification

- [ ] **TASK-010**: Build and verify
  - Run dotnet build
  - Fix any compilation errors

## Dependencies

```
TASK-001 ──┬──▶ TASK-002
           ├──▶ TASK-003
           └──▶ TASK-004 ──▶ TASK-005

TASK-006 ──┬──▶ TASK-007
           └──▶ TASK-008

All ──▶ TASK-010
```
