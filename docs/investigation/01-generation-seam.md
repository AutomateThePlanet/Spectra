# 01 — The model-invocation seam (Area A)

> **Purpose.** Pinpoint, with file+line evidence, exactly where the CLI calls the model
> during generation and criteria extraction today, what crosses that boundary in and out,
> and where the retry loop lives — so the v2 "generation handoff inversion" (ARCHITECTURE-v2
> §63–76) can be cut at the right seam.
>
> Scope: investigation only. No code was changed. Confirmed claims cite `file:line`;
> hypotheses are under **INFERRED**; risks are under **Findings** (recorded, not fixed).

---

## 1. Generation: the model call site

**The single model call for generation:**
`src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs:239–242`

```csharp
var response = await session.SendAndWaitAsync(
    new MessageOptions { Prompt = fullPrompt },
    timeout: batchTimeout,
    cancellationToken: ct);
```

`session` is a `GitHub.Copilot.SDK.CopilotSession` (the SDK is imported at
`GenerationAgent.cs:5`), created at `GenerationAgent.cs:117–120` via
`service.CreateGenerationSessionAsync(_provider, tools, ct)`. The service is the
`CopilotService` singleton obtained at `GenerationAgent.cs:89`
(`CopilotService.GetInstanceAsync`).

### 1.1 Call chain (command → model)

| Step | Symbol | Location |
|------|--------|----------|
| 1 | `GenerateCommand` routes to handler | `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs` (handler wiring) |
| 2 | `GenerateHandler.ExecuteDirectModeAsync` (batch loop) | `GenerateHandler.cs:233` (entered from `:225`) |
| 3 | batch loop calls the agent | `GenerateHandler.cs:762` `await agent.GenerateTestsAsync(...)` |
| 4 | `CopilotGenerationAgent.GenerateTestsAsync` | `GenerationAgent.cs:78` |
| 5 | get SDK service singleton | `GenerationAgent.cs:89` `CopilotService.GetInstanceAsync` |
| 6 | create SDK session (model+provider+tools) | `GenerationAgent.cs:117` → `CopilotService.CreateGenerationSessionAsync` at `CopilotService.cs:61` |
| 7 | **model call** | `GenerationAgent.cs:239` `session.SendAndWaitAsync` |

The agent is constructed through `AgentFactory.CreateAgentAsync` (returns `IAgentRuntime`;
`CopilotGenerationAgent` is the implementation — class decl `GenerationAgent.cs:22`). The
agent is **tool-using**: document tools + test-index tools + an in-process Testimize
field-spec tool are attached to the session (`GenerationAgent.cs:97–113`), so the model can
call back into the CLI mid-turn (read docs, check duplicates, allocate IDs) before emitting
its final JSON. This is a Copilot SDK agent loop, not a single completion.

### 1.2 What is passed IN

The prompt is compiled by `BuildFullPrompt` (`GenerationAgent.cs:448`), invoked at
`GenerationAgent.cs:222–228`. Inputs assembled into the prompt:

- **User prompt** (focus / behaviours) — `prompt` param, mapped to template var `behaviors`
  (`GenerationAgent.cs:476`).
- **System template** `test-generation.md` loaded via `PromptTemplateLoader.LoadTemplate`
  (`GenerationAgent.cs:468`); resolved with a values dictionary at
  `GenerationAgent.cs:469–485`. **Fallback** inline prompt when no template loader
  (`GenerationAgent.cs:488–534`).
- **`criteriaContext`** — passed as template var `acceptance_criteria`
  (`GenerationAgent.cs:479`); produced by `LoadCriteriaContextAsync`
  (`GenerateHandler.cs:2484`, called at `:673`), which returns the typed
  `CriteriaContextResult` record (`GenerateHandler.cs:2474`).
- **Testimize dataset** — pre-computed YAML block via `FormatTestimizeDataset`
  (`GenerationAgent.cs:384`), embedded as template var `testimize_dataset`
  (`GenerationAgent.cs:474`).
- **Profile JSON format** — `ProfileFormatLoader.LoadFormat` (`GenerationAgent.cs:215`),
  template var `profile_format` (`GenerationAgent.cs:480`). Note: this is sent as an
  **example schema**, not a programmatic constraint (see Findings F-1).
- **Requested count** — template var `count` (`GenerationAgent.cs:481`).

Session/model knobs are set in `CopilotService.CreateGenerationSessionAsync`
(`CopilotService.cs:61–80`): `Model = ProviderMapping.GetModelName(providerConfig)`
(`:68`), `Provider = ProviderMapping.MapProvider(providerConfig)` (`:69`),
`Streaming = true` (`:70`), `OnPermissionRequest = PermissionHandler.ApproveAll` (`:71`).
**No temperature and no `response_format`/structured-output is set** (see Findings F-1).
Per-batch timeout derives from `config.Ai.GenerationTimeoutMinutes`
(`GenerationAgent.cs:233–234`).

### 1.3 What comes BACK and how it's parsed

Response text is read from the SDK message at `GenerationAgent.cs:251`
(`response?.Data?.Content ?? ""`). Parsing pipeline:

- `ParseTestsFromResponse` (`GenerationAgent.cs:537`)
  - `ExtractJson` (`GenerationAgent.cs:571`) — pulls the array out of a ```` ```json ````
    fence or from the first `[`.
  - `TryParseJsonArray` (`GenerationAgent.cs:598`) — strict parse.
  - `TryRepairTruncatedArray` (`GenerationAgent.cs:619`) — on failure, salvages the last
    complete `{}` and closes the array (handles token-limit truncation).
  - `ParseTestCase` (`GenerationAgent.cs:678`) — deserializes each element into a `TestCase`
    (id, title, priority, tags, steps, source_refs, criteria, etc.).

**YAML frontmatter is NOT parsed from the model response.** The model returns a JSON array;
frontmatter is written later, at persist time, by `CreateTestWithGrounding`
(`GenerateHandler.cs:2229`). So the model→CLI contract is "JSON array of test objects," and
the disk format is produced deterministically afterward.

### 1.4 The retry loop (generation)

**There is no in-CLI retry for generation.** On a zero-test parse the agent returns a
`GenerationResult` carrying error strings (`GenerationAgent.cs:260–272`); the batch loop in
`ExecuteDirectModeAsync` moves on rather than re-prompting the same batch. Exceptions
(timeout, Copilot CLI, 429, session error) are caught and converted to error results
(`GenerationAgent.cs:280–356`), again without an automatic re-attempt of the model call.

This matters for the inversion: today the only "redo on bad output" mechanism for generation
is whatever the SDK agent loop does internally before `SendAndWaitAsync` returns. Once the
CLI stops calling the model, **that redo must be expressed as SKILL/agent choreography**
(ARCHITECTURE-v2 §69–72): invalid CLI-side validation → skill instructs the agent to redo
with the specific error.

---

## 2. Criteria extraction: the model path (Spec 046/047)

There are **two** extractor implementations that each call the model. ARCHITECTURE-v2 and
CLAUDE.md (Spec 047 note) record they are intentionally not merged.

### 2.1 `CriteriaExtractor` (typed-result path, used by `ai analyze --extract-criteria`)

- **Model call:** `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs:85–88`
  (`session.SendAndWaitAsync`, 2-min timeout). Session via
  `CreateGenerationSessionAsync` (`CriteriaExtractor.cs:68`).
- **Typed result:** `CriteriaExtractionResult` record
  (`src/Spectra.CLI/Agent/Copilot/CriteriaExtractionResult.cs:37–42`) with
  `ExtractionOutcome { Extracted | EmptyResponse | ParseFailure }`
  (`CriteriaExtractionResult.cs:12–30`) and `IsCacheable => Outcome == Extracted`
  (`CriteriaExtractionResult.cs:41`).
- **Validation (pure function):** `ClassifyResponse` (`CriteriaExtractor.cs:225`) — empty →
  `EmptyResponse` (`:231–232`); no `[`/`]` delimiters or null deserialize or exception →
  `ParseFailure` (`:238–239`, `:248–249`, `:268–272`); otherwise `Extracted` (`:266`).
- **Empty source short-circuit:** whitespace content returns `Extracted, []` without a model
  call (`CriteriaExtractor.cs:64–65`) — a genuine "nothing to extract," cacheable.

### 2.2 `RequirementsExtractor` (legacy path, used by `docs index`)

- **Model call:** `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs:72–75`
  (`session.SendAndWaitAsync`, 2-min timeout), with a manual `Task.WhenAny` deadline guard
  (`:76–83`). Session via `CreateGenerationSessionAsync` (`:64–66`).
- **Failure semantics differ from `CriteriaExtractor`:** empty response **throws**
  `InvalidOperationException` (`RequirementsExtractor.cs:88–90`); the timeout branch
  **throws** `TimeoutException` (`:79–83`). It returns `IReadOnlyList<RequirementDefinition>`
  via `ParseResponse` (`:92`), with no typed outcome enum.
- The whole file is marked legacy (`#pragma warning disable CS0618` at `:1`).

### 2.3 The retry loop (extraction — this one exists)

Lives in the analyze handler, not the extractor:
`src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`.

- `ExtractWithRetryAsync` (`AnalyzeHandler.cs:102`) loops up to `maxAttempts`, returning
  early when `lastResult.IsCacheable` (`AnalyzeHandler.cs:115`), i.e. only a `ParseFailure`
  or `EmptyResponse` triggers a retry.
- Backoff `ExtractionRetryBackoff = 1500ms` (`AnalyzeHandler.cs:50`) via an injected
  `IExtractionDelayProvider`; per-attempt deadline `ExtractionPerAttemptDeadline = 2 min`
  (`AnalyzeHandler.cs:51`) applied by `ExtractWithDeadlineAsync` (`AnalyzeHandler.cs:65`).
- Wired at `AnalyzeHandler.cs:542–545`; the content-hash cache write is gated on
  `!result.IsCacheable` (`AnalyzeHandler.cs:575`) so a non-cacheable failure never poisons
  the cache (the Spec 047 fix).

This retry pattern is the closest existing analogue to what the v2 generation flow must move
into skill choreography — but it is **per-document extraction**, not generation, and it is
the only model-output retry currently in the CLI.

---

## 3. INFERRED

- **INFERRED:** `GenerateCommand` → `GenerateHandler` is the only entry into
  `ExecuteDirectModeAsync` for the direct/batch path; an interactive/gap-driven path
  (`ExecuteInteractiveModeAsync`, referenced by the second `GenerateTestsAsync` call at
  `GenerateHandler.cs:1403`) shares the same agent + critic + persist seam. *Confirming
  evidence:* both call sites use `agent.GenerateTestsAsync` and `VerifyTestsAsync` (lines
  762/817 and 1403/1458), but I did not read the full interactive handler body.
- **INFERRED:** the SDK agent loop performs its own internal tool-call iterations before
  `SendAndWaitAsync` returns, so "the model is called once from C#" is true at the C# call
  site even though multiple model turns may occur inside the SDK. *Confirming evidence:* the
  event subscription handles `ToolExecutionStart/Complete` events (`GenerationAgent.cs:146,
  191`), implying multi-step tool use within one `SendAndWaitAsync`.

---

## 4. Findings (recorded, not fixed)

- **F-1 — No structured-output enforcement on generation.** The session sets no
  `response_format`/schema (`CopilotService.cs:66–72`); the profile JSON is only an example
  in the prompt (`GenerationAgent.cs:480, 515–517`). Reliability rests entirely on prose
  instructions plus `TryRepairTruncatedArray` (`GenerationAgent.cs:619`). Relevant because
  ARCHITECTURE-v2 §69 explicitly says there is "no programmatic `response_format`" in v2 —
  i.e. the current code already has no such net, so boundary validation is the only thing
  that must harden.
- **F-2 — Divergent extractor failure semantics.** `CriteriaExtractor.ClassifyResponse`
  returns a typed outcome (`CriteriaExtractor.cs:225`) while `RequirementsExtractor` throws
  on empty/timeout (`RequirementsExtractor.cs:81, 89`). Two code paths for "extract criteria
  from a doc" with different contracts; only the former is cache-safe. (Merge is out of
  scope per Spec 047 / CLAUDE.md.)
- **F-3 — Duplicate critic-model default logic.** A provider→default-model switch exists in
  both `CopilotService.GetCriticModel` (`CopilotService.cs:319–336`) and (per Area B)
  `GroundingAgent.GetEffectiveModel`. Two sources of truth for the same default. Recorded
  here; detailed in `02-critic.md`.

---

## 5. Conclusion — the seam

> **The seam is `session.SendAndWaitAsync` in `GenerationAgent.cs:239` (and its twins at
> `CriteriaExtractor.cs:85` and `RequirementsExtractor.cs:72`).** Everything *before* it —
> `BuildFullPrompt` (`GenerationAgent.cs:448`) compiling doc + criteria + profile + Testimize
> into a prompt — is already a deterministic, unit-testable artifact that survives v2.
> Everything *after* it — `ParseTestsFromResponse` → `ParseTestCase` (`:537`, `:678`) →
> `CreateTestWithGrounding` → persist — is the deterministic boundary validation that v2
> keeps and must harden into the "fail loud, never silently repair" net.

What inverts: the call at `:239` is deleted from the CLI; prompt compilation
(`BuildFullPrompt`) becomes a CLI `generate-prompt`-style output, the interactive agent does
the generative turn, and the CLI re-enters at the parse/validate boundary. The retry that is
absent for generation today (and present only for extraction at `AnalyzeHandler.cs:102`) must
be re-expressed as skill choreography keyed on CLI validation errors.
