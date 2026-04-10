# Tasks: 034 - GitHub Pages Documentation Site

Dependency-ordered. All tasks are documentation/config; no C# changes.

## Phase A — Site scaffolding

- [ ] **A1**. Create `docs/_config.yml` with title, theme `just-the-docs`, baseurl `/Spectra`, search, color scheme `spectra`, footer, aux links, gh_edit_*, exclusion list.
- [ ] **A2**. Create `docs/Gemfile` with `just-the-docs` and `jekyll-seo-tag`.
- [ ] **A3**. Create `docs/_sass/color_schemes/spectra.scss` with ATP palette overrides.
- [ ] **A4**. Create `docs/index.md` landing page (`layout: home`, nav_order 0).

## Phase B — Section parents

- [ ] **B1**. Create `docs/user-guide.md` (nav_order 2, has_children).
- [ ] **B2**. Create `docs/architecture.md` (nav_order 3, has_children).
- [ ] **B3**. Create `docs/execution-agents.md` (nav_order 4, has_children).
- [ ] **B4**. Create `docs/deployment.md` (nav_order 5, has_children).

## Phase C — Frontmatter on existing docs (no content changes)

Top-level files:
- [ ] **C1**. `docs/getting-started.md` → `title: Getting Started`, `nav_order: 1`.
- [ ] **C2**. `docs/cli-reference.md` → `parent: User Guide`, `nav_order: 1`.
- [ ] **C3**. `docs/configuration.md` → `parent: User Guide`, `nav_order: 2`.
- [ ] **C4**. `docs/test-format.md` → `parent: User Guide`, `nav_order: 3`.
- [ ] **C5**. `docs/generation-profiles.md` → `parent: User Guide`, `nav_order: 4`.
- [ ] **C6**. `docs/grounding-verification.md` → `parent: User Guide`, `nav_order: 5`.
- [ ] **C7**. `docs/coverage.md` → `parent: User Guide`, `nav_order: 6`.
- [ ] **C8**. `docs/document-index.md` → `parent: User Guide`, `nav_order: 7`.
- [ ] **C9**. `docs/skills-integration.md` → `parent: User Guide`, `nav_order: 8`.
- [ ] **C10**. `docs/copilot-spaces-setup.md` → `parent: Deployment`, `nav_order: 3`.
- [ ] **C11**. `docs/DEVELOPMENT.md` → `title: Development`, `nav_order: 6`.

Subfolder files:
- [ ] **C12**. `docs/architecture/overview.md` → `parent: Architecture`, `nav_order: 1`.
- [ ] **C13**. `docs/analysis/cli-vs-chat-generation.md` → `parent: Architecture`, `nav_order: 2`.
- [ ] **C14**. `docs/deployment/cloudflare-pages-setup.md` → `parent: Deployment`, `nav_order: 1`.
- [ ] **C15**. `docs/deployment/github-packages-setup.md` → `parent: Deployment`, `nav_order: 2`.
- [ ] **C16**. `docs/execution-agent/overview.md` → `parent: Execution Agents`, `nav_order: 1`.
- [ ] **C17**. `docs/execution-agent/copilot-cli.md` → `parent: Execution Agents`, `nav_order: 2`.
- [ ] **C18**. `docs/execution-agent/copilot-chat.md` → `parent: Execution Agents`, `nav_order: 3`.
- [ ] **C19**. `docs/execution-agent/claude.md` → `parent: Execution Agents`, `nav_order: 4`.
- [ ] **C20**. `docs/execution-agent/generic-mcp.md` → `parent: Execution Agents`, `nav_order: 5`.

## Phase D — CI/CD

- [ ] **D1**. Create `.github/workflows/docs.yml` (jekyll-build-pages → deploy-pages, paths filter on `docs/**`).

## Phase E — Repo metadata

- [ ] **E1**. Update `README.md` with docs site badge and "Browse the full documentation" pointer.
- [ ] **E2**. Add 034 entry to `CLAUDE.md` Recent Changes.

## Phase F — Verify

- [ ] **F1**. `dotnet test` — ensure no test regressions (expected: still passes; this is a docs-only change).
- [ ] **F2**. `git status` — confirm only intended files staged.

## Phase G — Ship

- [ ] **G1**. Commit all changes on branch `034-github-pages-docs`.
- [ ] **G2**. Merge into `main` (no-ff to preserve branch history).
- [ ] **G3**. Push `main` to origin.
