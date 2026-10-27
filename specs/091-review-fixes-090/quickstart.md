# Quickstart: verifying Spec 091

```bash
dotnet test --filter "FullyQualifiedName~VerdictOlderThanPrompt"    # T1–T3 (F1)
dotnet test --filter "FullyQualifiedName~RepairedIds"               # T4–T6 (F2)
dotnet test --filter "FullyQualifiedName~KeptHallucinated"          # T7 (F3)
dotnet test --filter "FullyQualifiedName~SkillsManifest"            # T8 (F2–F4 text guards)
dotnet test                                                          # everything; only the known ProfileFormatLoaderTests failure
```

Manual check with the built CLI in a scratch workspace from the real `spectra init`:

1. **Setup:** add a doc and tests TC-101…TC-103 with `ai ingest-tests`.
2. **F1:** compile the critic prompt for TC-101, write a grounded verdict, set its time an hour back. Edit TC-101 by
   hand, compile again, write no verdict. `ingest-verdict --from …TC-101.json`, `ingest-grounding --test TC-101`: exit
   4 `VERDICT_STALE` ("older than the last critic prompt"); `ingest-verdict --all` / `ingest-grounding --all`: TC-101
   under `stale`. Write the same verdict again (byte-identical): `ingest-grounding --all` writes it.
3. **F1 compatibility:** remove `compiled_at` from TC-102's record (a 090 record) and give TC-102 a verdict with an old
   time: accepted when the fingerprint matches.
4. **F2:** TC-102 and TC-103 have fresh partial verdicts. `ingest-grounding --all --repaired --repair-attempts 1`:
   exit 4 `IDS_REQUIRED`, nothing written. `… --ids TC-103,TC-999`: only TC-103 gets a `repaired: true` block;
   `not_found: ["TC-999"]`.
5. **F3:** give TC-102 a hallucinated verdict (fresh) and run `ingest-grounding --all`: `errors: 0`,
   `skipped_hallucinated: 1`, `hallucinated: ["TC-102"]`; stderr names it. `--test TC-102`: exit 4 as before.
6. **F4:** `.claude/agents/spectra-critic.agent.md` (after `update-skills`) says the compile writes only the fingerprint
   record.
7. **Pre-091 workspace:** a workspace with 090 records, verdict files and grounding blocks: every command runs and
   the 090 behaviour holds for 090 records.
