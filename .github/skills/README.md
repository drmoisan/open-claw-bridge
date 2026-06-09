# Skills Taxonomy

## Taxonomy

Skills live under `.github/skills/<skill-name>/SKILL.md` and define reusable guidance that can be shared across agents and prompts. Each skill directory contains a single canonical `SKILL.md` file and optional supporting assets (kept minimal).

## Frontmatter Requirements

Every `SKILL.md` must include YAML frontmatter with:
- `name`: the canonical skill name (kebab-case recommended).
- `description`: a concise summary of the skill’s purpose.

## Canonical Location

Reusable guidance must be authored once in its canonical skill file. Agents and prompts should reference the canonical skill instead of duplicating its content. Canonical references should point to the full path of the skill, for example:

- `.github/skills/feature-review-workflow/SKILL.md`

## Examples

```yaml
---
name: feature-review-workflow
description: Feature-branch review workflow: artifact generation, evidence storage, remediation handoff.
---
```
