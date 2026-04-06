# SPECTRA CLI — Copilot SDK Architecture Review & Refactoring

## Context

The SPECTRA CLI currently uses Semantic Kernel for AI calls and possibly raw HTTP 
requests for some providers. Based on thorough research of the GitHub Copilot SDK 
(https://github.com/github/copilot-sdk), the SDK provides capabilities we are NOT 
using properly. This review must identify all gaps and produce a refactoring plan.

## What the Copilot SDK Actually Provides

### 1. Agent Loop (NOT single-prompt calls)
The SDK provides a full agentic runtime with multi-turn conversations, tool calling, 
and iterative reasoning. The correct pattern is:

```csharp
await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    Tools = [myCustomTools],
    SystemMessage = new SystemMessageConfig { Content = "..." }
});

// Agent loop - the SDK handles tool calling automatically
var response = await session.SendAndWaitAsync(new MessageOptions
{
    Prompt = "Generate 10 test cases for citizen registration"
});
```

The key insight: `SendAndWaitAsync` runs a FULL AGENT LOOP internally — the model 
can reason, call tools, get results, reason more, and only returns when done. This 
is NOT a single completion call. If SPECTRA is using Semantic Kernel `InvokeAsync` 
or raw HTTP `POST /chat/completions`, we're missing the agent loop entirely.

### 2. BYOK (Bring Your Own Key) — Built Into the SDK
The SDK natively supports custom providers. NO NEED for separate HTTP clients or 
Semantic Kernel connectors. All providers use the SAME SDK code:

```csharp
// GitHub Copilot (default) — uses GitHub token
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o"
});

// Azure AI Foundry — BYOK
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-sonnet-4-5",
    Provider = new ProviderConfig
    {
        Type = "azure",
        BaseUrl = "https://angel-mdnhzlaw-swedencentral.services.ai.azure.com",
        ApiKey = Environment.GetEnvironmentVariable("AZURE_API_KEY")
    }
});

// Anthropic Direct — BYOK
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-sonnet-4-5",
    Provider = new ProviderConfig
    {
        Type = "anthropic",
        BaseUrl = "https://api.anthropic.com",
        ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    }
});

// OpenAI Direct — BYOK
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Provider = new ProviderConfig
    {
        Type = "openai",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    }
});
```

Important: When using BYOK, you MUST specify the model. The SDK handles all 
authentication headers, API path construction, and wire format differences 
automatically. Use `Type = "azure"` for Azure endpoints (NOT "openai").

### 3. Custom Tools — Give the Agent Domain Knowledge
The SDK supports custom function tools that the agent can call during its loop:

```csharp
AIFunction listDocs = AIFunctionFactory.Create(
    (string directory) => {
        var files = Directory.GetFiles(directory, "*.md");
        return string.Join("\n", files.Select(f => $"- {Path.GetFileName(f)}"));
    },
    "list_documentation_files",
    "List all documentation files in the given directory"
);

AIFunction readDoc = AIFunctionFactory.Create(
    (string filePath) => File.ReadAllText(filePath),
    "read_document",
    "Read the full content of a documentation file"
);

AIFunction readIndex = AIFunctionFactory.Create(
    (string suiteName) => {
        var indexPath = Path.Combine("tests", suiteName, "_index.json");
        return File.Exists(indexPath) ? File.ReadAllText(indexPath) : "No index found";
    },
    "read_test_index",
    "Read the existing test index for a suite to avoid duplicates"
);

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    Tools = [listDocs, readDoc, readIndex],
    SystemMessage = new SystemMessageConfig
    {
        Content = "You are a test generation agent. Use tools to read docs, " +
                  "check existing tests, then generate new unique test cases."
    }
});
```

With tools, the agent can:
1. List available documents (tool call)
2. Pick relevant ones (reasoning)  
3. Read document content (tool call)
4. Check existing tests (tool call)
5. Generate non-duplicate tests (reasoning + output)

This SOLVES the "load all docs at once" problem AND the "duplicate tests" problem 
naturally through the agent loop.

### 4. Multi-Model Sessions
Different sessions can use different models:

```csharp
// Generator session — powerful model
await using var generator = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-sonnet-4-5",
    Provider = new ProviderConfig { Type = "anthropic", ... }
});

// Critic session — fast cheap model  
await using var critic = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o-mini"
});
```

This enables the dual-model grounding verification pipeline natively.

### 5. Streaming Events
The SDK supports streaming for real-time CLI feedback:

```csharp
session.On(evt =>
{
    if (evt is AssistantMessageDeltaEvent delta)
        Console.Write(delta.Data.DeltaContent);
    else if (evt is ToolExecutionStartEvent toolStart)
        Console.WriteLine($"[Reading: {toolStart.Data.ToolName}]");
});
```

### 6. Infinite Sessions (Context Compaction)
For large document sets, the SDK can auto-compact context:

```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    InfiniteSessions = new InfiniteSessionConfig
    {
        Enabled = true,
        BackgroundCompactionThreshold = 0.80
    }
});
```

## YOUR TASK: Review the SPECTRA Codebase

### Step 1: Map Current Architecture
Read ALL files in the Spectra.CLI project (especially the AI-related code) and answer:

1. **How are AI calls currently made?**
   - Is there a `CopilotClient` / `CopilotSession` anywhere?
   - Or is it using Semantic Kernel `IChatCompletionService`?
   - Or raw `HttpClient` calls to `/chat/completions`?
   - List every file that makes AI calls with their approach.

2. **Is there an agent loop?**
   - When generating tests, does the code send ONE prompt and parse the response?
   - Or does it use multi-turn conversation with tool calling?
   - Does the AI get to "reason" and request more information?

3. **How are providers handled?**
   - Is there a provider abstraction layer?
   - Are there separate code paths for GitHub Models vs OpenAI vs Anthropic?
   - Is Semantic Kernel used as a provider abstraction?
   - How is authentication handled per provider?

4. **How is document content loaded?**
   - Does it load ALL docs into context at once?
   - Is there selective loading / document map?
   - What's the token usage per generation request?

5. **How are duplicates prevented?**
   - Does the AI see existing tests before generating?
   - Is there post-generation deduplication?

### Step 2: Produce a Gap Analysis

Create a table:

| Capability | SDK Provides | SPECTRA Current | Gap |
|---|---|---|---|
| Agent loop with tool calling | Yes | ? | ? |
| BYOK multi-provider | Yes (ProviderConfig) | ? | ? |
| Custom function tools | Yes (AIFunction) | ? | ? |
| Multi-model sessions | Yes | ? | ? |
| Streaming events | Yes | ? | ? |
| Context compaction | Yes (InfiniteSessions) | ? | ? |

### Step 3: Write the Refactoring Plan

Based on the gap analysis, write a detailed refactoring plan that:

1. **Replaces Semantic Kernel / HTTP calls with CopilotClient/CopilotSession**
   - Single AI abstraction layer using the SDK
   - Provider config maps to `SessionConfig.Provider` (ProviderConfig)
   - Same code path for all providers

2. **Implements tool-based generation** 
   - Define tools: `list_docs`, `read_document`, `read_test_index`, `get_document_map`
   - Let the agent decide which docs to read (solves token limit problem)
   - Let the agent check existing tests (solves duplicate problem)
   - The system prompt instructs the agent on the generation workflow

3. **Configures BYOK from spectra.config.json**
   - Map config providers to `ProviderConfig`:
     ```json
     {
       "ai": {
         "providers": [
           {
             "name": "azure-ai",
             "type": "azure",
             "model": "claude-sonnet-4-5",
             "endpoint": "https://angel-mdnhzlaw-swedencentral.services.ai.azure.com",
             "api_key_env": "AZURE_API_KEY"
           },
           {
             "name": "anthropic",
             "type": "anthropic",
             "model": "claude-sonnet-4-5",
             "endpoint": "https://api.anthropic.com",
             "api_key_env": "ANTHROPIC_API_KEY"
           }
         ]
       }
     }
     ```

4. **Enables streaming for CLI UX**
   - Show real-time generation progress
   - Show tool call events (e.g., "Reading citizen_portal_registration.md...")

5. **Implements dual-model for grounding verification**
   - Generator session (Claude/GPT-4o) → produces tests
   - Critic session (GPT-4o-mini/Gemini Flash) → validates grounding

### Step 4: Implement the Changes

After the review and plan are approved, implement the refactoring. Key files to 
create/modify:

- `Services/CopilotService.cs` — singleton CopilotClient wrapper
- `Services/GenerationAgent.cs` — agent with tools for test generation
- `Services/GroundingAgent.cs` — critic agent for verification
- `Tools/DocumentTools.cs` — AIFunction definitions for doc reading
- `Tools/TestIndexTools.cs` — AIFunction definitions for test index
- `Configuration/ProviderMapping.cs` — config to ProviderConfig mapper
- Remove: Semantic Kernel dependencies, raw HTTP AI clients, 
  separate provider implementations

### Important Notes

- The Copilot SDK requires the Copilot CLI installed (`copilot` in PATH)
- NuGet package: `dotnet add package GitHub.Copilot.SDK`
- The SDK is in technical preview (v0.1.10) — API may change
- CopilotClient should be a SINGLETON (expensive to create)
- CopilotSession should be created PER OPERATION (lightweight)
- When using BYOK with Azure endpoints, use `Type = "azure"` not "openai"
- When BYOK is used, model parameter is REQUIRED
- InfiniteSessions can handle large contexts via auto-compaction
