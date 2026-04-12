# Data Model: Terminology, Folder Rename & Landing Page

**Feature**: 043-terminology-folder-landing  
**Date**: 2026-04-12

## Overview

This feature has no data model changes. It modifies a single default string value (`"tests/"` → `"test-cases/"`) in configuration and updates documentation content. No new entities, fields, relationships, or state transitions are introduced.

## Affected Configuration Entity

### TestsConfig

The only structural change is the default value of an existing field:

| Field | Type | Old Default | New Default |
|-------|------|-------------|-------------|
| `Dir` | string | `"tests/"` | `"test-cases/"` |

All other fields in `TestsConfig` remain unchanged. Users who have explicitly set `tests.dir` in their `spectra.config.json` will continue using their custom value.
