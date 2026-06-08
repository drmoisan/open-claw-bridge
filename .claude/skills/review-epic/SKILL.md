---
name: review-epic
description: Invoke the epic-review worker to produce epic-audit artifacts for an epic folder.
---

# Review Epic Skill

This direct-use wrapper delegates review work to the `epic-review` worker.

## Inputs

- Active epic folder path
- Evidence and child-feature context under that epic

## Output Paths

- `docs/features/epics/<epic>/epic-audit.<timestamp>.md`

## Worker Routing

- Worker: `epic-review`
