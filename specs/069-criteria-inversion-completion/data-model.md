# Data Model: Criteria-extraction inversion — completion + Copilot SDK removal

This feature is mostly a **relocation + deletion**; the persisted criteria data model is unchanged
(byte-compatibility is a hard requirement). The deltas are: where the classification helpers live, and
the config schema.

## Unchanged (byte-compatible — FR-012/013/014)

- **AcceptanceCriterion** (`Spectra.Core/Models/Coverage/AcceptanceCriterion.cs`) — unchanged. Fields:
  `id`, `text`, `rfc2119`, `source`, `source_type`, `source_doc`, `source_section`, `component`,
  `priority`, `tags`, `linked_test_ids`, `technique_hint`. Persisted to `docs/criteria/{component}.criteria.yaml`
  via `CriteriaFileWriter` (`OmitNull|OmitDefaults`, atomic temp+rename).
- **CriteriaIndex / CriteriaSource** (`Spectra.Core/Models/Coverage/`) — unchanged. Index
  `docs/criteria/_criteria_index.yaml`: `version`, `total_criteria` (recomputed on write), `sources[]`;
  each source: `file`, `source_doc`, `source_type`, `doc_hash`, `criteria_count`, `last_extracted` /
  `imported_at`, `outcome` (`"extracted"` only ever persisted).
- **ID scheme** — `AC-{COMPONENT}-{NNN}`, reused by exact text match (`CriteriaIngestor`).

## Relocated (FR-001 — `Agent/Copilot/` → `Spectra.CLI.Extraction`)

| Member (today) | New home |
|---|---|
| `CriteriaExtractor.ClassifyResponse` | `CriteriaResponseClassifier.Classify` (new static class) |
| `CriteriaExtractor.NormalizePriority` / `NormalizeTechniqueHint` | same new class (internal/private statics) |
| `ExtractionOutcome` (enum) | `Spectra.CLI.Extraction.ExtractionOutcome` |
| `CriteriaExtractionResult` / `RequirementsExtractionResult` (records) | `Spectra.CLI.Extraction` |

Consumers repointed: `CriteriaIngestor` (drop `using Spectra.CLI.Agent.Copilot;`), `IngestCriteriaCommand`.
Behaviour identical — pure move + namespace change (FR-018).

## Deleted (FR-009)

- `Spectra.CLI.Agent.Copilot.*`: `CopilotService`, `ProviderMapping`, `CriteriaExtractor`,
  `RequirementsExtractor` (and the GitHub Copilot SDK package reference in `Spectra.CLI.csproj`).

## Config schema delta (FR-010 + FR-019)

**Removed from the model** (`Spectra.Core/Models/Config/`):
- `AiConfig.Providers` (list) and `ProviderConfig` (type) — gone.
- `AiConfig.Critic` and `CriticConfig` (type) — gone.
- `AiConfig` retains only surviving cost/telemetry levers (e.g. `analysis_timeout_minutes`,
  `generation_timeout_minutes`, batch sizing) if any; otherwise `AiConfig` may collapse or be removed
  from `SpectraConfig` if nothing survives (decide during implementation by what other code/tests read).

**Validation** (`Spectra.Core/Config/ConfigLoader`):
- Remove the `MISSING_PROVIDERS` rule. A config with no `ai.providers` is valid.
- Unknown keys (legacy `ai.providers`/`ai.critic` in old configs) are ignored on load (forward-compat;
  confirm no `JsonUnmappedMemberHandling.Disallow`).

**Generation** (`ConfigLoader.GenerateDefaultConfig` / `GenerateConfig`, embedded
`Templates/spectra.config.json`): emit **no** `ai.providers` and **no** `ai.critic`. No dummy block.

**Display** (`Spectra.CLI` `ConfigHandler`): stop printing providers/critic.

**Retired but retained (Core, `[Obsolete]`, pinned by `RequirementsWriterTests` — do NOT delete)**:
- `RequirementDefinition`, `RequirementsWriter`, `RequirementsParser` (`Spectra.Core`). Unused by the
  product after this feature; still unit-tested in isolation. `docs/requirements/_requirements.yaml` is no
  longer produced.

## New (FR-005)

- **Changed-docs result** (shape emitted by `spectra docs changed`): a list of
  `{ path: string, component: string, status: "new" | "changed" | "unchanged", current_hash: string,
  indexed_hash: string | null }`. Derived purely from `FileHasher` + `CriteriaIndexReader`; no new
  persisted entity.

## State / outcome (unchanged — FR-012)

`ExtractionOutcome`: `Extracted` | `EmptyResponse` | `ParseFailure`. Only `Extracted` persists;
`EmptyResponse`/`ParseFailure` write nothing and leave the index byte-unchanged.
