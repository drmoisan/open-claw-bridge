# Codex Repository Skills

Repository-local Codex skills live under `.agents/skills/<skill-name>/SKILL.md`.

## Skill Groups

- Foundation
  - `policy-compliance-order`
  - `atomic-plan-contract`
  - `acceptance-criteria-tracking`
  - `evidence-and-timestamp-conventions`
  - `skill-canonical-location-audit`

- Integration
  - `repo-automation-adapter`
  - `pr-base-branch-merge-base`
  - `pr-context-artifacts`
  - `feature-promotion-lifecycle`
  - `policy-audit-template-usage`
  - `remediation-handoff-atomic-planner`

- Language routing and state
  - `csharp-change-budget-router`
  - `csharp-orchestration-state-machine`
  - `powershell-change-budget-router`
  - `powershell-orchestration-state-machine`

- Workflow
  - `atomic-planner`
  - `atomic-executor`
  - `feature-review`
  - `orchestrator-workflow`

- Specialist support
  - `commit-message-conventions`
  - `pr-authoring`

- Meta
  - `make-skill-template`

## Authoring Rules

1. Put shared rules in one skill only.
2. Have workflow skills reference shared skills instead of copying their text.
3. Put host-specific repo automation rules and their MCP dependency binding in `repo-automation-adapter`.
4. Keep names stable when migrating from the legacy Copilot ecosystem.
5. When an agent needs reusable rules, extract them into a shared skill and keep the agent as a thin wrapper.
6. Put top-level route selection and checkpoint rules in a shared workflow skill rather than duplicating them across prompts or agent personas.
7. When a skill depends on an external MCP server, declare it in that skill's `agents/openai.yaml` instead of repeating the binding in each caller.
