---
name: skill-canonical-location-audit
description: 'Audit skills for canonical-location duplication. Use when adding or changing skills that define canonical paths, folders, or file names.'
---

# Skill Canonical-Location Audit

Audit skills to ensure canonical locations are defined in exactly one place.

## Scope

- Applies to repository skills under `.agents/skills/**/SKILL.md`
- Focuses on canonical paths, folders, and file names

## Workflow

1. Inventory explicit canonical-location statements.
2. Group them by the item they describe.
3. Detect duplicates.
4. Recommend one skill as the single source of truth for each duplicated item.

## Notes

- Indirect references to another skill do not count as duplication.
- Only explicit canonical definitions count.
