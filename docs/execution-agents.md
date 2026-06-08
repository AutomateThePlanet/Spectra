---
title: Execution Agents
nav_order: 4
has_children: true
permalink: /execution-agents
---

# Execution Agents

How to drive SPECTRA's MCP execution server from different AI clients — Copilot CLI, Copilot Chat, Claude, and any generic MCP-compatible client.

> **Manual runs now use the local web console.** The execution agent orchestrates rather than driving a
> per-test loop in chat: it selects tests, runs `spectra run start`, launches `spectra run console`, and
> hands you the local `http://127.0.0.1:<port>/` URL. You record each verdict (PASS / FAIL / BLOCKED),
> comment, and screenshot in the browser console — its buttons enforce the human-in-the-loop guardrails.
> The agent stays on-call, reading current state from `spectra run status` (SQLite) to answer questions.
> See `cli-reference.md` (`spectra run console`).
