---
name: show-my-agent-tree
description: Render the current session's subagent call tree and print it in the assistant reply. Use when the user asks to "show my agent tree" or otherwise wants to see the subagent hierarchy of the running session.
allowed-tools:
  - Read
  - Bash
  - mcp__drm-copilot__render_subagent_tree
---

# Show My Agent Tree

Render the subagent call tree for the current session and print it directly in
the assistant reply. This works identically in normal turns and in `/btw`
side-conversations and needs no VS Code host API.

## Flow

1. **Resolve the session id.** Follow the `identify-session-id` skill to obtain
   the current session id and note which source supplied it.

2. **Call the MCP tool.** Invoke `mcp__drm-copilot__render_subagent_tree` with:
   - `session_id`: the id resolved in step 1.
   - `workspace_root`: an explicit absolute path to the current workspace root
     (do not rely on the tool's default; pass it explicitly).

   On success the tool returns `ok: true`, a `summary` naming the session id
   and resolved transcript path, and a `rendered_tree` string.

3. **Print the tree.** Output the `rendered_tree` value in the assistant reply
   inside a fenced code block, so the hierarchy is legible. Include the
   `summary` line above it for context.

## Error Handling

- If the tool returns `ok: false`, report the `summary` verbatim. An unknown
  session id names the searched directories; a malformed session id names the
  validation rule (`^[0-9A-Za-z-]{8,64}$`).
- If `identify-session-id` cannot resolve an id, report that and do not call
  the tool.
