# 00 — Migration surface (summary)

> **Purpose.** One-page map of the v2 migration seam, drawn from the area investigations
> (`01`–`06`). Each row cites the load-bearing file. This is the evidence base for the
> `/speckit.specify` specs that follow — not a spec itself.
>
> **Inputs read:** `docs/architecture/ARCHITECTURE-v2.md` (identical to
> `docs/specs/ARCHITECTURE-v2.md` — verified by `diff`). **`INVESTIGATION-claude-code-
> mechanics.md` does not exist** in the repo; its four conclusions survive only inline in
> ARCHITECTURE-v2 §113–122 (open question Q-2 below).

---

## The seam, in one sentence

> **The CLI's `session.SendAndWaitAsync` / `SendAsync` calls — generation
> (`GenerationAgent.cs:239`), criteria extraction (`CriteriaExtractor.cs:85`,
> `RequirementsExtractor.cs:72`), and critic (`GroundingAgent.cs:124`) — are the entire
> seam; everything before them is deterministic prompt compilation that survives, and
> everything after them is deterministic parse/validate/persist that survives.**

The migration deletes those four call sites from C#, moves the generative turn to the user's
interactive Claude Code session, and re-enters the CLI at the parse/validate boundary — with
the retry that today is absent for generation re-expressed as skill choreography.

---

## Component → disposition

| Component | Disposition | Key reference |
|---|---|---|
| Generation model call | **changes at seam** (deleted from CLI) | `Spectra.CLI/Agent/Copilot/GenerationAgent.cs:239` |
| `BuildFullPrompt` (prompt compiler) | **wrapper-only change** (relocate out of Copilot agent; itself model-free) | `GenerationAgent.cs:448` |
| Response parse (`ParseTestsFromResponse` → `ParseTestCase`) | **reused verbatim** (becomes the boundary net) | `GenerationAgent.cs:537, 678` |
| Criteria extraction (`CriteriaExtractor`, typed result) | **changes at seam** | `CriteriaExtractor.cs:85`; `CriteriaExtractionResult.cs:37` |
| Legacy `RequirementsExtractor` (`docs index`) | **changes at seam** (different failure semantics — throws) | `RequirementsExtractor.cs:72, 88` |
| Extraction retry loop | **wrapper-only change** (pattern moves to skill choreography) | `AnalyzeHandler.cs:102, 115` |
| Critic (`CopilotCritic`) | **changes at seam** → `context: fork` subagent | `GroundingAgent.cs:124`; invoked at `GenerateHandler.cs:817` |
| Critic prompt/parse contract | **reused verbatim** (isolation already matches v2) | `CriticPromptBuilder.cs:76`; `CriticResponseParser.cs:25` |
| `CopilotService`, `ProviderMapping`, `AgentFactory` | **dead/removed** (for generation/critic) | `CopilotService.cs:61`; `ProviderMapping.cs:16`; `AgentFactory.cs:62` |
| `TestPersistenceService` (write+index) | **reused verbatim** | `Spectra.CLI/IO/TestPersistenceService.cs:36` |
| Index / validation / coverage / parsing (all `Spectra.Core`) | **reused verbatim** (zero model refs — grep-proven) | `Spectra.Core/Coverage/AutomationScanner.cs:63`; `DuplicateDetector.cs:48` |
| MCP execution engine + 25 tools | **reused verbatim** (client-agnostic) | `Spectra.MCP/Program.cs:135–166`; `ToolRegistry.cs:43` |
| Screenshot-by-path | **reused verbatim** (already MCP-saves-to-file) | `SaveScreenshotTool.cs:131, 203` |
| `execution.copilot_space*`, `github/*` Spaces tools | **dead/removed** (feature gap — needs replacement) | `ExecutionConfig.cs:10–14`; `spectra-execution.agent.md:6–7, 91–93` |
| Orchestration: 2 agents + 14 skills (`model: GPT-4o`, `disable-model-invocation`, `{{…TOOLS}}`) | **changes at seam** → `.claude/skills/<name>/SKILL.md` | `Spectra.CLI/Skills/Content/Agents/`, `Content/Skills/` |
| Critic skill (`context: fork`) | **net-new** | (none today) |
| `mcp__spectra__*` allowlist | **net-new** (client-side) | `.claude/settings.local.json` (absent) |
| `ai.providers` model select, `ai.fallback_strategy` | **dead/removed** | `AiConfig.cs:10, 13` |
| `analysis.max_prompt_tokens`, `debug.enabled` | **reused verbatim** (cost lever + measurement) | `AnalysisConfig.cs:21`; `DebugConfig.cs` |
| `ai.critic.model` | **wrapper-only change** (value → Sonnet 4.6; key already exists) | `CriticConfig.cs:30` |

---

## Confirmed (with evidence)

1. The entire `Spectra.Core` library and the MCP server contain **zero** model/Copilot
   references (two greps). The reuse surface is everything model-free in Core + MCP + the
   deterministic CLI persisters/validators/parsers (`03`, `04`).
2. The model seam is exactly four C# call sites (seam sentence above), all in
   `Spectra.CLI/Agent/Copilot/` (`01`, `02`).
3. The critic already runs isolated — separate session, sees only artifact + ≤5 docs, never
   the generator's reasoning (`CriticPromptBuilder.cs:76–141`). v2 formalizes an existing
   property (`02` §3).
4. Generation has **no in-CLI retry** and **no `response_format`**; the only model-output
   retry today is per-document extraction (`AnalyzeHandler.cs:102`). The "retire
   response_format" task is a no-op (`01` §1.4, `06` §3).
5. The migration prompt's Area E paths (`.github/skills/`, `.github/agents/`,
   `.vscode/mcp.json`) are **empty/absent**; artifacts are CLI-bundled embedded resources
   (`05` §1–2).
6. Screenshot-by-path and the client-agnostic execution loop v2 wants **already exist**
   (`SaveScreenshotTool.cs:203`; `04` §3, §5).
7. `mcp__spectra__*` pre-approval is **not** present in `.claude/settings.local.json` (`05`
   F-4).

## Open questions — need a human decision

- **Q-1 (canonical doc).** Two identical `ARCHITECTURE-v2.md` copies exist
  (`docs/specs/` and `docs/architecture/`). Which is canonical, and should the duplicate be
  removed? (Did not touch either.)
- **Q-2 (missing mechanics doc).** `INVESTIGATION-claude-code-mechanics.md` does not exist.
  Author it, or treat ARCHITECTURE-v2 §113–122 as the canonical record of the four verified
  mechanics?
- **Q-3 (critic family).** ARCHITECTURE-v2 §29/§32 favours same-family Sonnet 4.6; the code
  comments (`GroundingAgent.cs:197`) still favour cross-architecture. Settle via the §32
  defect-injection bake-off, or set Sonnet 4.6 now and defer the bake-off?
- **Q-4 (extractor merge).** `CriteriaExtractor` (typed result) and `RequirementsExtractor`
  (throws) diverge in failure semantics (`01` F-2). In scope to merge during migration, or
  keep deferred per Spec 047?
- **Q-5 (Copilot Spaces replacement).** `execution.copilot_space*` + `github/*` Spaces tools
  have no Claude Code equivalent (`04` F-2). Replace with docs-index/file-read lookup, or drop
  the feature?
- **Q-6 (dead `ai.providers`).** `ai.providers` is `required` but goes inert for generation
  (`06` F-2). Keep inert, relax to optional, or remove — and what migration story for existing
  `spectra.config.json` files?

---

## Deliverables in this folder

`00-migration-surface.md` (this file) · `01-generation-seam.md` · `02-critic.md` ·
`03-deterministic-core.md` · `04-execution.md` · `05-orchestration.md` · `06-config.md`.
