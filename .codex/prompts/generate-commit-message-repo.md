---
description: Generate a conventional commit message for the current repository by spawning commit-steward
---

Spawn `commit-steward` to generate a single audit-quality conventional commit message for the current repository.

Use an explicitly supplied commit-context artifact as the primary input when one is provided.
If no explicit commit-context artifact is provided, prefer `artifacts/commit_context.txt` when that file exists in the workspace.
If no commit-context artifact is available, have `commit-steward` inspect staged changes directly and scope the message to staged changes only.

Return exactly one fenced `text` code block containing only the commit message.
