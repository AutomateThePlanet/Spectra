# 02 — The critic (Area B)

> **Purpose.** Establish, with file+line evidence, how the critic is invoked today, what
> model drives it, what context it sees, and its output contract — to ground the v2 move to
> "critic = a `context: fork` subagent, invoked as a mandatory explicit step inside the
> generation skill" (ARCHITECTURE-v2 §81–84).
>
> Investigation only. Confirmed claims cite `file:line`; hypotheses under **INFERRED**; risks
> under **Findings** (recorded, not fixed).

---

## 1. Where the critic is invoked

**Inline in the generation batch loop**, not as a separate pass.

- `GenerateHandler.ExecuteDirectModeAsync` calls `VerifyTestsAsync` per batch:
  `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs:817` (interactive path twin at
  `:1458`).
- `VerifyTestsAsync` is defined at `GenerateHandler.cs:2064`. It creates the critic via
  `CriticFactory.TryCreate(criticConfig, _tokenTracker, _errorTracker)`
  (`GenerateHandler.cs:2075`), converts the document map to `SourceDocument`s
  (`:2109–2116`), then verifies each test.
- Per-test verification: `critic.VerifyTestAsync(test, sourceDocs, ct)`
  (`GenerateHandler.cs:2159`), inside a `SemaphoreSlim`-bounded parallel loop
  (`:2124–2161`) whose width is `criticConfig.GetEffectiveMaxConcurrent()` (`:2124`;
  default 1 = sequential).
- The critic runtime is `CopilotCritic : ICriticRuntime`
  (`src/Spectra.CLI/Agent/Copilot/GroundingAgent.cs:16`); `VerifyTestAsync` at
  `GroundingAgent.cs:55`.

The **model call** for the critic is `session.SendAsync` at `GroundingAgent.cs:124` —
event-driven, not `SendAndWaitAsync`. The response is assembled from
`AssistantMessageDeltaEvent`/`AssistantMessageEvent` and completed on `SessionIdleEvent`
(`GroundingAgent.cs:92–121`). The critic session is created by
`CopilotService.CreateCriticSessionAsync` (`GroundingAgent.cs:86` →
`CopilotService.cs:85`), with `Streaming = false` (`CopilotService.cs:96`).

### 1.1 Factory note (two factories; one is dead)

- **Live:** `Spectra.CLI.Agent.Critic.CriticFactory.TryCreate`
  (`src/Spectra.CLI/Agent/Critic/CriticFactory.cs:103`) — used by the handler at
  `GenerateHandler.cs:2075`; constructs `CopilotCritic` (`CriticFactory.cs:123`). Handles
  Spec 039 provider resolution (`ResolveProvider`, `CriticFactory.cs:180`): canonical set
  `github-models, azure-openai, azure-anthropic, openai, anthropic` (`:79–80`), `github` →
  `github-models` alias with warning (`:86–90, 194–206`), `google` hard-rejected (`:97–98`).
- **Dead:** `CopilotCriticFactory` (`GroundingAgent.cs:226`) — a second factory with the same
  job. A repo-wide search finds it only in its own defining file (no other references), so it
  is unused. (See Findings F-1.)

---

## 2. What model is wired, and how

Config-driven, with a provider→default fallthrough.

- Resolution: `CopilotCritic.GetEffectiveModel(criticConfig)` (`GroundingAgent.cs:192–207`):
  `config.Model` wins if set (`:194–195`); otherwise a provider switch
  (`anthropic`/`azure-anthropic` → `claude-haiku-4-5`; `azure-deepseek` → `DeepSeek-V3-0324`;
  `openai`/`azure-openai` → `gpt-5-mini`; default `gpt-5-mini`) (`:200–206`). Assigned to
  `ModelName` in the ctor (`GroundingAgent.cs:39`).
- Config keys (`ai.critic.*`): `enabled`, `provider`, `model`, `api_key_env`, `base_url`,
  `timeout_seconds`, `max_concurrent` (detailed in `06-config.md`). The per-call timeout is
  `criticConfig.TimeoutSeconds` (`GroundingAgent.cs:68`).
- ARCHITECTURE-v2 §29 states the model **is config-driven, not hard-coded** — confirmed: the
  default is only a fallback; `ai.critic.model` overrides it. The v2 target (Sonnet 4.6) would
  be set via that key, no code change to the resolution logic required.

> **Note (Spec 041 intent vs v2):** the comments at `GroundingAgent.cs:197–198` and
> `CopilotService.cs:324–327` describe the defaults as favouring **cross-architecture**
> verification (GPT critic for a Claude generator). ARCHITECTURE-v2 §29/§32 revises this:
> same-family Sonnet read as more useful in team testing; "different families" was a means,
> not the end. The config-driven model keeps that decision open. This is a deliberate
> direction change, recorded — not a code defect.

---

## 3. What context the critic receives

Built by `CriticPromptBuilder` (`src/Spectra.CLI/Agent/Critic/CriticPromptBuilder.cs`),
assembled in `CopilotCritic` at `GroundingAgent.cs:77–79`
(`{system}\n\n---\n\n{user}`).

- **System prompt** (`CriticPromptBuilder.BuildSystemPrompt`, `:26`): a generic "test case
  verification expert" instruction with the output JSON schema and verdict rules
  (`:40–70`). **No reference to the generator's model, prompt, or reasoning.**
- **User prompt** (`BuildUserPrompt`, `:76`): the test artifact only — id, title,
  preconditions, steps, expected result, test data (`:80–109`) — plus source documents
  selected by `SelectRelevantDocuments` (`:146`): docs whose path matches the test's
  `source_refs` first, capped at `MaxDocuments = 5` (`:14, 169`), each truncated to
  `MaxDocumentChars = 8000` (`:13, 130, 183`).

**Confirmed:** the critic sees *only the artifact + selected source docs*, never the
generator's prompt, reasoning, tool calls, or token usage. This already matches v2's
`context: fork` isolation requirement (ARCHITECTURE-v2 §84) — the isolation exists today by
construction (separate SDK session + a prompt that simply never includes generator state),
so v2 formalizes an existing property rather than introducing one.

---

## 4. Output contract and how it feeds the test

- **Wire format** (instructed in the system prompt, `CriticPromptBuilder.cs:45–58`):
  `{ verdict: "grounded"|"partial"|"hallucinated", score: 0.0–1.0, findings: [{ element,
  claim, status, evidence, reason }] }`.
- **Parsing:** `CriticResponseParser.Parse`
  (`src/Spectra.CLI/Agent/Critic/CriticResponseParser.cs:25`) → `ExtractJson` (`:50`,
  strips ```` ```json ```` fences, slices first `{` … last `}`) → `ParseJson` (`:73`):
  `ParseVerdict` (`:92`, unknown → `Partial`), `ParseScore` (`:107`, clamp 0–1, missing →
  0.5), `ParseFindings` (`:121`). Produces a `VerificationResult` with
  `VerificationVerdict { Grounded | Partial | Hallucinated | … }` and `CriticModel`.
- **Write-back:** in `CreateTestWithGrounding` (`GenerateHandler.cs:2229`), a successful
  result becomes `GroundingMetadata` via `result.ToMetadata(generatorModel)`
  (`GenerateHandler.cs:2266`, only when `result.IsSuccess`), attached as `test.Grounding`
  (`:2288`). A pre-existing `Manual` verdict is preserved and never overwritten
  (`:2237–2260`, and the verify loop skips Manual tests at `:2134`).
- **Gate:** tests with `Verdict == Hallucinated` are filtered out of the persisted set
  (`GenerateHandler.cs:848` keeps `!= Hallucinated`; `:853` selects the rejected set);
  `Grounded`/`Partial`/`Unverified`/`Manual` pass through.
- **Failure → non-rejecting:** a critic exception/timeout does **not** reject the test. The
  agent-level catch returns `Verdict = Partial, Score = 0` (`GroundingAgent.cs:209–220`); the
  handler-level catch marks the test `Unverified` (`GenerateHandler.cs:2163–2173+`); a
  missing/disabled critic yields `VerificationResult.Unverified` for all tests
  (`GenerateHandler.cs:2085, 2100`). So the critic is **advisory-gating**: it can drop a
  clearly-hallucinated test, but its own failure never blocks generation.

---

## 5. INFERRED

- **INFERRED:** `result.IsSuccess` (used at `GenerateHandler.cs:2264`) is false for the
  error-result shape produced by `CreateErrorResult` (`GroundingAgent.cs:209`,
  `CriticResponseParser.cs:175`), so a failed verification writes **no** `Grounding` block
  rather than a misleading one. *Confirming evidence:* read `VerificationResult.IsSuccess` /
  `ToMetadata` in `Spectra.Core/Models/Grounding/` (not opened in this pass).
- **INFERRED:** `max_concurrent > 1` makes multiple critic model calls run concurrently
  against the configured provider; under the v2 single-subscription model this would mean
  several simultaneous subscription turns. *Confirming evidence:* the semaphore at
  `GenerateHandler.cs:2127` and the comment at `:2118–2123`.

---

## 6. Findings (recorded, not fixed)

- **F-1 — Dead second critic factory.** `CopilotCriticFactory` (`GroundingAgent.cs:226`) is
  unreferenced outside its own file; the live factory is `CriticFactory` (`CriticFactory.cs:68`).
- **F-2 — Duplicate critic-model default logic.** The provider→default-model switch is
  duplicated in `CopilotCritic.GetEffectiveModel` (`GroundingAgent.cs:192`) and
  `CopilotService.GetCriticModel` (`CopilotService.cs:319`), and the latter is reached via
  `CreateCriticSessionAsync` (`CopilotService.cs:94`). Two sources of truth for the same
  default; the values currently agree but could drift.
- **F-3 — Verdict on parse-miss is silently `Partial`.** Missing `verdict`/`score` default
  to `Partial`/0.5 (`CriticResponseParser.cs:95, 110`); a malformed critic response is thus
  treated as a soft pass rather than surfaced. Relevant to v2's "fail loud" boundary
  principle (ARCHITECTURE-v2 §72) once the critic is a mandatory skill step.

---

## 7. Conclusion — what changes for v2

The critic's **isolation and config-driven model already match** the v2 target: it runs in a
separate session, sees only artifact + docs (`CriticPromptBuilder.cs:76–141`), and takes its
model from `ai.critic.model` (`GroundingAgent.cs:194`). Three things move:

1. **Invocation moves from in-process `ICriticRuntime` to a skill-mandated subagent turn.**
   Today "always runs" is enforced by the handler calling `VerifyTestsAsync` in the batch loop
   (`GenerateHandler.cs:817`); in v2 it becomes a mandatory explicit step *inside the
   generation skill's procedure* (ARCHITECTURE-v2 §84), realized as a `context: fork`
   subagent — never auto-invocation.
2. **The model call (`GroundingAgent.cs:124`) is removed from C#;** the parsing/verdict
   contract (`CriticResponseParser`) and the write-back (`CreateTestWithGrounding:2266`)
   stay as the deterministic boundary that ingests the subagent's JSON verdict.
3. **`ai.critic.model` selects the subagent model** (target: Sonnet 4.6); the
   cross-architecture default comments (`GroundingAgent.cs:197`) are superseded by the §32
   same-family direction — a config value, not a code change.
