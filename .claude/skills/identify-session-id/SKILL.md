---
name: identify-session-id
description: Resolve the current Claude Code session id (the root transcript filename stem) without human input, using an ordered fallback chain, and report which source supplied it. Use before any workflow that needs the running session's id (for example show-my-agent-tree).
allowed-tools:
  - Read
  - Bash
---

# Identify Session Id

Resolve the current session's id — the root transcript filename stem under
`~/.claude/projects/<encoded-workspace>/` — without asking the human. Try the
sources in order and stop at the first that yields a non-empty id. Always
report which source was used.

## Resolution Order

1. **Environment variable (primary).** Read `CLAUDE_SESSION_ID` from the
   environment with a single command, e.g.:
   - Bash: `printf '%s' "$CLAUDE_SESSION_ID"`
   - pwsh: `pwsh -NoProfile -Command 'Write-Output $env:CLAUDE_SESSION_ID'`

   This variable is provisioned by the SessionStart hook
   `.claude/hooks/persist-session-id.ps1` through the `CLAUDE_ENV_FILE`
   channel. If it is non-empty, use it and report source `env:CLAUDE_SESSION_ID`.

2. **State file (secondary).** Read `.claude/state/current-session-id`
   (written by the same hook when `CLAUDE_ENV_FILE` is unset). If it exists and
   is non-empty, use its trimmed contents and report source
   `.claude/state/current-session-id`.

3. **Newest transcript (tertiary heuristic).** List the root `*.jsonl` files
   directly under `~/.claude/projects/<encodeWorkspacePath(cwd)>/` (the encoding
   replaces every path separator and `:` with `-`), pick the one with the
   newest modification time, and use its filename stem (without `.jsonl`).
   Report source `newest-mtime transcript` and note that this heuristic can
   pick the wrong sibling only when multiple concurrent sessions share one
   workspace path.

## Output

Report the resolved session id and the source that supplied it, for example:
`session_id = <id> (source: env:CLAUDE_SESSION_ID)`. If every source is empty,
state that the session id could not be resolved and which sources were checked.
