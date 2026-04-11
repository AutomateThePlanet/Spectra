# Contract: `.spectra-debug.log` Line Format (Spec 040)

## Header

Every line begins with a UTC timestamp and a component tag (existing format, unchanged):

```
2026-04-11T12:34:56.789Z [generate] <message>
```

Component values: `analyze`, `generate`, `critic`, `update`, `criteria`, `testimize`, `analysis`.

## AI lines (NEW format)

Every AI call line MUST end with these key=value pairs (in this order):

```
model=<name> provider=<name> tokens_in=<int|?> tokens_out=<int|?>
```

When the API response did not include a `usage` block, `tokens_in` and `tokens_out` MUST be the literal `?` so the line stays grep-able.

### Examples

```
2026-04-11T12:34:56.789Z [analysis] ANALYSIS OK behaviors=42 elapsed=18.5s model=gpt-4.1 provider=github-models tokens_in=8900 tokens_out=6200
2026-04-11T12:35:09.123Z [generate] BATCH OK requested=8 elapsed=12.3s model=gpt-4.1-mini provider=azure-openai tokens_in=4521 tokens_out=3890
2026-04-11T12:35:11.456Z [critic  ] CRITIC OK verdict=grounded elapsed=2.1s model=gpt-4o-mini provider=github-models tokens_in=2100 tokens_out=340
2026-04-11T12:35:30.000Z [update  ] UPDATE OK chunk=1 elapsed=58.0s model=gpt-4.1-mini provider=azure-openai tokens_in=12000 tokens_out=8400
2026-04-11T12:35:42.000Z [criteria] CRITERIA OK doc=auth.md elapsed=11.2s model=gpt-4.1 provider=github-models tokens_in=3400 tokens_out=1200
```

## AI failure lines

`BATCH TIMEOUT`, `ANALYSIS PARSE_FAIL`, `CRITIC ERROR`, etc. MUST also include `model=` and `provider=` (token counts may be `?`):

```
2026-04-11T12:36:00.000Z [analysis] ANALYSIS PARSE_FAIL response_chars=24389 elapsed=568.8s model=deepseek-v3.2 provider=github-models tokens_in=? tokens_out=?
```

## Non-AI lines (UNCHANGED)

Non-AI lines MUST remain in their existing format and MUST NOT include the new suffixes:

```
2026-04-11T12:34:00.000Z [testimize] TESTIMIZE HEALTHY command=testimize-mcp tools_added=2
2026-04-11T12:34:00.500Z [testimize] TESTIMIZE DISPOSED
```

## Gating

Debug log writes MUST be no-ops when **both** of the following are true:
- `spectra.config.json` `debug.enabled` is `false` (or absent)
- The current command is not running with `--verbosity diagnostic`

When either condition allows logging, **all** AI lines (per the format above) and existing non-AI lines are written.

## Backwards compatibility

The old `ai.debug_log_enabled` field is removed. Existing configs that still set it are silently ignored (System.Text.Json default behavior).
