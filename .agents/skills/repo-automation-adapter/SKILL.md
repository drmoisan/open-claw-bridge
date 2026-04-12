---
name: repo-automation-adapter
description: 'Centralize host-surface and repo-automation differences for Codex. Use when a migrated workflow previously depended on GitHub Copilot or VS Code extension commands and needs a single Codex-compatible execution or fallback rule.'
---

# Repo Automation Adapter

Use this skill to keep host-specific workflow translation in one place.

## When to Use This Skill

Use this skill when:
- a migrated skill previously depended on `drmCopilotExtension.*` commands,
- a workflow needs PR-context collection, issue promotion, or feature-folder creation,
- the same fallback behavior would otherwise be repeated across multiple skills.

## Canonical Rule

Do not encode host-specific execution details in multiple workflow skills. Put them here and have the calling skills reference this skill.

## Published Codex Automation Surface

The canonical Codex automation dependency for this repo is the published MCP server:
- `drmCopilotExtension`

Downstream Codex skills should depend on the MCP server name `drmCopilotExtension`, not on raw VS Code command IDs.

Declare that dependency once on this skill via `agents/openai.yaml`.

## Codex Capability Model

Codex in this repo can reliably use:
- repository files,
- shell commands,
- git,
- local scripts that already exist in the repository,
- configured MCP tools when available.

Codex in this repo should not assume direct access to:
- `vscode/runCommand`,
- `drmCopilotExtension.*` command execution,
- GitHub issue or PR mutation unless an explicit connector or script is available.

## Published MCP Tool Surface

Prefer these semantic MCP tools when the server is configured:

- `collect_commit_context`
- `collect_pr_context`
- `push_down_copilot_customizations`
- `push_down_codex_and_agents_customizations`
- `new_potential_bug_entry`
- `new_potential_entry`
- `potential_to_issue`
- `new_active_feature_folder`
- `resolve_execute_hard_lock_prompt`

Legacy VS Code command IDs remain historical source material only:

- `drmCopilotExtension.collectCommitContext`
- `drmCopilotExtension.collectPrContext`
- `drmCopilotExtension.pushDownCopilotCustomizations`
- `drmCopilotExtension.pushDownCodexAndAgentsCustomizations`
- `drmCopilotExtension.newPotentialBugEntry`
- `drmCopilotExtension.newPotentialEntry`
- `drmCopilotExtension.potentialToIssue`
- `drmCopilotExtension.newActiveFeatureFolder`
- `drmCopilotExtension.resolveExecuteHardLockPrompt`

## Adapter Preconditions

Before treating the MCP path as available, assume these prerequisites:

- the Codex client is configured with MCP server name `drmCopilotExtension`
- the published extension or bridge is installed and built
- an open workspace folder exists for workspace-targeted operations
- `python` is on `PATH`
- `pwsh` is preferred, with Windows PowerShell fallback when applicable

## Execution Order

For any host-specific workflow step:

1. Prefer the published `drmCopilotExtension` MCP tool when it covers the requested operation.
2. If the MCP server is unavailable, determine whether a deterministic git or filesystem fallback is sufficient.
3. If a deterministic fallback is sufficient, use it and record that the result is a fallback artifact rather than a canonical tool-produced artifact.
4. If no MCP path or safe fallback exists, stop and report the missing automation dependency instead of inventing behavior.

## Current Adapter Guidance

### PR context collection

- Preferred: call tool `collect_pr_context` on MCP server `drmCopilotExtension`.
- When the caller already resolved a base branch, pass that base explicitly.
- Current fallback: use deterministic git commands to reconstruct equivalent context when review workflows only need base/head, merge-base, commits, and changed files.
- When using fallback, record the provenance in the generated review artifact.

### Commit context collection

- Preferred: call tool `collect_commit_context` on MCP server `drmCopilotExtension`.
- If the MCP server is unavailable and the workflow only needs staged-diff summary, use non-destructive git inspection as fallback and record that provenance.

### Feature promotion and active feature folder creation

- Preferred MCP tools:
  - `new_potential_entry`
  - `new_potential_bug_entry`
  - `potential_to_issue`
  - `new_active_feature_folder`
- Current rule: use the MCP tools as the canonical path.
- Execute these lifecycle operations as one ordered chain:
  - create potential entry
  - promote with `potential_to_issue`
  - capture numeric issue number from promotion output
  - create or check out `${promotion-type}/${short-name}-${issue-num}`
  - create active feature folder with `new_active_feature_folder`
- `new_active_feature_folder` is not an allowed bootstrap substitute for missing promotion state.
- If `${issue-num}` is missing, non-numeric, or placeholder text, stop instead of continuing.
- If the MCP server is unavailable, surface a precise dependency gap unless the caller explicitly requests a best-effort local-only fallback.
- Do not synthesize GitHub issue state, active-folder scaffolding, or placeholder lifecycle variables unless the user explicitly requests a best-effort local-only fallback.
- When a local-only fallback is explicitly approved, preserve the same ordered lifecycle and verification gates; do not skip directly to folder creation.

### Customization publishing and hard-lock resolution

- Preferred MCP tools:
  - `push_down_copilot_customizations`
  - `push_down_codex_and_agents_customizations`
  - `resolve_execute_hard_lock_prompt`
- If the MCP server is unavailable, stop unless the caller explicitly provides an approved alternate path.

## Output Requirements

When this skill is used, the calling workflow should report:
- which operation required host adaptation,
- which direct adapter or fallback path was selected,
- whether the result is canonical or fallback-only,
- what dependency is missing when the step is blocked.
