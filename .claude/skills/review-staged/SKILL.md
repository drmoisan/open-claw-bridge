---
name: review-staged
description: Invoke the staged-review worker to produce staged-review artifacts from the staged diff.
---

# Review Staged Skill

This direct-use wrapper delegates review work to the `staged-review` worker.

## Inputs

- Current staged diff
- Repository context needed to review staged changes

## Output Paths

- `artifacts/reviews/staged-review.<timestamp>.md`

## Worker Routing

- Worker: `staged-review`
