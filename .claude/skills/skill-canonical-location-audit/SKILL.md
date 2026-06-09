---
name: skill-canonical-location-audit
description: 'Audit skills for canonical-location duplication. Use when ensuring a canonical location for a given item is defined in exactly one skill and duplicates are flagged.'
---

# Skill Canonical-Location Audit

Audit skills to ensure canonical locations for the same item are not defined in multiple places.

## When to Use This Skill

Use this skill when:
- You add or update skills that define canonical locations.
- You want to verify that canonical paths live in exactly one skill.
- You need a duplication report listing which skills conflict.

## Scope

- Applies to all skills under `.claude/skills/**/SKILL.md`.
- Focuses on canonical locations (paths, folders, or file names) for a given item (e.g., PR context artifacts, evidence locations, issue mirrors).

## Audit Workflow

1) Inventory canonical-location statements
   - Read each `SKILL.md` and extract explicit canonical location statements (paths, folders, file names).
   - Group statements by the item they describe (e.g., “PR context summary”, “baseline evidence”).

2) Detect duplicates
   - For each item group, verify that exactly one skill defines its canonical location.
   - If two or more skills define the same item’s canonical location, flag it as a duplication.

3) Report results
   - Produce a short report listing:
     - The item with duplicated canonical location
     - The conflicting skills
     - A recommendation for consolidation (name the single skill to keep as source-of-truth)

## Output Template (Suggested)

- Item: <canonical item name>
  - Skills with definitions: <skill A>, <skill B>, ...
  - Recommendation: Keep <skill X> as canonical; remove duplicates from others.

## Notes

- If a skill references a canonical location indirectly (e.g., “see evidence-and-timestamp-conventions”), do NOT treat that as a duplication.
- Only treat explicit canonical paths or filenames as definitions.


