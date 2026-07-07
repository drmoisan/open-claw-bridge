---
name: feedback-worktree-write-path
description: Write tool must target the worktree copy of a file, not the shared-checkout path, when running inside an isolated git worktree.
metadata:
  type: feedback
---

When this agent runs inside a git worktree under `.../.claude/worktrees/<agent-id>/`, the `Write` tool rejects paths that resolve to the shared/main checkout (e.g., `C:\Users\DanMoisan\repos\open-claw-bridge\docs\...` when the working directory is the worktree) with the error "This agent is isolated in the worktree ... Edit the worktree copy of this file instead of the shared-checkout path."

**Why:** The worktree sandbox enforces that all writes land under the worktree's own directory tree, even though `Read`/`Glob` against the shared-checkout-style absolute path (without the worktree segment) succeed and return correct file contents (git worktrees share object storage, so reads resolve either way).

**How to apply:** Before calling `Write` on a computed absolute path derived from a feature/task description, prefix it with the worktree root (from the `Working directory` field in the environment block, e.g., `C:\Users\DanMoisan\repos\open-claw-bridge\.claude\worktrees\<agent-id>\`) rather than the bare repo path. If a `Write` call errors with this isolation message, retry immediately with the worktree-prefixed path — no need to re-derive content.
