# Contract: `spectra docs index` — converged, index-only (FR-006/FR-007)

## Before → After

- **Before**: builds the doc index **and** inline-extracts criteria via `RequirementsExtractor`
  (model call, 2-min per-doc deadline), writing `docs/requirements/_requirements.yaml`. `--skip-criteria`
  gated the extraction block.
- **After**: builds the doc index **only**. No model call. No `_requirements.yaml`. Criteria are produced
  exclusively by the skill-driven `.criteria.yaml` seam.

## Invocation (unchanged surface)

```
spectra docs index [--force] [--output-format json|human] [--verbosity ...]
```

- `--skip-criteria`: accepted as a no-op for back-compat (documented) **or** removed if no CLI test
  references it. Either way it no longer gates anything (there is nothing to gate).

## Output

- Reports indexed/updated/total documents and the manifest path, as today.
- MUST NOT report `criteria_extracted` / `criteria_file` (those fields are removed from the result), and
  MUST NOT create `docs/requirements/`.

## Removed runtime coupling

- `DocsIndexHandler` no longer reads `config.Ai.Providers` (one of the three live sites eliminated).
- The CLI `RequirementsExtractor` is deleted (FR-009). The Core `RequirementsWriter`/`RequirementDefinition`
  remain (`[Obsolete]`, unit-tested) but are unreferenced by the handler.

## Exit codes

- `0` success; `4` token-budget/pre-flight failure (unchanged); `1` error. No extraction-specific exit
  paths remain.
