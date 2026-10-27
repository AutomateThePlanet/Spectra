# Quickstart: Verification Hardening

## CLI checks (built CLI)

1. **Grounding ids (F1):**
   - a suite with fresh `grounded` verdicts for TC-1, TC-2 and TC-3;
   - run `ingest-grounding --suite S --all --ids TC-1,TC-2`;
   - expect blocks on TC-1 and TC-2 only, and `written: 2`.
   - Read the installed skills: generate 8a and update 5a pass `--ids`, and the 075 guard text is unchanged.
2. **Audit (F2):** use a pre-097 workspace with:
   - verdict files of suites A and B, and one stray verdict;
   - one stale verdict (the test was edited after the record);
   - 090 and 091 records.

   Then:
   - `audit-grounding --suite A` lists only A's tests, the stale one is `critic` / `stale: true`, and `summary.stale`
     is 1;
   - `--action repair` shows only repair entries;
   - `--ids X,ZZ` → `not_found: ["ZZ"]`.
3. **Repair (F3):** with a stale partial verdict:
   - `compile-repair-prompt` exits 4 with `VERDICT_STALE`;
   - `compile-repair-batch` leaves it out of stdout and lists it on stderr under `stale`.
4. **Clock (F4):** set the record's file time t and the verdict's file time t + 10 s, and edit `compiled_at` to t + 1 h
   → `ingest-verdict --from` accepts the verdict. Set the verdict to t − 10 s and `compiled_at` to t − 1 h → refused.
5. **Empty ids (F5):**
   - `ingest-grounding --all --ids " , "` processes the whole suite;
   - with `--repaired` → `IDS_REQUIRED`.
6. **Dry run (F6):** for each of `ingest-verdict --all`, `ingest-grounding --all` and `ingest-analysis`:
   - run on two copies, once with `--dry-run` and once without;
   - expect the same JSON except `dry_run`, and the same exit code;
   - the dry-run copy must be byte-identical to before.

   `ingest-grounding --test T --dry-run` → exit 1.
7. **Critic model (F7):**
   - real `spectra init` → config `ai.critic.model: "sonnet"`, agent `model: sonnet`;
   - set `opus` and run `update-skills` → the agent says `model: opus`;
   - edit the agent, set `haiku`, run `update-skills` → the agent is unchanged and reported as customized;
   - `ai.critic.model: "two words"`, then `"[1m]"`, then malformed JSON → `update-skills` warns
     `spectra.config.json is invalid (…)`, keeps the critic's model line, refreshes the rest and exits 0;
   - pre-097 workspace (pinned agent, no `ai.critic`) → `update-skills` sets `model: sonnet`;
   - `spectra validate` with the retired `ai.critic.provider` key still shows the retired-key notice.
8. **No regressions:** `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` pass, and the full suite has 0 failures.
