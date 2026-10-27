# Quickstart: verifying 098 by hand

Build once: `dotnet build`; run the CLI as `dotnet run --project src/Spectra.CLI --`.

1. **Real init**: `spectra init` in an empty folder → config has `ai.critic.model: sonnet` and
   `generation.coverage_threshold: 0.6`; `spectra update-skills` → no warning.
2. **Pre-069 config (F1)**: replace `ai.critic` with `{"enabled": true, "provider": "github-models", "model": "gpt-5-mini"}`
   → `spectra validate`, `spectra ai update-triage --all-suites --dry-run`, `spectra docs changed` all run;
   `spectra update-skills` warns and `.claude/agents/spectra-critic.agent.md` says `model: sonnet`. Same with
   `{"model": "openai/gpt-4.1"}`.
3. **Audit (F2)**: verdict files `grounded` / `partial` / `hallucinated` for three tests without blocks →
   `spectra ai audit-grounding --suite s --output-format json` → `ground`, `repair`, `review`.
4. **Suite delete (F3)**: `spectra suite delete s --dry-run --output-format json` → verdict files only under
   `verdict_files_removed`.
5. **Manifest (F4)**: pre-096 manifest with relative v1 + absolute v2 keys; edit the file → skipped; restore v2 →
   updated.
6. **Keep-baseline (F5)**: `spectra ai ingest-update s --test-id TC-9 --from f.json --keep-baseline` for an
   unbaselined test → `update-triage --dry-run` still lists it as unbaselined.
7. **Anchors (F6)**: `## ![logo](x.png) Setup` with ref `doc.md#setup` → resolves.
8. **Group prompt (F7)**: `compile-update-prompt --group G1` where two tests disagree on a criterion → per-test states.
9. **count_fallback (F8)**: customised pre-094 `test-generation.md`, edit a doc → `compile-prompt --batch B1` exit 0 +
   warning.
10. **Coverage (F9)**: remove `coverage_threshold`, set `duplicate_threshold: 0.75` → `ingest-analysis` prints the notice.
