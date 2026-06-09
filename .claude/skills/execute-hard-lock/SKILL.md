---
name: execute-hard-lock
description: Place the session in atomic execution mode bound to a specific plan-of-record. Resolves the hard-lock prompt via the drm-copilot MCP tool, then delegates to the atomic-executor subagent with the resolved text. Use when a caller provides ${plan-path} and ${work-mode} and requires strict plan-following behavior.
allowed-tools:
  - mcp__drm-copilot__resolve_execute_hard_lock_prompt
  - Read
---

# Execute Hard Lock

Thin wrapper that resolves the hard-lock prompt via the drm-copilot MCP tool and hands the resolved text to the `atomic-executor` subagent as kickoff directives. The resolved prompt is the authoritative instruction set for the session; this skill does not duplicate its contents.

## When to Use This Skill

Use this skill when:

- The caller provides an explicit plan file path (`${plan-path}`) and a selected work mode (`${work-mode}`).
- Strict plan-following behavior is required (no replanning, no reordering, no bucket tasks).
- The drm-copilot MCP server is registered and reachable.

## Inputs

Required:

- `plan-path` — absolute or repo-relative path to the plan-of-record markdown file.
- `work-mode` — one of `minor-audit`, `full-feature`, `full-bug`. Legacy value `full` normalizes to `full-feature`. Missing or malformed values fail closed to `full-feature`.

## Invocation Flow

### 1. Resolve the Hard-Lock Prompt via MCP

Call the extension's resolver as the first action:

- Tool: `mcp__drm-copilot__resolve_execute_hard_lock_prompt`
- Parameters:
  - `target` (required): the plan-of-record path (`${plan-path}`).
  - `workspace_root` (optional): the workspace root. Omit to default to the current working directory.

On success, the response contains an `artifacts` array whose first entry is the absolute path of a file containing the resolved hard-lock prompt (produced by the extension passing `--output` and `--quiet` to the bundled Python resolver).

### 2. Read the Resolved Prompt

Use the `Read` tool on the path returned in `artifacts[0]`. Treat the file contents as the authoritative hard-lock instruction set for this session.

### 3. Delegate to atomic-executor

Invoke the `atomic-executor` subagent via the Agent tool. Pass the resolved prompt text as the subagent's kickoff directive, followed by `${plan-path}` and `${work-mode}` as session context.

The resolved prompt itself already instructs the subagent to perform the mandatory read-proof (`git rev-parse HEAD`, SHA-256 of the plan file, unchecked-task count), preflight validation, and `READY TO BEGIN FROM [P#-T#]` handshake before executing any task. This skill does not reissue those instructions locally — doing so would risk divergence from the canonical template.

## Abort Conditions

Stop immediately and report `BLOCKED: execute-hard-lock <cause>` in any of these cases. Do not reconstruct the hard-lock prompt from any other source and do not delegate to `atomic-executor` without a successful read in step 2.

- MCP tool is not available or not permitted.
- MCP response has `ok: false` or is otherwise malformed.
- MCP response omits `artifacts` or `artifacts[0]`.
- `Read` on the artifact path fails (file missing, unreadable, empty).

## Equivalent Entry Points (Reference)

The three entry points below all produce the same resolved hard-lock prompt for a given plan path. This skill always uses the MCP form:

- MCP (used by this skill): `mcp__drm-copilot__resolve_execute_hard_lock_prompt` with `target=<plan-path>`. The extension passes `--output artifacts/hard_lock_prompt.txt` and `--quiet` to the bundled Python resolver.
- VS Code command: `@command:drmCopilotExtension.resolveExecuteHardLockPrompt` (interactive; writes to stdout + clipboard, no file artifact).

## Delegation Contract

The `atomic-executor` subagent at [../../agents/atomic-executor.md](../../agents/atomic-executor.md) enforces the persistent execution-mode behaviors (anti-replanning, preflight-only blocking, persistence across turns, resume) and preloads the shared skills that support them:

- `policy-compliance-order` — mandatory policy reading order.
- `atomic-plan-contract` — plan format, Phase 0 requirements, preflight signals, validator gate, and mode-specific plan gates.
- `acceptance-criteria-tracking` — AC check-off protocol and status summary format.
- `evidence-and-timestamp-conventions` — baseline and final-QC artifact paths and required fields.

## Prohibitions

- Do not proceed without a successful MCP resolver response AND a successful `Read` of the artifact.
- Do not reconstruct the hard-lock contract from any other source (not from this file nor from prior session memory).
- Do not modify the resolved prompt text before passing it to `atomic-executor`.
- Do not replan, reorder, or add tasks at this layer — the subagent owns that contract.
- Do not create or read secrets unless explicitly authorized.
