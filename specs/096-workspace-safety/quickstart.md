# Quickstart: Workspace Safety and Hygiene

## CLI checks (built CLI)

1. **update-skills keeps edits (real init):**
   - `spectra init`;
   - append a line to `.spectra/prompts/test-generation.md`, `profiles/_default.yaml` and `CUSTOMIZATION.md`;
   - run `spectra update-skills`;
   - expect all three byte-identical, and the edited template reported as skipped.
2. **Pre-096 manifest:**
   - replace the manifest with mixed keys: absolute skill keys, `\` relative template keys, and one key from another
     machine (`D:\other\x.md`);
   - run `update-skills`;
   - expect the keys normalised, the single summary warning (1 entry), and no edited file changed.
2b. **Moved workspace:** copy a real-init workspace to another folder and run `update-skills` there → no warning, no
   edited file touched (keys re-rooted).
3. **Suite delete:**
   - suites `a` (TC-101) and `b`: TC-201 has `depends_on: TC-101`, TC-202 lists `[TC-101]`, and TC-203 has a step
     `  - TC-101` and a 3-newline run;
   - give `a` a staging file, a failed file, a generation plan and verdict files;
   - `spectra suite delete a --dry-run --output-format json` lists all of it;
   - `--force`: TC-201 cleaned, TC-202 warned, TC-203 byte-identical, leftovers gone.
4. **show:** `spectra show <id>` on a suite from `ingest-tests` prints the test.
5. **validate:** break one test's frontmatter; `spectra validate` exits 1 with "failed".
6. **gitignore:**
   - in a workspace with a 090–095 manifest record, `update-skills` adds the two new entries once and writes one marker
     line;
   - remove `.spectra/verdicts/` from `.gitignore` and delete the manifest (a teammate clone);
   - `update-skills` doesn't re-add it.
7. **delete performance:** time `spectra delete <one id> --force` in a 2,500-test workspace on `23aa08c` and on 096.
8. **Old config:** a config with `ai.generation_batch_size` loads (`spectra validate`, `ingest-analysis`); a new init
   config doesn't contain it.
9. **Scripts:** `git check-attr eol -- specs/094-generation-batch-prompts/acceptance/setup-workspace.sh` → `lf`.
