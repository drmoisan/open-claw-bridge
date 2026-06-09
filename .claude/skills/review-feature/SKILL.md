---
name: review-feature
description: Invoke the feature-review worker to produce feature-audit artifacts for an active feature folder.
---

# Review Feature Skill

This direct-use wrapper delegates review work to the `feature-review` worker.

## Inputs

- Active feature folder path
- Existing implementation, evidence, and requirement files under that folder

## Output Paths

When the active review scope is a selected version folder such as `docs/features/active/<feature>/v2/`, write the review artifacts into that selected version folder rather than the parent feature root.

- `docs/features/active/<feature-or-selected-version>/policy-audit.<timestamp>.md`
- `docs/features/active/<feature-or-selected-version>/code-review.<timestamp>.md`
- `docs/features/active/<feature-or-selected-version>/feature-audit.<timestamp>.md`

## Worker Routing

- Worker: `feature-review`
