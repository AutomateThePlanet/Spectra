# INVESTIGATION: criteria-extraction inversion (re-home off Copilot SDK → Claude Code)

**Date:** 2026-06-09 · **Branch:** `claude-code-v2` · **Scope:** read-only reality map to ground a
future spec that removes the GitHub Copilot SDK / in-process model from criteria extraction — the last
live consumer of `ai.providers`. **No spec proposed here.**

> Evidence convention: **CONFIRMED** = derived from reading the code this session (files opened
> firsthand are marked ⟐; the rest are agent-traced from the same tree — line numbers may drift ±a
> few lines but the structural facts hold). **INFERRED** = reasoned, not directly read.

---

## TL;DR (the decisive shape)

- The criteria-extraction **inversion seam already exists** for the `.criteria.yaml` path (Spec 054)
  and is **unwired**: `spectra ai compile-extraction-prompt` (model-free prompt emit) +
  `spectra ai ingest-criteria` (model-free fail-loud persist) are registered (`AiCommand.cs:26-27` ⟐)
  but no skill calls them — `spectra-criteria` still drives the in-process `ai analyze --extract-criteria`
  (`spectra-criteria.md:17` ⟐).
- **The live model coupling is two parallel paths, only one of which has a seam:**
  1. `spectra ai analyze --extract-criteria` → `CriteriaExtractor` → `AcceptanceCriterion` →
     `docs/criteria/*.criteria.yaml` + `_criteria_index.yaml`. **Seam BUILT** (just rewire skill +
     rescue helpers + delete the model call).
  2. `spectra docs index` → **`RequirementsExtractor`** → `RequirementDefinition` →
     `docs/requirements/_requirements.yaml`. **NO seam** — and extraction runs **inline/blocking**
     inside the short-lived CLI, which after inversion cannot make a model call.
- Removing `ai.providers` is provably safe **iff** the 3 live read sites (below) are inverted/removed;
  `ai.critic` is already dead. The whole `Agent/Copilot/` folder (CopilotService, ProviderMapping,
  CriteriaExtractor, RequirementsExtractor) can then go — **after** rescuing the model-free helpers
  that model-free code already depends on.

---

## Q1 — Extraction call graph & per-document loop

**Two entry points → one model boundary.** CONFIRMED:
- `DocsIndexHandler.cs:253` reads `config.Ai.Providers.FirstOrDefault(p=>p.Enabled)` → builds
  `RequirementsExtractor` (~:285) → loops.
- `AnalyzeHandler.cs:436` (extract) and `:956` (import-split) read the same provider → build
  `CriteriaExtractor` (~:463 / :959).
- **Model-call boundary (single method):** `CopilotService.CreateGenerationSessionAsync(provider)` →
  `session.SendAndWaitAsync(prompt, timeout, ct)` — called by `CriteriaExtractor.cs:67-88` and
  `RequirementsExtractor.cs:63-77`. Provider→SDK mapping in `ProviderMapping.MapProvider` (read by
  `CopilotService` ~:69).

**The per-document loop is HANDLER-owned, not extractor-owned.** CONFIRMED — decisive for inversion,
because the loop + incremental-skip + deadline are reusable model-free orchestration:
- docs index: `DocsIndexHandler.ExtractCriteriaLoopAsync` (~:386) — `foreach (doc in documents)` (~:399),
  one extractor call per doc, per-doc deadline via `Task.WhenAny` (~:405-413).
- analyze: `AnalyzeHandler.RunExtractCriteriaAsync` — `foreach (doc in documentMap.Documents)` (~:484),
  `ExtractWithDeadlineAsync` wraps each call (~:66-79).
- Each document gets an **independent session** (new `CreateGenerationSessionAsync` per doc). The
  extractor classes hold **no internal loop** — they are one-document-at-a-time. INFERRED corollary:
  the inverted skill loop maps 1:1 onto the existing handler loop (compile→model→ingest per doc).

---

## Q2 — Invariant entanglement (the decisive question)

| # | Invariant | Where it lives | Flag |
|---|---|---|---|
| a | per-doc SHA-256 incremental skip | `FileHasher.ComputeFileHashAsync` (`FileHasher.cs:14-27`); compared by handler (`AnalyzeHandler.cs:~510`) + `CriteriaIngestor.TryComputeDocHashAsync` (`:168-177` ⟐); hash stored in `CriteriaSource.DocHash` | **MODEL-FREE** |
| b | `_criteria_index.yaml` merge (ID/source match, --merge/--replace) | `CriteriaMerger.Merge/Replace` (`Spectra.Core/Parsing/CriteriaMerger.cs:24-85`) — pure string match | **MODEL-FREE** |
| c | RFC 2119 normalization | **prompt** instruction already relocated to model-free `ExtractionPromptCompiler.cs:97-100` ⟐; **post-processing** normalizers (`NormalizePriority`/`NormalizeTechniqueHint`) sit at `CriteriaExtractor.cs:240-262` — model-free code **inside** the Copilot class | **RESCUE** (helpers only; prompt already free) |
| d | compound-criteria splitting | `CriteriaExtractor.SplitAndNormalizeAsync` (`:109-151`) = a model call (`BuildSplitPrompt`), used **only** by import (`AnalyzeHandler.cs:959`, default on); result parse is `ClassifyResponse` (model-free) | **ENTANGLED** (import-only) |
| e | per-document no-truncation guarantee | handler deadline `Task.WhenAny` + full doc content passed un-chunked (`CriteriaExtractor.cs:55-104`; deadlines `DocsIndexHandler.cs:369`, `AnalyzeHandler.cs:52`) | **MODEL-FREE** |
| f | CSV/YAML/JSON import | `CsvCriteriaImporter`, `JsonCriteriaImporter`, `CriteriaFileReader/Writer`, `CriteriaMerger` (all `Spectra.Core/Parsing`, no SDK) | **MODEL-FREE** |

**The rescue insight (CONFIRMED firsthand ⟐):** `CriteriaExtractor` is the Copilot-coupled class, yet
it exposes **model-free statics that model-free code already imports**:
- `CriteriaIngestor.Classify` calls `CriteriaExtractor.ClassifyResponse` (`CriteriaIngestor.cs:38`, with
  `using Spectra.CLI.Agent.Copilot;` at line 1).
- `IngestCriteriaCommand.cs:3` also `using Spectra.CLI.Agent.Copilot;` (for `ExtractionOutcome`).
- `CriteriaExtractor.BuildExtractionPrompt` already **delegates** to `ExtractionPromptCompiler`
  (documented at `ExtractionPromptCompiler.cs:7-17` ⟐).

So the Copilot folder cannot simply be deleted: `ClassifyResponse`, the normalizers, the
`ExtractionOutcome` enum, and `CriteriaExtractionResult` must be **relocated to a model-free home**
first (these live under `src/Spectra.CLI/Agent/Copilot/`, e.g. `CriteriaExtractionResult.cs`).

---

## Q3 — docs-index auto-trigger + the two-path asymmetry

CONFIRMED:
- Extraction is **inline & blocking** inside `spectra docs index`: `DocsIndexHandler.cs:194-200`
  synchronously awaits `TryExtractCriteriaAsync`; the command does not return until it finishes.
- `--skip-criteria` gates **only** the extraction block (`:23`, `:194`); index build/migration still run.
- 2-min **per-document** deadline (Spec 047): `DocsIndexHandler.cs:369`, applied `:296`, enforced `:405`.
- **No auto-refresh-before-generate.** Generation (Spec 059) does not auto-extract; `UpdateHandler`
  reads existing criteria for grounding but never triggers extraction. The only triggers are explicit:
  `docs index` (inline) and `analyze --extract-criteria`.

**The asymmetry (decisive scope flag).** docs-index does **not** use the criteria seam's data model —
it runs `RequirementsExtractor` producing `RequirementDefinition` written to
`docs/requirements/_requirements.yaml` (empirically: the demo's `docs index --force` reported
`criteria_extracted: 602, criteria_file: docs\requirements\_requirements.yaml`). The Spec-054 seam
(`compile-extraction-prompt`/`ingest-criteria`) targets **only** the `AcceptanceCriterion`/`.criteria.yaml`
path. Therefore the spec must choose, for the docs-index path, one of:
1. **Build a requirements seam** (compile-requirements-prompt → ingest-requirements), or
2. **Converge** docs-index onto the criteria seam (`AcceptanceCriterion`), or
3. **Drop** inline requirements extraction from docs-index (default `--skip-criteria`; extraction
   becomes a separate skill-driven step).
Because a short-lived non-interactive `docs index` process **cannot make a model call** post-inversion,
option (3) or skill-orchestration is unavoidable for the *trigger*; (1)/(2) decide the *artifact*.

---

## Q4 — The ingest/serialization contract (the boundary the inverted seam must honor)

Already implemented by `CriteriaIngestor.IngestAsync` (`CriteriaIngestor.cs:62-165` ⟐). Contract:

**Per-document file** — `docs/criteria/{component}.criteria.yaml`, writer `CriteriaFileWriter`
(atomic temp+rename; `OmitNull|OmitDefaults`; underscore naming). Shape:
```yaml
# Extracted from: <doc>   # Doc hash: <sha256>   # Generated at: <utc>
criteria:
  - id: AC-<COMPONENT>-001        # AcceptanceCriterion (Spectra.Core/Models/Coverage/AcceptanceCriterion.cs)
    text: "<RFC2119 rewrite>"
    rfc2119: MUST|SHOULD|MAY|...
    source: <external id?>         # null for doc-extracted
    source_type: document|import
    source_doc: <path>
    source_section: <heading?>
    component: <slug>
    priority: high|medium|low
    tags: [..]
    linked_test_ids: [TC-001,..]
    technique_hint: BVA|EP|DT|ST|... (optional)
```

**Master index** — `docs/criteria/_criteria_index.yaml`, writer `CriteriaIndexWriter` (recomputes
`total_criteria` on every write; atomic). Shape: `CriteriaIndex { version, total_criteria, sources:[CriteriaSource] }`:
```yaml
version: 1
total_criteria: <recomputed>
sources:
  - file: <component>.criteria.yaml
    source_doc: <path or import file>
    source_type: document|import
    doc_hash: <sha256?>           # incremental-skip key
    criteria_count: <n>
    last_extracted: <utc?>        # imported_at for imports
    outcome: extracted            # Spec 048 — ONLY "extracted" is ever persisted
```

**Boundary rules (CONFIRMED ⟐):**
- Only `ExtractionOutcome.Extracted` persists; `EmptyResponse`/`ParseFailure` write **nothing** and
  leave the index byte-unchanged (FR-006 anti-cache-poisoning) — `CriteriaIngestor.cs:80-83`.
- IDs: `AC-{COMPONENT}-{NNN}`, reused by exact text match against the existing per-doc file
  (`:101-128`); never re-numbered on re-extraction.
- Existing seam CLI (CONFIRMED ⟐):
  - `spectra ai compile-extraction-prompt --doc <path> [--component <c>] [--output-format json]` →
    prompt to **stdout**, writes nothing; FR-003 empty-source short-circuit (`outcome Extracted, []`);
    exit 0 ok / 4 refused / 1 error (`CompileExtractionPromptCommand.cs`).
  - `spectra ai ingest-criteria --doc <path> [--component <c>] [--from <file>|stdin] [--dry-run]
    [--output-format json]` → fail-loud persist; exit 0 ok / 1 error / 5 empty / 6 parse
    (`IngestCriteriaCommand.cs`).

---

## Q5 — Complete `config.Ai.*` read sites (proving schema-removal safety)

| File:Line | Read | Class |
|---|---|---|
| `DocsIndexHandler.cs:253` | `.Ai.Providers.FirstOrDefault(Enabled)` | **criteria-LIVE** (RequirementsExtractor) |
| `AnalyzeHandler.cs:436` | same | **criteria-LIVE** (CriteriaExtractor, extract) |
| `AnalyzeHandler.cs:956` | same | **criteria-LIVE** (CriteriaExtractor, import-split) |
| `ConfigLoader.cs:233,240` | `.Ai.Providers.Count` / `foreach provider` | validation-only |
| `ConfigHandler.cs:203,206` | `foreach provider` | display-only (`spectra config show`) |

`ai.critic`: **never read at runtime** — `CriticFactory.TryCreate` always returns *"In-process critic
retired (Spec 058); verification runs as the spectra-critic subagent"* (`CriticFactory.cs:108-117`);
`CriticModelResolver.Resolve` exists but has **zero callers**.

Already-removed (Spec 059, CONFIRMED absent — only referenced in comments): `BehaviorAnalyzer`,
`CopilotGenerationAgent`, `UserDescribedGenerator`, `ProviderChain`, `AgentFactory.CreateAgentAsync`.

→ The **only** thing keeping `ai.providers` alive is the 3 criteria-LIVE sites. Invert/remove those and
`ai.providers` + `ai.critic` can leave the schema; validation (`ConfigLoader:233/240`) and display
(`ConfigHandler:206`) must be updated to not require/print them.

---

## Q6 — Skill/agent surface & the inversion template

- **`spectra-criteria.md`** (⟐) drives the OLD path: extract = `spectra ai analyze --extract-criteria
  --no-interaction --output-format json` (`:17`); import = `--import-criteria` (`:33`, notes
  `--skip-splitting` "disable AI splitting"); list = `--list-criteria`. All single blocking CLI calls.
- **`spectra-docs.md`** drives `docs index` (inline criteria) + list/show.
- **The proven inversion template** (generation, Spec 053/059) the criteria path must mirror:
  `compile-*-prompt` (deterministic CLI, prompt→stdout) → **in-session model turn** (agent reads the
  doc with file tools, emits JSON) → `ingest-*` (fail-loud validate+persist, bounded retry) →
  present/approve. For criteria the two seam commands **already exist** (`AiCommand.cs:26-27` ⟐).
- **Where the per-doc loop goes (INFERRED, by analogy):** the **skill** orchestrates the loop (like
  `spectra-generate` drives compile→generate→ingest), iterating changed docs: per doc →
  `compile-extraction-prompt` → agent extracts in-session → `ingest-criteria`. The deterministic
  SHA-256 incremental-skip (today inside the handler loop) must be **exposed to the skill** (e.g. a
  model-free "list changed docs / changed since index" command) so the skill skips unchanged docs
  without a model turn. No extraction subagent is required (unlike the critic's `context: fork`).

---

## (a) RESCUE LIST — must land BEFORE the model call is cut
1. **`CriteriaExtractor.ClassifyResponse` + normalizers** (`NormalizePriority`, `NormalizeTechniqueHint`,
   `:190-262`) — model-free but inside the Copilot class; `CriteriaIngestor`/`IngestCriteriaCommand`
   depend on them. Relocate to a model-free home and repoint the two `using Spectra.CLI.Agent.Copilot;`.
2. **`ExtractionOutcome` enum + `CriteriaExtractionResult`/`RequirementsExtractionResult` records**
   (under `Agent/Copilot/`) — relocate (CriteriaIngestor references `ExtractionOutcome`).
3. **docs-index / `RequirementsExtractor` path has NO seam** — build a requirements compile/ingest
   seam, OR converge onto the criteria seam, OR drop inline requirements extraction (scope decision).
4. **docs-index inline model call** — a short-lived CLI cannot extract post-inversion; move the trigger
   to skill orchestration or default `--skip-criteria`.
5. **import compound-splitting** (`SplitAndNormalizeAsync`, the lone import-time model call) — either
   make `--skip-splitting` the only behavior, or re-home splitting onto a compile/ingest sub-seam.
6. **Config surface** — `ConfigLoader.GenerateConfig` + embedded `Templates/spectra.config.json` +
   `InitHandler` interactive AI steps (`InteractiveAuthSetupAsync` :160, `InteractiveModelPresetAsync`,
   `InteractiveCriticSetupAsync`) + validation/display reads must stop requiring/prompting providers/critic.

## (b) INGEST CONTRACT — see Q4 (per-doc `.criteria.yaml`, `_criteria_index.yaml`, outcome gate, ID
scheme, and the existing `compile-extraction-prompt` / `ingest-criteria` CLI signatures).

## (c) COMPLETE `config.Ai.*` READ SITES — see Q5 table (3 criteria-LIVE; 2 validation-only;
1 display-only; `ai.critic` dead; 5 generation classes already gone).

---

## CONFIRMED vs INFERRED ledger
- **CONFIRMED (firsthand ⟐):** seam exists & is unwired (`AiCommand.cs`, `spectra-criteria.md`,
  `CompileExtractionPromptCommand.cs`, `IngestCriteriaCommand.cs`, `ExtractionPromptCompiler.cs`,
  `CriteriaIngestor.cs`); ingest contract & outcome gate; rescue coupling via `using …Agent.Copilot`.
- **CONFIRMED (agent-traced, same tree):** handler-owned loops & deadlines; model-call boundary;
  invariant locations; the 3 live + validation/display read sites; RequirementsExtractor→`_requirements.yaml`.
- **INFERRED:** exact skill choreography of the per-doc loop; that a "changed-docs" command is the
  cleanest way to preserve incremental-skip without a model turn; the docs-index artifact decision.

## Verification (how the eventual spec/PR proves itself — for reference, not done here)
- Grep shows **zero** `config.Ai.Providers` reads outside validation/display after inversion.
- `Agent/Copilot/` deleted; `dotnet build -c Release` green; `CriteriaIngestor`/seam tests still pass.
- `spectra docs index` no longer makes a model call; `spectra-criteria` skill drives
  compile-extraction-prompt → ingest-criteria per doc; `_criteria_index.yaml`/`.criteria.yaml` output
  byte-compatible with today's writer.
