# Codex Skill Architecture

## Purpose

This directory is the canonical Codex runtime surface for repository-local skills.

Codex-supported repository locations in this repo are:

- `.agents/skills/<skill-name>/SKILL.md` for reusable workflow skills
- `.codex/agents/*.toml` for subagent definitions
- `.codex/prompts/*.md` for prompt entrypoints and lightweight workflow launchers

The legacy GitHub Copilot ecosystem under `.github/skills` and `.github/agents` remains as historical source material during migration. New Codex-native runtime behavior should be authored in `.agents/skills` and consumed from `.codex/agents`.

## Layering

Design skills in layers so behavior is authored once and reused everywhere:

1. Foundation skills
   - Policy order
   - Atomic plan contract
   - Acceptance criteria tracking
   - Evidence conventions
   - Canonical-location audits

2. Integration skills
   - PR base resolution
   - PR context artifact rules
   - Feature-promotion lifecycle
   - Remediation handoff
   - Host-surface adapters

3. Language routing and orchestration-state skills
   - Change-budget routers
   - Checkpoint and resume contracts

4. Workflow skills
   - `atomic-executor`
   - `atomic-planner`
   - `feature-review`
   - `orchestrator-workflow`

5. Specialist support skills
   - `commit-message-conventions`
   - `pr-authoring`

6. Subagents
   - Keep `.codex/agents/*.toml` concise.
   - Reference workflow and shared skills by name instead of embedding long duplicated instructions.

## Anti-Duplication Rules

1. If multiple workflows need the same rule, extract it into one shared skill.
2. Workflow skills should name the shared skills they depend on rather than restating those blocks.
3. Environment-specific repo automation and its MCP dependency binding should live in `repo-automation-adapter`, not in each workflow skill.
4. Canonical paths should be defined in exactly one skill; other skills should reference that skill instead of repeating the path.
5. If Codex already ships a suitable system skill, prefer a thin repo-local compatibility wrapper instead of re-implementing the same scaffolding.
6. If an agent persona grows reusable decision rules or formatting rules, move those rules into a shared skill and keep the agent as a thin wrapper.
7. If a top-level workflow routes between multiple existing skills or subagents, capture that routing once in a shared workflow skill and keep the prompt or agent as a launcher.

## Migration Rules

When migrating a Copilot artifact:

1. If it defines reusable repository guidance, migrate it to `.agents/skills/<name>/SKILL.md`.
2. If it defines a reusable agent persona or bounded delegation role, migrate it to `.codex/agents/<name>.toml`.
3. If it is mainly a launch prompt or an orchestration shortcut, migrate it to `.codex/prompts/<name>.md`.
4. Preserve stable names where possible so downstream handoffs remain readable and consistent.
5. If a Copilot workflow relied on `drmCopilotExtension.*` commands or another host-specific surface, move that translation logic into `repo-automation-adapter`, target semantic MCP tools on server `drmCopilotExtension` when available, and keep the business workflow skill host-agnostic.

## External Tool Bindings

When a repository skill owns an external MCP dependency:

- declare that dependency once in `agents/openai.yaml` beside the owning skill
- keep downstream workflow skills dependent on the adapter skill, not on duplicated MCP binding metadata

## Future Migrations

Use the system `$skill-creator` skill when scaffolding a new Codex skill, then apply the repository rules in this file:

- place the runtime skill under `.agents/skills`
- keep frontmatter minimal
- make the description explicit about triggers
- reference existing shared skills before creating a new one
- add a new shared skill only when the behavior cannot be expressed as a composition of existing skills
