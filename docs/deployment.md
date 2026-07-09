---
title: Deployment
nav_order: 5
has_children: true
permalink: /deployment
---

# Deployment

Guides for deploying the SPECTRA dashboard and configuring external integrations (Cloudflare Pages, Copilot Spaces).

## Two distinct deployment models that should not be conflated

SPECTRA has two web surfaces with **different** deployment models:

| Surface | What it is | Deployment | Lifetime |
|---|---|---|---|
| **Dashboard** (`spectra dashboard`) | A static analytics/coverage site | Built once, **hosted statically** (e.g. Cloudflare Pages) with GitHub OAuth | Published artifact, committed/served |
| **Execution console** (`spectra run console`) | A local, human-driven page for driving a manual run | A **local, detached HTTP server** on `127.0.0.1`, with no hosting, no auth, and no network beyond localhost | Ephemeral, gitignored, one human at a time; stops on `--stop` |

The console is **not** deployed anywhere. It is launched on the engineer's machine and talks straight to
the local execution database (`.execution/spectra.db`), and it is never committed and never served remotely.
See `cli-reference.md` (`spectra run console`) and the execution workflow for usage.
