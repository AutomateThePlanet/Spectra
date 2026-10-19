# Quickstart: Token Economy Follow-up (manual acceptance)

Run with the built CLI from a fresh checkout of the branch (`dotnet build`, then the `spectra` on the PATH or
`dotnet run --project src/Spectra.CLI --`). Record every result in tasks.md Notes, with the Claude Code version and the
critic model used.

## §1 — F1 quality gate (before F2 and F3)

1. Pick a workspace with a suite of ≥ 30 tests whose `source_refs` cite sections (`doc.md#anchor`), ideally from long
   documents. If fewer than 30 tests are "seen" (step 2), record how many; the gate needs ≥ 20.
2. Class each test. Run `spectra ai compile-critic-prompt --suite S --test <id> --full-docs`. The test is **seen** when
   every cited section's heading line and its last non-blank line are in the prompt; otherwise it is **cut**.
3. Prompt sizes: for each test, the character count of the prompt in both modes (`--output-format json`, the `prompt`
   length). Record median and max per mode, and how many section-mode items still hit the 8,000-character cut.
4. Section-mode run: in Claude Code, have `spectra-critic` judge each test (suite + id). Then run
   `spectra ai ingest-verdict --suite S --all --output-format json` and save the per-test verdicts.
5. Full-docs run: move the verdict files aside, run `spectra-critic` per test with `full_docs: true`, ingest again, and
   save the verdicts.
6. Gate:
   - "seen" tests: the verdicts agree for ≥ 90%;
   - "cut" tests: section mode has at least as many `grounded` as full-docs; list each with both verdicts;
   - all tests: section mode's grounded rate ≥ full-docs' − 5 points.
7. List every test whose verdict differs, with both verdicts and one line on why (from the findings).
8. **If any condition fails: stop, report, and don't start F2 or F3.**

## §2 — Workspaces (F2, F4)

1. **Real init**: `spectra init` in an empty folder. Expect:
   - `.claude/skills/spectra-generate/reference.md` and `.claude/skills/spectra-update/reference.md` exist and are
     recorded in `.spectra/skills-manifest.json`;
   - each agent's frontmatter has only `name`, `description`, `tools`, `model`.
   `spectra init --skip-skills` in another folder: no reference file.
2. **Pre-101 workspace with edits**: a workspace made by `6a4c41a`'s `spectra init`. Append a line to
   `.claude/skills/spectra-update/SKILL.md` and to `.claude/agents/spectra-analyst.agent.md`. Run `spectra update-skills`
   with the 101 build. Expect:
   - both reference files are written and listed under "Updated";
   - unedited skills and agents are updated;
   - the two edited files are listed under "Skipped (customized by user)" and are byte-identical to before.
   Then edit `reference.md` of `spectra-generate`, run `update-skills` again: it is skipped as customized.
3. **Moved workspace**: copy the workspace to another folder and run `update-skills`. There is no "entries from another
   location were dropped" line.
4. **`claude --debug`** (Claude Code ≥ 2.1.296): start a session in the real-init workspace, run `/agents`, exit. Search
   `~/.claude/debug/<session-id>.txt` for `does not recognize or apply`: 0 lines naming a SPECTRA agent.
5. **Auto-delegation spot check**: in the real-init workspace, ask "update the login tests after the doc change". The
   session uses the `spectra-update` skill (triage first), not `spectra-updater` directly.

## §3 — Generation budget and compaction (F2, F3)

1. In a workspace with docs for ≥ 24 tests in ≥ 3 batches, run `/context` and note the total.
2. Run `/spectra-generate` for that area, approve, and let it finish. Run `/context` again.
3. Per-test growth = (after − before) / tests created. Expect ≤ 300 tokens. Repeat with the `6a4c41a` build and
   expect 101 to be lower. Record both.
4. Check the report: every created id is listed with its outcome (grounded, repaired, dropped, unverified).
5. **SKILL.md token counts**: in a fresh session, invoke `/spectra-generate` and run `/context`, and do the same for
   `/spectra-update` in another fresh session (or use the API's token count on each installed SKILL.md). Record each
   skill's token count; each must be under 5,000. If either is over, move more reference material out and measure
   again.
6. **Compaction run**: start another generation and run `/compact` right after Step 7's last batch returns. Expect the
   session to complete Step 8 routing, the repair and the Step 9 report.
7. **Fallback**: set `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1`, generate a small batch. The generator returns
   `critic: not run`; the main session runs the critic per test and routes as before.

## §4 — Update budget (F3)

1. In a workspace with a documentation change touching ≥ 3 triage groups, run `/context`, then `/spectra-update` for
   the suite.
2. The re-verify question comes once, before the first group is edited. Answer yes.
3. After the run, `/context` again. Per-test growth ≤ 300 tokens; lower than `6a4c41a` for the same change.
4. Repeat on a copy answering no: no critic runs; the edited tests are listed as unverified.

## §5 — Fresh checkout

`git clone` the repository into a new folder, check out the branch, `dotnet build` (0 warnings), `dotnet test`
(0 failures). Record the totals against the baseline.
