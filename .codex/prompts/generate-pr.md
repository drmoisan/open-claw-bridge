---
description: Generate a GitHub-ready PR body from the canonical PR-context bundle by spawning pr-author
argument-hint: Optional notes or PR intent edits to apply while writing the PR body
---

Spawn `pr-author` to generate a GitHub-ready pull request body.

Use only:
- the canonical PR-context bundle
- the additional context files enumerated inside that bundle
- any explicit user directives supplied with this prompt invocation

If the PR-context bundle is missing or stale, refresh it using the canonical mechanism before generating the PR body.

Return exactly one fenced `markdown` code block containing only the pull request message.
