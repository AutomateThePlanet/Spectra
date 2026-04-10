---
title: CLI vs Chat Generation
parent: Architecture
nav_order: 2
---

# CLI vs Chat+MCP Test Generation: Comprehensive Analysis

A detailed comparison of Spectra's current CLI-based test generation pipeline (`spectra ai generate`) against a hypothetical Chat+MCP alternative where test generation is performed conversationally through the MCP server.

> **Source of truth**: This analysis is derived from the current codebase, not spec-kit specifications.
> All implementation statuses reflect the actual code as of 2026-03-21.

---

## Part 1: Current CLI Generation Pipeline — Features & Implementation Status

### 1.1 Command Surface

```bash
spectra ai generate                              # Interactive mode (guided prompts)
spectra ai generate checkout                     # Direct mode (specific suite)
spectra ai generate checkout --focus "negative"  # Direct with focus area
spectra ai generate checkout --count 10          # Control test count
spectra ai generate checkout --no-interaction    # CI/automation mode
spectra ai generate --skip-critic                # Skip grounding verification
spectra ai generate --dry-run                    # Preview without writing files
```

**Entry point**: `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs` (60 lines)
**Handler**: `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (1010 lines)

### 1.2 Feature Inventory

| # | Feature | Status | Key Files | Description |
|---|---------|--------|-----------|-------------|
| 1 | **Document Loading & Indexing** | IMPLEMENTED | `DocumentIndexService.cs`, `SourceDocumentLoader.cs`, `DocumentMapBuilder.cs` | Loads `docs/_index.md` (incremental SHA-256 hash-based updates), full markdown content, and structured document map for gap analysis |
| 2 | **Duplicate Detection** | IMPLEMENTED | `GenerationAgent.cs` (CheckDuplicates tool) | Jaccard index similarity (60%+ threshold) checked during generation via agent tool call; prevents semantically duplicate tests |
| 3 | **Generation Profiles** | IMPLEMENTED | `ProfileLoader.cs` (259 lines) | Three-tier cascade: suite `_profile.md` → repo `spectra.profile.md` → built-in defaults. Category-level merging (detail level, negative scenarios, priority, formatting, domain, exclusions) |
| 4 | **Grounding Verification** | IMPLEMENTED | `CriticFactory.cs`, `CriticPromptBuilder.cs`, `CopilotCritic.cs` | Dual-model architecture: generator creates tests, separate critic model verifies against source docs. Three verdicts: grounded (write), partial (write with warnings), hallucinated (reject). Up to 5 docs, 8000 chars max per doc |
| 5 | **Test Count & Feedback Control** | IMPLEMENTED | `GenerateCommand.cs` (`--count`), `GenerateHandler.cs` | `--count N` controls batch size (default varies by test type). Interactive mode offers FullCoverage (30), NegativeOnly (15), SpecificArea, FreeDescription presets |
| 6 | **Global ID Allocation** | IMPLEMENTED | `GlobalIdScanner.cs`, `GetNextTestIds` tool | Scans all suites for existing IDs, allocates unique sequential IDs (1-100 per request). Prevents cross-suite ID collisions |
| 7 | **CI/CD Mode** | IMPLEMENTED | `--no-interaction` flag | Disables all interactive prompts, uses exit codes for pass/fail. Suitable for automated pipelines |
| 8 | **Interactive Mode** | IMPLEMENTED | `InteractiveSession.cs` (159 lines), suite/type/focus selectors | State machine: SuiteSelection → TestTypeSelection → FocusInput → GapAnalysis → Generating → Results → GapSelection → loop. Uses Spectre.Console for rich terminal UI |
| 9 | **Error Handling & Recovery** | IMPLEMENTED | `GenerationAgent.cs` (TryRepairTruncatedArray) | 5-minute timeout, JSON repair for truncated responses (salvages complete objects from incomplete arrays), rate limit (429) detection with retry guidance |
| 10 | **Multi-Suite Awareness** | IMPLEMENTED | `GlobalIdScanner.cs`, `GapAnalyzer.cs` | ID uniqueness enforced across all suites. Gap analysis per suite. Interactive mode allows suite selection |
| 11 | **Cost Control** | PARTIAL | Config-level provider selection | Users select provider/model in `spectra.config.json`. No per-invocation cost tracking, token counting, or budget limits |
| 12 | **Dry Run / Preview** | IMPLEMENTED | `--dry-run` flag | Runs full pipeline without writing files. Shows what would be generated |
| 13 | **Coverage Gap Analysis** | IMPLEMENTED | `GapAnalyzer.cs`, `GapPresenter.cs` | Compares document map against existing tests, identifies uncovered areas. Interactive mode allows targeted gap filling |

### 1.3 Generation Agent Architecture

The AI generation happens inside `CopilotGenerationAgent` (`src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs`, 531 lines):

```
CopilotGenerationAgent
  ├── Creates Copilot SDK session with provider config
  ├── Registers 7 tools as AIFunction instances:
  │     Document Tools (3):
  │       ├── ListDocumentationFiles  — doc structure + optional index metadata
  │       ├── ReadDocument            — full content or specific section
  │       └── SearchDocuments         — keyword search with context excerpts
  │     Test Index Tools (4):
  │       ├── ReadTestIndex           — existing tests (ID, title, priority, tags)
  │       ├── CheckDuplicates         — semantic similarity via Jaccard index
  │       ├── GetNextTestIds          — allocate 1-100 unique IDs across suites
  │       └── GetExistingTestDetails  — full test info, filterable by component
  ├── Builds system + user prompt (6-step workflow embedded)
  ├── Sends prompt via session.SendAndWaitAsync() (5-min timeout)
  └── Parses/repairs JSON response → TestCase[]
```

### 1.4 Direct Mode Flow (21 Steps)

```
GenerateCommand.cs → GenerateHandler.ExecuteAsync()
 1. Load spectra.config.json
 2. DocumentIndexService.EnsureIndexAsync()        — refresh docs/_index.md
 3. SourceDocumentLoader.LoadAllAsync()             — full markdown content
 4. DocumentMapBuilder.BuildAsync()                 — structure + previews
 5. Load existing tests from suite directory
 6. GlobalIdScanner.ScanAllIdsAsync()               — unique IDs across suites
 7. ProfileLoader.LoadAsync()                       — generation settings (3-tier cascade)
 8. GapAnalyzer.AnalyzeGaps()                       — coverage analysis
 9. Display matching tests (if --focus specified)
10. AgentFactory.CreateAgentAsync()                 — CopilotGenerationAgent
11. agent.GenerateTestsAsync()
      a. Create Copilot SDK session
      b. Register 7 tools (3 doc + 4 test index)
      c. Build system + user prompt with profile context
      d. session.SendAndWaitAsync() (5-min timeout)
      e. Parse/repair JSON response → TestCase[]
12. ShouldVerify() check (config + --skip-critic)
13. CriticFactory.TryCreateAsync()                  — create critic with configured provider
14. CriticPromptBuilder.BuildPrompt()               — select up to 5 docs, 8000 chars each
15. CopilotCritic.VerifyTestAsync() per test        — verdict + score
16. Filter hallucinated tests
17. CreateTestWithGrounding()                        — add grounding metadata to frontmatter
18. TestFileWriter.WriteAsync()                      — YAML frontmatter + markdown body
19. IndexGenerator.CreateEntry()                     — build index entries
20. IndexWriter.WriteAsync()                         — update _index.json
21. GapPresenter.ShowRemainingGaps()
```

---

## Part 2: Hypothetical Comparison — CLI vs Chat+MCP Generation

In this hypothetical, test **generation** (not just execution) would happen through a conversational AI connected to an MCP server. The AI orchestrator (Claude, Copilot Chat, etc.) would call MCP tools to read docs, generate tests, verify them, and write them — rather than using the CLI's in-process agent pipeline.

### Comparison Across 12 Dimensions

#### Dimension 1: Document Loading

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | `DocumentIndexService` → `SourceDocumentLoader` → `DocumentMapBuilder`. Three-layer pipeline: incremental index (SHA-256 hashing), full content load, structured map | MCP tools like `read_document`, `list_documents`, `search_documents` called by orchestrator on demand |
| **Efficiency** | Loads all docs upfront in one batch. Index cached between runs. Full content in memory for gap analysis | Lazy loading — only fetches what the AI asks for. May re-read documents across turns |
| **Context limits** | No limit — in-process, all docs available to prompt builder | Constrained by orchestrator's context window. Large doc sets may require chunking strategies |
| **Implementation status** | IMPLEMENTED — 3 tools + index service | Would require 3-4 new MCP tools. `GetDocumentMapTool` already exists in MCP but only for coverage analysis |

**Assessment**: CLI has a clear advantage. Batch loading is more efficient for generation, where the AI needs broad context. Chat+MCP would struggle with large documentation sets due to context window limits and the overhead of multiple tool calls to load the same content the CLI loads in one operation.

---

#### Dimension 2: Duplicate Detection

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | `CheckDuplicates` tool called by agent during generation. Jaccard index on title words, 60% threshold | Would need equivalent MCP tool. AI could also attempt semantic comparison in-context |
| **Scope** | Checked against current suite's `_index.json` via tool | Same — would read index via MCP tool |
| **Reliability** | Deterministic (algorithmic similarity) | Mix of algorithmic (via tool) and AI judgment (in-context). AI might hallucinate "this is unique" |

**Assessment**: Roughly equivalent. The dedup logic is in a tool either way. The CLI's deterministic approach is slightly more reliable than relying on the AI's in-context judgment.

---

#### Dimension 3: Generation Profiles

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | `ProfileLoader` with 3-tier cascade (suite → repo → defaults). Profile injected into prompt by `BuildPrompt()` | Would need `get_generation_profile` MCP tool. Orchestrator would need to incorporate profile into its system prompt |
| **Enforcement** | Profile settings (detail level, negative scenarios, formatting) are structurally embedded in the prompt | AI might ignore or misinterpret profile settings if they're just one tool result among many |
| **Customization** | Per-suite and per-repo YAML files, category-level merge | Same profile files, but the orchestrator needs to know to load them and how to apply them |

**Assessment**: CLI has an advantage. The profile is compiled into a structured prompt by code that understands the semantics. In Chat+MCP, the profile would be just another text blob the AI receives — compliance depends on the orchestrator's prompt engineering.

---

#### Dimension 4: Grounding Verification

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | Separate `CopilotCritic` model call per test. `CriticPromptBuilder` selects relevant docs (up to 5, 8000 chars each). Three verdicts. Hallucinated tests auto-rejected | Orchestrator could call a `verify_test_grounding` MCP tool, or the MCP server could run the critic internally |
| **Architecture** | Dual-model: generator (e.g., claude-sonnet) + critic (e.g., gemini-flash). Independent judgment | Could replicate — MCP tool calls separate model. OR orchestrator self-verifies (weaker: same model, same biases) |
| **Metadata** | Grounding score, verdict, unverified claims persisted in YAML frontmatter | Same metadata could be written by MCP tool |
| **Provider config** | `critic` section in `spectra.config.json` with separate provider/model | Same config, but MCP server would need Copilot SDK dependency for critic calls |

**Assessment**: CLI has a significant advantage. The dual-model verification is a core quality gate. Replicating it in MCP means either (a) the MCP server gains AI SDK dependencies (currently it has none — it's a pure tool server), or (b) the orchestrator self-verifies (same model, compromised independence). The CLI's clean separation of generator and critic is architecturally superior.

---

#### Dimension 5: Test Count & Feedback Control

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | `--count N` flag, test type presets (30/15/custom). Count embedded in prompt | Conversational: "Generate 10 payment tests". Count in natural language |
| **Enforcement** | Count is a hard constraint in the prompt template | AI may over/under-generate. No programmatic enforcement without a validation tool |
| **Iteration** | Interactive mode loops: generate → review gaps → generate more | Natural: "Those look good. Now generate 5 more for error handling" |

**Assessment**: Chat+MCP has a slight conversational advantage for iteration, but CLI has better enforcement. The conversational model is more natural for exploratory generation; the CLI is better for predictable batch output.

---

#### Dimension 6: Global ID Allocation

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | `GlobalIdScanner` scans all suites upfront. `GetNextTestIds` tool allocates 1-100 IDs atomically | Would need `allocate_test_ids` MCP tool with same logic |
| **Collision risk** | None — scanner is authoritative, IDs allocated before generation | Same if tool-based. Risk if AI tries to assign IDs itself |
| **Cross-suite** | Yes — scans all `_index.json` files across suites | Same — tool would scan all indexes |

**Assessment**: Equivalent if implemented as an MCP tool. The logic is the same either way. The only risk is the AI ignoring the tool and making up IDs — mitigable with validation.

---

#### Dimension 7: CI/CD Integration

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | `--no-interaction` flag, exit codes, deterministic behavior | Requires stable MCP client in CI. Must script the conversation or use a headless orchestrator |
| **Reproducibility** | Same inputs → same generation flow (model non-determinism aside) | Harder to reproduce — depends on orchestrator behavior, prompt drift, conversation state |
| **Pipeline integration** | Standard CLI tool: `spectra ai generate checkout --no-interaction --count 5` | Custom script calling MCP server, managing session, parsing responses |
| **Exit codes** | 0 = success, non-zero = failure with structured error | MCP has no concept of exit codes — would need wrapper |

**Assessment**: CLI has a decisive advantage. CI/CD pipelines are built for CLIs with flags, exit codes, and deterministic behavior. An MCP-based approach would require significant wrapper infrastructure to achieve the same level of automation reliability.

---

#### Dimension 8: Interactive / Conversational UX

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | `InteractiveSession` state machine with Spectre.Console. Structured selectors (suite, test type, focus, gaps) | Natural conversation. "I want to generate tests for the auth module, focusing on edge cases" |
| **Flexibility** | Constrained to predefined choices (FullCoverage, NegativeOnly, SpecificArea, FreeDescription) | Open-ended. User can describe intent in natural language |
| **Guidance** | Prescriptive — guides user through exact steps | Adaptive — AI interprets intent, asks clarifying questions |
| **Discoverability** | Menu-driven, all options visible | User must know what to ask. AI can suggest, but no guaranteed discovery |

**Assessment**: Chat+MCP has a clear advantage for exploratory, creative generation. The conversational model is more natural and flexible. However, CLI's structured approach is more predictable and discoverable for new users.

---

#### Dimension 9: Error Handling & Recovery

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **AI timeout** | 5-minute hard timeout with guidance to reduce `--count` | Orchestrator-dependent. Some have no timeout concept |
| **Rate limiting** | 429 detection with "wait and retry" message | Orchestrator handles its own rate limits; MCP tools don't make AI calls |
| **Truncated responses** | `TryRepairTruncatedArray()` salvages complete tests from incomplete JSON | Orchestrator manages token limits. May lose partial generation |
| **Crash recovery** | None — re-run command | Could persist generation state in SQLite (like execution runs) but not currently designed for this |
| **Partial success** | Writes successful tests, warns about failures | Could write tests incrementally via MCP tools as they're generated |

**Assessment**: CLI has better error handling for AI-specific failures (timeout, truncation, rate limits). Chat+MCP could offer better crash recovery if generation state were persisted, but this would add significant complexity.

---

#### Dimension 10: Multi-Suite Generation

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Mechanism** | One suite per invocation. Interactive mode allows suite selection | Conversational: "Generate tests for auth and checkout suites" — could span suites in one session |
| **Cross-suite context** | IDs are globally unique. Tests generated with awareness of other suites via `GetExistingTestDetails` | Same awareness if MCP tools provide cross-suite read access |
| **Batch across suites** | Requires multiple CLI invocations or scripting | Single conversation could generate across suites naturally |

**Assessment**: Chat+MCP has a slight advantage for cross-suite generation in a single session. CLI requires separate invocations per suite, though this is often desirable for isolation and predictability.

---

#### Dimension 11: Cost & Token Efficiency

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Token usage** | Single large prompt (docs + profile + existing tests) + one response. Critic is a separate smaller call | Multiple tool call rounds. Each turn includes conversation history. Context grows with each interaction |
| **Model calls** | 1 generation call + N critic calls (N = number of generated tests) | Potentially many more calls: tool results → AI processing → more tool calls → generation → verification |
| **Overhead** | Minimal — direct prompt-to-response | Significant — JSON-RPC framing, tool call overhead, conversation state |
| **Tracking** | None currently. Provider/model choice affects cost | Same limitation. Orchestrator may have its own usage tracking |

**Assessment**: CLI is significantly more token-efficient. A single, well-structured prompt with batch output is cheaper than a multi-turn conversation with tool calls. For generation workloads, this matters at scale.

---

#### Dimension 12: Maintenance & Evolution

| Aspect | CLI (Current) | Chat+MCP (Hypothetical) |
|--------|--------------|------------------------|
| **Code ownership** | Full control — `GenerateHandler.cs` (1010 lines) owns the entire pipeline | Split ownership — MCP tools + orchestrator prompt. Behavior depends on external AI behavior |
| **Testing** | 329 CLI tests + 349 core tests. Deterministic, mockable | MCP tools testable (306 tests exist). But end-to-end generation flow depends on orchestrator behavior — not testable without integration tests |
| **Prompt management** | Prompt built in code (`BuildFullPrompt()`). Profile injected programmatically | Prompt is the orchestrator's system prompt (e.g., `SKILL.md`). Less control over how it's used |
| **Version stability** | CLI version = behavior version. Upgradable, backward compatible | Orchestrator model changes can alter generation quality unpredictably |
| **Feature additions** | Add code to `GenerateHandler`, test, ship | Add MCP tool + update SKILL.md + hope orchestrator uses it correctly |

**Assessment**: CLI has a significant maintenance advantage. Full pipeline control means deterministic behavior, testability, and version-locked quality. Chat+MCP introduces dependency on external orchestrator behavior that can change without warning.

---

### Summary Matrix

| Dimension | CLI Advantage | Chat+MCP Advantage | Verdict |
|-----------|:---:|:---:|---------|
| 1. Document Loading | Strong | — | **CLI** |
| 2. Duplicate Detection | Slight | — | **CLI** (marginal) |
| 3. Generation Profiles | Moderate | — | **CLI** |
| 4. Grounding Verification | Strong | — | **CLI** |
| 5. Count & Feedback | Slight | Slight | **Tie** |
| 6. ID Allocation | — | — | **Tie** |
| 7. CI/CD Integration | Decisive | — | **CLI** |
| 8. Interactive UX | — | Strong | **Chat+MCP** |
| 9. Error Handling | Moderate | — | **CLI** |
| 10. Multi-Suite | — | Slight | **Chat+MCP** (marginal) |
| 11. Cost / Token Efficiency | Strong | — | **CLI** |
| 12. Maintenance & Evolution | Strong | — | **CLI** |

**Score**: CLI wins or ties 10 of 12 dimensions. Chat+MCP wins 2 (interactive UX and multi-suite convenience).

---

## Part 3: Recommendation

### Use Each Approach Where It Excels

**Keep CLI for structured, batch generation** (the current design):
- CI/CD pipelines and automated generation
- Predictable, repeatable output
- Grounding verification with dual-model independence
- Profile-driven generation with enforcement
- Cost-efficient batch operations
- Any scenario where quality gates and determinism matter

**Consider Chat+MCP for exploratory, conversational generation** (new capability):
- Ad-hoc test creation during development ("generate a few tests for this new feature I just wrote")
- Cross-suite exploration ("what's untested across all suites?")
- Iterative refinement in conversation ("those are too basic — add more edge cases")
- Onboarding — a conversational interface is more approachable than CLI flags

### Minimum MCP Tools for Chat-Based Generation

If adding conversational generation as a secondary mode, the MCP server would need these new tools:

| Tool | Purpose | Complexity |
|------|---------|------------|
| `read_source_document` | Load documentation content for AI context | Low — wraps existing loader |
| `search_source_documents` | Find relevant docs by keyword | Low — wraps existing search |
| `get_generation_profile` | Load effective profile for a suite | Low — wraps ProfileLoader |
| `generate_test_cases` | Core: invoke generation agent, return draft tests | High — requires AI SDK in MCP |
| `verify_test_grounding` | Run critic verification on draft tests | High — requires AI SDK in MCP |
| `write_test_files` | Write verified tests to disk + update index | Medium — wraps existing writers |
| `allocate_test_ids` | Reserve unique IDs across suites | Low — wraps GlobalIdScanner |
| `analyze_generation_gaps` | Show uncovered documentation areas | Medium — wraps GapAnalyzer |

**Critical architectural decision**: Tools 4 and 5 (`generate_test_cases`, `verify_test_grounding`) would require adding GitHub Copilot SDK as a dependency to `Spectra.MCP`. Currently the MCP server is a pure tool server with no AI dependencies — this is a deliberate design choice that keeps it lightweight, testable, and provider-agnostic. Adding AI SDK would fundamentally change its architecture.

**Alternative**: Skip tools 4-5 entirely. Let the orchestrator generate tests using its own model and doc context from tools 1-2, then call `write_test_files` to persist them. This loses grounding verification but preserves MCP server simplicity. The orchestrator could self-verify against docs it loaded, but this is weaker than independent dual-model verification.

### Verdict

**The CLI pipeline should remain the primary generation mechanism.** It wins on quality (grounding), reliability (determinism), efficiency (tokens), and operability (CI/CD). The 1010-line GenerateHandler represents significant investment in correct, testable generation logic that would be difficult to replicate in a conversational flow.

**Chat-based generation is a valid secondary mode** for exploratory use, but should not replace the CLI. If pursued, it should be designed as a lightweight complement — not a full reimplementation of the CLI pipeline.

---

## Part 4: Action Items

### Regardless of Design Choice

These improvements apply to the current CLI pipeline independent of any Chat+MCP decision:

| # | Action | Impact | Effort |
|---|--------|--------|--------|
| 1 | **Add token/cost tracking** to `CopilotGenerationAgent` | Enables cost visibility and budget limits | Medium |
| 2 | **Add `--suite` as repeatable option** for multi-suite generation in one CLI invocation | Eliminates scripting for multi-suite CI jobs | Low |
| 3 | **Persist generation history** (what was generated, from which docs, with which profile) for auditability | Supports compliance and debugging | Medium |
| 4 | **Add `--model` override flag** to CLI for per-invocation model selection without editing config | Simplifies A/B testing of models | Low |

### If Keeping CLI-Only (Recommended Path)

| # | Action | Impact | Effort |
|---|--------|--------|--------|
| 5 | **Improve interactive mode** — add natural language focus input alongside structured selectors | Captures Chat+MCP's UX advantage without architecture change | Low |
| 6 | **Add `--output json` for generation results** — machine-readable output of what was generated | Better CI integration | Low |
| 7 | **Add retry logic** with exponential backoff for rate limits and timeouts | Reduces manual re-runs | Medium |
| 8 | **Strengthen profile enforcement** — validate generated tests against profile constraints post-generation | Catches profile violations before writing | Medium |

### If Adding Chat+MCP Generation (Secondary Mode)

| # | Action | Impact | Effort |
|---|--------|--------|--------|
| 9 | **Add 3 read-only MCP tools** (`read_source_document`, `search_source_documents`, `get_generation_profile`) | Enables doc-aware conversation without AI SDK in MCP | Low |
| 10 | **Add `write_test_files` MCP tool** | Allows orchestrator to persist generated tests via MCP | Medium |
| 11 | **Add `allocate_test_ids` MCP tool** | Prevents ID collisions in conversational generation | Low |
| 12 | **Update SKILL.md** with generation workflow alongside execution workflow | Guides orchestrator through generation steps | Low |
| 13 | **Accept no grounding verification** in Chat mode, or implement as optional `verify_test_grounding` tool that adds Copilot SDK dependency to MCP | Architectural decision with significant implications | High |

### If Supporting Both Modes

| # | Action | Impact | Effort |
|---|--------|--------|--------|
| 14 | **Extract shared generation logic** into `Spectra.Core` (currently CLI-only in `GenerateHandler`) | Enables reuse by both CLI and MCP without duplication | High |
| 15 | **Define generation quality tiers**: CLI = "verified" (grounded), Chat = "draft" (unverified) | Sets clear expectations per mode | Low |
| 16 | **Add `generation_source` metadata** to test frontmatter (`cli`/`chat`/`manual`) | Traceability for how each test was created | Low |
| 17 | **Create shared test validation pipeline** that both modes call before writing | Ensures consistent quality regardless of generation mode | Medium |

---

## Appendix: Architecture Diagrams

### Current: CLI Generation Pipeline

```
User                CLI                     Copilot SDK              File System
 │                   │                          │                        │
 │── spectra ai ────→│                          │                        │
 │   generate        │── Load config ──────────→│                        │
 │                   │── Load docs ─────────────│───── read docs/ ──────→│
 │                   │── Load profile ──────────│───── read profiles ───→│
 │                   │── Scan IDs ──────────────│───── read _index.json ─→│
 │                   │── Analyze gaps ──────────│                        │
 │                   │                          │                        │
 │                   │── Create agent session ──→│                        │
 │                   │   (7 tools registered)   │                        │
 │                   │── Generate ─────────────→│                        │
 │                   │                          │── [tool calls] ──────→│
 │                   │                          │←─ [tool results] ─────│
 │                   │←── TestCase[] ───────────│                        │
 │                   │                          │                        │
 │                   │── Verify (critic) ──────→│ (separate model)       │
 │                   │←── Verdicts ─────────────│                        │
 │                   │                          │                        │
 │                   │── Write tests ───────────│───── write tests/ ────→│
 │                   │── Update index ──────────│───── write _index.json→│
 │←── Results ───────│                          │                        │
```

### Hypothetical: Chat+MCP Generation Pipeline

```
User                Orchestrator            MCP Server               File System
 │                   │                          │                        │
 │── "Generate       │                          │                        │
 │    auth tests" ──→│                          │                        │
 │                   │── read_source_document ──→│───── read docs/ ──────→│
 │                   │←── document content ─────│                        │
 │                   │── get_generation_profile→│───── read profiles ───→│
 │                   │←── profile settings ─────│                        │
 │                   │── allocate_test_ids ────→│───── scan _index.json ─→│
 │                   │←── reserved IDs ─────────│                        │
 │                   │                          │                        │
 │                   │── [AI generates tests    │                        │
 │                   │    using own model] ──┐  │                        │
 │                   │                       │  │                        │
 │                   │   (no independent     │  │                        │
 │                   │    grounding check)   │  │                        │
 │                   │←──────────────────────┘  │                        │
 │                   │                          │                        │
 │←── "Here are 5    │                          │                        │
 │    tests, look    │                          │                        │
 │    good?"         │                          │                        │
 │── "Yes, write     │                          │                        │
 │    them" ────────→│                          │                        │
 │                   │── write_test_files ──────→│───── write tests/ ────→│
 │                   │←── confirmation ─────────│───── write _index.json→│
 │←── "Done! 5 tests │                          │                        │
 │    written." ─────│                          │                        │
```

Key difference: In Chat+MCP, the orchestrator IS the generator — there's no independent verification layer unless the MCP server takes on AI SDK dependencies.
