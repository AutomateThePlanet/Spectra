---
title: Deployment
parent: Guides
nav_order: 11
has_children: true
permalink: /deployment
---

# Deployment

How to host the SPECTRA dashboard. The step-by-step guide for Cloudflare Pages: [Cloudflare Pages setup](deployment/cloudflare-pages-setup).

## Two distinct deployment models that should not be conflated

SPECTRA has two web surfaces with **different** deployment models:

| Surface | What it is | Deployment | Lifetime |
|---|---|---|---|
| **Dashboard** (`spectra dashboard`) | A static analytics/coverage site | Built once, **hosted statically** (e.g. Cloudflare Pages) with GitHub OAuth | Published artifact, committed/served |
| **Execution console** (`spectra run console`) | A local, human-driven page for driving a manual run | A **local, detached HTTP server** on `127.0.0.1`, with no hosting, no auth, and no network beyond localhost | Ephemeral, gitignored, one human at a time; stops on `--stop` |

The console is **not** deployed anywhere. It is launched on the engineer's machine and talks straight to
the local execution database (`.execution/spectra.db`), and it is never committed and never served remotely.
See [Run tests with your agent](execution-agents) for how the console is used.
