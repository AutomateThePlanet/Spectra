# Research: Criteria-extraction inversion — completion + Copilot SDK removal

All "unknowns" were resolved by `INVESTIGATION-criteria-inversion.md` and the planning-phase test
audit. Decisions below; rationale + alternatives for each.

## R1 — Rescue home for the model-free helpers (FR-001)

- **Decision**: Relocate `ClassifyResponse` + `NormalizePriority`/`NormalizeTechniqueHint` into a new
  `CriteriaResponseClassifier` (static) in the **existing** `Spectra.CLI.Extraction` namespace; move the
  `ExtractionOutcome` enum and `CriteriaExtractionResult`/`RequirementsExtractionResult` records there
  too. Repoint `CriteriaIngestor` and `IngestCriteriaCommand` (drop `using Spectra.CLI.Agent.Copilot;`).
- **Rationale**: `Spectra.CLI.Extraction` is already the model-free home of `ExtractionPromptCompiler` +
  `CriteriaIngestor`; reusing it avoids inventing `Spectra.CLI.Criteria` (YAGNI, Principle V).
- **Alternatives**: new `Spectra.CLI.Criteria` namespace (rejected: needless surface); move to
  `Spectra.Core` (rejected: these are CLI-only classification helpers, not Core models).

## R2 — Changed-docs command (FR-005)

- **Decision**: Add `spectra docs changed [--output-format json]` — a model-free command that compares
  each source doc's current SHA-256 (`FileHasher.ComputeFileHashAsync`) against the recorded
  `CriteriaSource.doc_hash` in `_criteria_index.yaml` and emits, per doc, `{path, component, status:
  new|changed|unchanged}`. The skill iterates only `new|changed`.
- **Rationale**: Reuses `FileHasher` + `CriteriaIndexReader` (both already model-free); exposes the
  incremental-skip key that today lives inside the handler loop, so the skill preserves it without a
  model turn. Mirrors how generation seam commands surface deterministic state to the skill.
- **Alternatives**: bake skip logic into `compile-extraction-prompt` (rejected: compile is per-doc and
  shouldn't read the whole index); a `--changed-only` flag on a batch command (rejected: the skill owns
  the loop, FR-003).

## R3 — Import compound-splitting disposition (FR-008)

- **Decision**: Remove the AI split entirely — delete `CriteriaExtractor.SplitAndNormalizeAsync` +
  `BuildSplitPrompt`; `import` becomes a deterministic pass-through (former `--skip-splitting` behavior).
  **Remove** the `--skip-splitting` flag (no shim, Principle V); if a CLI test references it, downgrade
  to an accepted hidden no-op and note it.
- **Rationale**: Splitting is the only import-time model call; dropping it severs the last import-side SDK
  use and matches the "no backwards-compatibility shims" principle.
- **Alternatives**: keep flag as no-op (rejected as default); re-home splitting onto a sub-seam (deferred,
  out of scope).

## R4 — docs-index convergence (FR-006/FR-007)

- **Decision**: Remove the inline extraction block from `DocsIndexHandler` (`:194-200`); `docs index`
  builds the doc index only. Delete the **CLI** `RequirementsExtractor` (Agent/Copilot). Stop producing
  `docs/requirements/_requirements.yaml`. `--skip-criteria` becomes an accepted no-op (kept only if a CLI
  test needs it; otherwise removed).
- **Rationale**: A short-lived non-interactive `docs index` cannot make a model call post-inversion;
  removing inline extraction is forced. Criteria are produced only via the skill-driven `.criteria.yaml`
  seam.
- **Core-side caveat**: `RequirementDefinition`, `RequirementsWriter`, `RequirementsParser` live in
  `Spectra.Core` and are pinned by `RequirementsWriterTests` (already `[Obsolete]`). They **stay**
  (unit-tested in isolation) but are unused by the product. "Retire" = remove runtime use + artifact, not
  the Core types. This satisfies FR-007 at the product level without touching Core tests.

## R5 — FR-010 vs FR-017 conflict (RESOLVED by user: FR-010 wins)

- **Conflict (confirmed)**: `Spectra.Core` tests pin provider/critic presence and FR-017 forbids editing
  Core tests — incompatible with FR-010/SC-007 (no `ai.providers`/`ai.critic` in the generated config):
  - `ConfigLoaderTests.Load_WithMissingProviders_ReturnsFailure` — empty providers ⇒ `MISSING_PROVIDERS`.
  - `ConfigLoaderTests.GenerateDefaultConfig_ReturnsValidJson` — Load-validates the generated config.
  - `ConfigLoaderTests.Load_With{Valid,MultipleProviders,SuiteOverrides,Defaults,Comments}` — assert on
    `Ai.Providers`.
  - `ProviderRetirementTests` — asserts `ai.providers` not deprecated, critic model honored.
  - `CriticConfigTests` / `CriticConfigClampTests` — pin `CriticConfig`.
- **Decision (user, authoritative)**: **FR-010 wins** with **strict discipline** — the regression net
  protects *behavior*, not the retired config contract. The conflicting tests assert the OLD contract
  ("a valid config requires a provider"), which this spec deliberately retires; such a test is not
  catching a regression, it *is* the thing being retired.
  - **Edit ONLY the provider-presence assertions**, named explicitly:
    `ConfigLoaderTests.GenerateDefaultConfig_ReturnsValidJson` (the provider part),
    `ConfigLoaderTests.Load_With*` provider pins, `ProviderRetirementTests`, and the `CriticConfig`
    presence tests; **remove the `MISSING_PROVIDERS` validation**.
  - **Document, per edited test**, which old assertion is now obsolete and why (contract retired here).
  - **Hard rule**: if ANY *other* Core test fails — one NOT about provider/critic presence — that is a
    **real regression**: investigate, do NOT edit. The net stays 100% in force for behavioral tests
    (`CriteriaMerger`, `*Writer` incl. `RequirementsWriterTests`, `TestPersistenceService`, criteria,
    grounding, parsing).
  - **No vestigial/dummy `ai.providers` block** (Options 1 and 3 rejected): full removal from the
    generated config, validation, and init.
- **Mechanics**: drop `MISSING_PROVIDERS`; `GenerateDefaultConfig`/`GenerateConfig` emit no
  `ai.providers`/`ai.critic`; remove `AiConfig.Providers`, `ProviderConfig`, `AiConfig.Critic`,
  `CriticConfig` from the model (reduce `AiConfig` to surviving cost/telemetry levers). Update only the
  named Core tests.
- **Forward-compat**: `System.Text.Json` ignores unknown keys by default (confirm `ConfigLoader` does not
  set `JsonUnmappedMemberHandling.Disallow`), so a legacy config still carrying `ai.providers`/`ai.critic`
  continues to load — honoring the spec Assumption with no migration.
- **`ai.critic` safety gate**: before deleting `CriticConfig`, confirm **zero** runtime readers of
  `.Ai.Critic` (investigation Q5 says dead: `CriticFactory` retired, `CriticModelResolver` uncalled). If
  `compile-critic-prompt` (Spec 055) reads `ai.critic.model`, that is out-of-scope coupling to flag and
  the critic-model selection must be preserved by another means before removal.

## R6 — Skill loop shape (FR-003/FR-004)

- **Decision**: `spectra-criteria` extraction recipe becomes: (1) `spectra docs changed --output-format
  json`; (2) for each `new|changed` doc → `spectra ai compile-extraction-prompt --doc <path>
  [--component <c>]` → **in-session** extraction turn (read the doc, emit JSON array) → `spectra ai
  ingest-criteria --doc <path> [--component <c>] --from <file>` (fail-loud; bounded retry on
  empty/parse outcomes); (3) summarize. Main-session turn, no subagent (FR-004).
- **Rationale**: 1:1 with the handler-owned per-doc loop and identical to the generation seam
  choreography (spectra-generate); the deterministic skip + fail-loud gates are preserved by the CLI
  commands, not the model.
- **Alternatives**: a `context: fork` extraction subagent (rejected, FR-004 — extraction needs the
  main-session file tools and has no adversarial-isolation requirement like the critic).
