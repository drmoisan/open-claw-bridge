---
name: make-skill-template
description: 'Create or scaffold a new repository skill for Codex. Use when asked to create a new skill or migrate a Copilot skill into the Codex runtime tree.'
---

# Make Skill Template

Repository compatibility wrapper for Codex skill scaffolding.

## Canonical Rule

Use the built-in `$skill-creator` system skill first, then apply repository-specific conventions from `.agents/README.md`.

## Required Repository Conventions

When creating a new repository skill:
- place it under `.agents/skills/<skill-name>/SKILL.md`,
- keep frontmatter minimal with `name` and `description`,
- write a trigger-rich description,
- prefer composing existing shared skills before inventing a new shared rule,
- centralize host-specific repo automation in `repo-automation-adapter`.

## Migration Rule

When the source is a GitHub Copilot skill:
- preserve the stable skill name when practical,
- move reusable behavior into the new Codex skill,
- remove Copilot-only tool assumptions from the runtime instructions,
- keep the skill body focused and defer repeated rules to existing shared skills.
