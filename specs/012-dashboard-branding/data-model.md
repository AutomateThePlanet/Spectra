# Data Model: SPECTRA Branding & Design System

**Feature**: 012-dashboard-branding
**Date**: 2026-03-21

## Overview

This feature introduces no new data entities, database changes, or C# model modifications. The "data model" for this feature is the design token system and brand asset inventory — both are static configuration expressed in CSS and file assets.

## Design Tokens

The design token system is a set of CSS custom properties that define the visual language. These are not stored in a database or C# model — they live in the CSS `:root` block and are consumed by all dashboard component styles.

### Token Categories

| Category | Token Pattern | Example | Count |
|----------|--------------|---------|-------|
| Primary colors | `--color-{name}` | `--color-navy: #1B2A4A` | 4 |
| Spectral palette | `--color-{name}` | `--color-green: #16A34A` | 5 |
| Neutral grays | `--color-gray-{weight}` | `--color-gray-500: #6B7280` | 7 |
| Backgrounds | `--bg-{surface}` | `--bg-page: #F9FAFB` | 4 |
| Shadows | `--shadow-{size}` | `--shadow-md: 0 4px 6px ...` | 3 |

### Token-to-Component Mapping

| Component | Tokens Used |
|-----------|-------------|
| Navigation bar | `--bg-nav`, `--color-navy` |
| Cards | `--bg-card`, `--color-gray-200`, `--shadow-sm`, `--shadow-md` |
| Status badges | `--color-green`, `--color-red`, `--color-gold` (via hardcoded badge colors) |
| Priority indicators | `--color-red`, `--color-gold`, `--color-gray-500` |
| Tables | `--color-gray-50`, `--color-gray-100`, `--color-gray-200`, `--color-gray-500` |
| Sidebar | `--bg-sidebar`, `--color-gray-200`, `--color-gray-500` |
| Page background | `--bg-page` |
| Body text | `--color-gray-900` |

## Brand Assets

| Asset | File | Location in Template | Usage |
|-------|------|---------------------|-------|
| Dashboard banner | `spectra_dashboard_banner.png` | `dashboard-site/assets/` | Nav bar logo image |
| Favicon | `spectra_favicon.png` | `dashboard-site/assets/` | Browser tab icon |
| README banner | `spectra_github_readme_banner.png` | `assets/` (repo root) | GitHub README header |

## CSS Class Inventory

### New Classes (to add)

| Class | Purpose |
|-------|---------|
| `.badge-passed` / `.badge-automated` | Green pill badge |
| `.badge-failed` / `.badge-uncovered` | Red pill badge |
| `.badge-skipped` / `.badge-partial` | Yellow pill badge |
| `.badge-blocked` | Purple pill badge |
| `.priority-high` | Red priority text |
| `.priority-medium` | Gold priority text |
| `.priority-low` | Gray priority text |
| `.nav-tab` / `.nav-tab.active` | Tab button styling (replaces `.nav-btn`) |

### Modified Classes (existing, restyled)

| Class | Change |
|-------|--------|
| `.card` | New border-radius (12px), updated shadow, hover transition |
| `.badge` | Pill style (9999px radius), uppercase, letter-spacing |
| `.sidebar` | Updated headings to uppercase small caps |
| `.test-row` | Add status-colored left border |
| `table th` | Uppercase, small font, letter-spacing |
| `table td` | Updated padding, bottom border color |

## No C# Model Changes

No changes to:
- `DashboardData`, `SuiteStats`, `TestEntry`, `RunSummary` models
- `DataCollector` data collection logic
- JSON serialization format
- MCP tool responses
