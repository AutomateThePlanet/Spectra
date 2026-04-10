# Phase 1 — Data Model: Quickstart SKILL & Usage Guide

This feature has no runtime data model — it ships static content. The "entities" are the artifacts produced and the manifest entries that track them.

## Entities

### 1. Quickstart SKILL file

| Field | Value |
|-------|-------|
| **Embedded resource id** | `Spectra.CLI.Skills.Content.Skills.spectra-quickstart.md` |
| **Source path** | `src/Spectra.CLI/Skills/Content/Skills/spectra-quickstart.md` |
| **Output path** | `.github/skills/spectra-quickstart/SKILL.md` (in user project) |
| **Manifest key** | `.github/skills/spectra-quickstart/SKILL.md` |
| **Auto-loaded** | Yes — by `SkillResourceLoader.LoadAllSkills()` |
| **Typed accessor** | `SkillContent.Quickstart` |
| **Frontmatter** | `name: SPECTRA Quickstart`, `description: Guided onboarding and workflow walkthroughs for SPECTRA via Copilot Chat.` |
| **Required content sections** | Tools list, workflow overview (11 workflows), per-workflow detail with example conversations, troubleshooting, trigger phrases |

### 2. USAGE.md doc file

| Field | Value |
|-------|-------|
| **Embedded resource id** | `Spectra.CLI.Skills.Content.Docs.USAGE.md` |
| **Source path** | `src/Spectra.CLI/Skills/Content/Docs/USAGE.md` |
| **Output path** | `USAGE.md` (project root) |
| **Manifest key** | `USAGE.md` |
| **Loader** | `ProfileFormatLoader.LoadEmbeddedUsageGuide()` (new method, mirrors `LoadEmbeddedCustomizationGuide`) |
| **Required content sections** | Prerequisites, getting started, 11 workflows in offline form, troubleshooting, complete pipeline walk-through |

### 3. SkillsManifest entries (existing entity, two new keys)

The existing `SkillsManifest.Files` dictionary gains two new entries on init:

```json
{
  "files": {
    ".github/skills/spectra-quickstart/SKILL.md": "<sha256>",
    "USAGE.md": "<sha256>"
  }
}
```

`update-skills` consults this manifest: if the user's on-disk file hash matches the manifest hash, the file is unmodified and may be refreshed; if it differs, the user has customized it and the file is skipped.

### 4. Agent prompt files (existing entities, edited)

| File | Change |
|------|--------|
| `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md` | Add one delegation line: onboarding intents → spectra-quickstart SKILL |
| `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` | Add one delegation line: onboarding intents → spectra-quickstart SKILL |

## Relationships

```
spectra init
   │
   ├── writes ──► .github/skills/spectra-quickstart/SKILL.md  (from EmbeddedResource)
   │                            │
   │                            └── tracked by ──► SkillsManifest.Files[".github/skills/spectra-quickstart/SKILL.md"]
   │
   └── writes ──► USAGE.md  (from EmbeddedResource)
                  │
                  └── tracked by ──► SkillsManifest.Files["USAGE.md"]

spectra update-skills
   │
   ├── for each manifest entry, compare on-disk hash vs stored hash
   ├── if unchanged: refresh from EmbeddedResource, update stored hash
   └── if changed:   skip (user customization preserved)

Copilot Chat (user types "help me get started")
   │
   ├── generation agent receives intent
   │     │
   │     └── delegates to ──► spectra-quickstart SKILL
   │
   └── quickstart SKILL presents 11-workflow overview + example conversations
```

## State / lifecycle

- **Created** by `spectra init` (or by `spectra update-skills` when missing).
- **Updated** by `spectra update-skills` only when the user has not modified the file.
- **Deleted** by manual user action (not managed by SPECTRA).
- **No runtime mutation** — content is static between releases.
