# Implementation Plan: 034 - GitHub Pages Documentation Site

## Summary

Docs-only feature. Add Jekyll/Just the Docs configuration to the existing `docs/` directory and a GitHub Actions workflow that builds and deploys it to GitHub Pages on push to `main`. Insert YAML frontmatter into all 17 existing user-facing markdown files (in their actual subfolder locations — no file moves) so they appear in the sidebar under the correct section.

No C# code changes. No tests added (documentation-only change). Existing test suite must remain green.

## Actual `docs/` layout (verified against the repo)

The user-supplied spec assumed a flat `docs/` layout. The real repo has subfolders:

```
docs/
├── DEVELOPMENT.md
├── analysis/
│   └── cli-vs-chat-generation.md
├── architecture/
│   └── overview.md
├── cli-reference.md
├── configuration.md
├── copilot-spaces-setup.md
├── coverage.md
├── deployment/
│   ├── cloudflare-pages-setup.md
│   └── github-packages-setup.md
├── document-index.md
├── execution-agent/
│   ├── claude.md
│   ├── copilot-chat.md
│   ├── copilot-cli.md
│   ├── generic-mcp.md
│   └── overview.md
├── generation-profiles.md
├── getting-started.md
├── grounding-verification.md
├── skills-integration.md
└── test-format.md
```

There is no `docs/_index.md`, no `docs/criteria/` folder, and no `docs/_criteria_index.yaml` at the moment — but they may be auto-generated later by the SPECTRA CLI, so the `_config.yml` exclusion list still covers them.

## Final navigation hierarchy (adapted to the real layout)

```
SPECTRA Docs
├── Home                         (index.md, nav_order: 0, layout: home)
├── Getting Started              (getting-started.md, nav_order: 1)
│
├── User Guide                   (user-guide.md, nav_order: 2, has_children: true)
│   ├── CLI Reference            (cli-reference.md, nav_order: 1)
│   ├── Configuration            (configuration.md, nav_order: 2)
│   ├── Test Format              (test-format.md, nav_order: 3)
│   ├── Generation Profiles      (generation-profiles.md, nav_order: 4)
│   ├── Grounding Verification   (grounding-verification.md, nav_order: 5)
│   ├── Coverage Analysis        (coverage.md, nav_order: 6)
│   ├── Document Index           (document-index.md, nav_order: 7)
│   └── Skills Integration       (skills-integration.md, nav_order: 8)
│
├── Architecture                 (architecture.md, nav_order: 3, has_children: true)
│   ├── Overview                 (architecture/overview.md, nav_order: 1)
│   └── CLI vs Chat Generation   (analysis/cli-vs-chat-generation.md, nav_order: 2)
│
├── Execution Agents             (execution-agents.md, nav_order: 4, has_children: true)
│   ├── Overview                 (execution-agent/overview.md, nav_order: 1)
│   ├── Copilot CLI              (execution-agent/copilot-cli.md, nav_order: 2)
│   ├── Copilot Chat             (execution-agent/copilot-chat.md, nav_order: 3)
│   ├── Claude                   (execution-agent/claude.md, nav_order: 4)
│   └── Generic MCP              (execution-agent/generic-mcp.md, nav_order: 5)
│
├── Deployment                   (deployment.md, nav_order: 5, has_children: true)
│   ├── Cloudflare Pages Setup   (deployment/cloudflare-pages-setup.md, nav_order: 1)
│   ├── GitHub Packages Setup    (deployment/github-packages-setup.md, nav_order: 2)
│   └── Copilot Spaces Setup     (copilot-spaces-setup.md, nav_order: 3)
│
└── Development                  (DEVELOPMENT.md, nav_order: 6)
```

Note: `parent: User Guide` works regardless of the actual file path under `docs/` — Just the Docs uses the `parent` frontmatter value, not the directory layout, so we do not need to move any files.

## Files to create

| Path | Purpose |
|---|---|
| `docs/_config.yml` | Jekyll/Just the Docs config with theme, branding, exclusions |
| `docs/Gemfile` | Gem dependencies (`just-the-docs`, `jekyll-seo-tag`) |
| `docs/_sass/color_schemes/spectra.scss` | ATP-brand SCSS color overrides |
| `docs/index.md` | Landing page (`layout: home`) |
| `docs/user-guide.md` | User Guide section parent |
| `docs/architecture.md` | Architecture section parent |
| `docs/execution-agents.md` | Execution Agents section parent |
| `docs/deployment.md` | Deployment section parent |
| `.github/workflows/docs.yml` | Build & deploy workflow |

## Files to modify (frontmatter insertion only)

All 17 existing user-facing doc files get a YAML frontmatter block prepended. Original content is preserved verbatim below the frontmatter.

Top-level: `getting-started.md`, `cli-reference.md`, `configuration.md`, `test-format.md`, `generation-profiles.md`, `grounding-verification.md`, `coverage.md`, `document-index.md`, `skills-integration.md`, `copilot-spaces-setup.md`, `DEVELOPMENT.md`

Subfolders: `architecture/overview.md`, `analysis/cli-vs-chat-generation.md`, `deployment/cloudflare-pages-setup.md`, `deployment/github-packages-setup.md`, `execution-agent/{overview,copilot-cli,copilot-chat,claude,generic-mcp}.md`

Plus `README.md` and `CLAUDE.md` for documentation references.

## Out of scope

- Moving or renaming any existing doc file
- Fixing pre-existing broken cross-doc links (handled reactively post-deploy)
- Creating a logo SVG or favicon (can be added later — text fallback works)
- Local Jekyll preview tooling
- Any C# code or test changes

## Risks & mitigations

- **Risk**: GitHub Pages source not yet set to "GitHub Actions" — first deploy fails. **Mitigation**: Document the one-time manual setting in the workflow file header.
- **Risk**: Some inline relative links break under Jekyll. **Mitigation**: Just the Docs handles `.md` links by default; broken links can be fixed reactively in a follow-up PR.
- **Risk**: A future SPECTRA `docs index` run creates `_index.md` which could leak into the site. **Mitigation**: Already excluded via `_config.yml` patterns.
