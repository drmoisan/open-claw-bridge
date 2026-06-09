---
name: fill-feature-docs
description: Invoke the prd-feature worker to produce feature-document outputs from issue and research inputs.
---

# Fill Feature Docs Skill

This direct-use wrapper delegates feature-document work to the `prd-feature` worker.

## Inputs

- Feature folder issue and research context
- Existing spec and user-story files when present

## Output Paths

- `docs/features/active/<feature>/spec.md`
- `docs/features/active/<feature>/user-story.md`

## Worker Routing

- Worker: `prd-feature`
