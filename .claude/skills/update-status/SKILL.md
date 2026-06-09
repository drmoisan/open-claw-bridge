---
name: update-status
description: Invoke the status-updater worker to reconcile status artifacts and synchronized status outputs.
---

# Update Status Skill

This direct-use wrapper delegates status synchronization work to the `status-updater` worker.

## Inputs

- Epic or feature folder path
- Existing plan, evidence, and issue status context

## Output Paths

- `artifacts/status/status-sync.<timestamp>.md`

## Worker Routing

- Worker: `status-updater`
