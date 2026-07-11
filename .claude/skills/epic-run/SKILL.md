---
name: epic-run
description: Execute a previously planned epic by replaying the committed epic-kickoff artifact through the epic-orchestrator agent. Use after /epic-plan has prepared the epic (issues, folders, research, specs, atomic plans, preflight clearance) and the user is ready to execute end-to-end.
argument-hint: "[epic-slug]"
context: fork
agent: epic-orchestrator
---

# Epic Run Skill

A user invocation (`/epic-run <epic-slug>`) forks the `epic-orchestrator` agent to execute an
epic that `epic-planner` has already prepared. The epic slug (or a path under
`docs/features/epics/`) for this run is:

$ARGUMENTS

## Procedure

1. Resolve the epic home. A bare slug resolves to `docs/features/epics/<epic-slug>/`; a path
   argument resolves to its containing epic folder.
2. Read the committed kickoff artifact at `docs/features/epics/<epic-slug>/epic-kickoff.md`.
   - If the file does not exist, STOP without delegating anything and report that the epic has
     no committed kickoff artifact: the user must run `/epic-plan` first (or, for an epic that
     was authored manually, invoke `/epic-orchestrate <epic-manifest-path>` directly).
3. Execute the kickoff artifact's `## Invocation Prompt` section as the epic objective, applying
   the `epic-orchestrate` skill procedure and the `## Prepared-Epic Execution (epic-planner
   Handoff)` section of `.claude/agents/epic-orchestrator.md`: reuse the existing integration
   branch, and have each child `Agent(orchestrator)` delegation resume at atomic execution from
   its committed `plan-path` rather than re-running promotion, research, or planning.
4. Honor existing checkpoint state: if `artifacts/orchestration/epic-orchestrator-state.json`
   already tracks this epic, resume per the `epic-orchestrate` skill's resume procedure instead
   of restarting.

## Scope

This skill adds no procedure of its own beyond kickoff-artifact resolution; wave scheduling,
the wave barrier, merge-on-green fan-in, worktree cleanup, `epic-status.md` maintenance, and
the final integration-to-`main` PR are governed entirely by the `epic-orchestrate` skill.
