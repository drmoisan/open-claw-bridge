---
name: orchestrator-not-spawnable-from-own-session
description: Agent(orchestrator) fails inside sessions whose main thread IS the orchestrator agent; epic-orchestrator child spawns are blocked in that configuration (hit 2026-07-06).
metadata:
  type: project
---

In a session where the main thread runs as the `orchestrator` agent, the `orchestrator` subagent type is NOT registered in the Agent tool (verified 2026-07-06: available types included `epic-orchestrator` and all specialists, but not `orchestrator`, even though `.claude/agents/orchestrator.md` exists with valid frontmatter). Consequence: `Agent(epic-orchestrator)` spawns fine, but its own `Agent(orchestrator)` child spawns fail with `Agent type 'orchestrator' not found`, blocking epic child-feature launches. Agent registration is fixed at session start and cannot be repaired mid-session.

Related: there is no `.claude/agents/pr-author.md`; PR authoring is skill-based ([[pr-author-skill]]), so `Agent(pr-author)` references in agent tool lists do not resolve either.

**Why:** The main-thread agent was wired via the top-level `"agent": "orchestrator"` key in `.claude/settings.json`; the harness excludes the main-thread agent's own type from the spawnable set (consistent with "orchestrator never delegates to itself"), and the epic delegation chain orchestrator → epic-orchestrator → Agent(orchestrator) re-enters that excluded type. The `agent` key is independent of the `permissions`/`hooks` sections, so removing it loses nothing else.

**How to apply:** The `agent` key was removed from `.claude/settings.json` on 2026-07-06, so new sessions start plain and `orchestrator` is spawnable. Run epics from such a plain session. For orchestrator-led single-feature sessions, wire it per-session via `claude --agent orchestrator` or a machine-local `.claude/settings.local.json` `{"agent": "orchestrator"}` — do not re-add the key to the shared settings.json. When an epic run blocks with `wave_2:BLOCKED_orchestrator_subagent_unavailable` in `artifacts/orchestration/epic-orchestrator-state.json`, this wiring is the first thing to check. Related: [[openclaw-vision-program-status]].
