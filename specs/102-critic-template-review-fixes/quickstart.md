# Quickstart: Review Fixes for 101 and the Critic Prompt Template

Manual checks beside the automated rows. Use a scratch workspace made by the real `spectra init` (built CLI:
`dotnet run --project src/Spectra.CLI --`).

## 1. The critic uses the template (F1)

1. In the scratch workspace, generate or write one test `TC-001` in suite `s` citing a document section.
2. `spectra ai compile-critic-prompt --suite s --test TC-001`: the output contains `## Technique Verification` and
   `## Arithmetic Verification`. Repeat with `--full-docs`, and with the legacy form
   `--test test-cases/s/TC-001.md --docs docs`.
3. Copy the bundled template: `spectra prompts reset critic-verification` (or `spectra prompts show critic-verification --raw`
   into `.spectra/prompts/critic-verification.md`). Add a line `QUICKSTARTMARKER` to its body and compile again: the
   line is in the output.
4. `spectra prompts validate critic-verification`: exit 0 ("valid (3 placeholders, 0 warnings)"; the command takes a template id).
5. `spectra ai compile-critic-prompt --suite s --test TC-001 --output-format json 2>/dev/null`: one JSON document on
   stdout.

## 2. Mixed-case refs (F2, Windows or macOS)

Give `TC-001` the refs `docs/Auth.md#login` and `docs/auth.md#logout` (file on disk: `docs/auth.md`). Compile: both
sections appear. Change the second ref to `docs/auth.md`: the whole file appears once.

## 3. Unchanged repairs (F3)

Run `spectra-generate` on a small suite until a test comes back partial, and its repair returns `unchanged`. The
repairer's lines show `<id> | unchanged` and `<id> | verdict partial <score>`, and the run's critic count (progress page
or transcript) has no critic call for that id after the repair. The report lists it as flagged partial.

## 4. Created lines (F4)

The generator's final message for a batch shows `<id> | created: <title>` lines. A test titled
"Verdict banner is shown" is reported as created and then verified.

## 5. Install (F9)

On a workspace from before 102 with one edited skill (`spectra-update`) and one unedited (`spectra-generate`):
`spectra update-skills` updates `spectra-generate` (its `tools:` line ends in `Agent`) and reports `spectra-update` as
customised. `.claude/agents/*.agent.md` unedited copies update in the same way.

## 6. Fresh checkout

```bash
git worktree add ../spectra-102-check 102-critic-template-review-fixes
```
In it (with `core.autocrlf=true`): `dotnet build --no-incremental` (0 warnings) and `dotnet test` (0 failures). Record
totals in the tasks' Notes, then remove the worktree.
