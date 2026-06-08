---
name: pr-author
description: Write a GitHub-ready pull request body from the canonical PR-context bundle with strict verification and auto-close rules.
allowed-tools:
  - Read
  - "Bash(git log *)"
---

# PR Author Skill

Produce a GitHub-ready Pull Request description from the standard PR context files. The pull request must be in a copy / paste block so that it can be copied easily to the PR extension.

## Inputs

- `artifacts/pr_context.summary.txt` — PR context summary (primary)
- `artifacts/pr_context.appendix.txt` — PR context appendix (full baseline diff, commits, changed files)
- Optional user directives

Only reference files that are listed under "Additional context files" in the context bundle. Do not cite or summarize files outside that enumeration.

## Output Requirements

1. Output only the PR text body in GitHub-flavored Markdown.
2. Use clear headings, consistent structure, and concise bullets.
3. Do not invent tests or results; if not in context, state "Not verified in this PR."
4. If scope is large, include a Review Guide with suggested review order.
5. The output is a PR body artifact or PR-ready body text only.

## PR Body Structure

Use these sections in this order:

1. **Suggested title** — crisp, prioritizing the primary outcome
2. **Summary** — 3–7 bullets, most important first; lead with the primary product/feature/architecture change
3. **Why** — motivation, constraints, root cause; rely on embedded feature-doc excerpts and PR Intent fields only
4. **What Changed** — grouped by theme (core feature, tooling/CI, tests, docs/templates)
5. **Architecture / How It Fits Together** — short wiring description with components, entry points, control flow
6. **Verification** — "Completed" (from context) and "Recommended" (commands to run)
7. **Backward Compatibility / Migration Notes** — breaking changes, removals, renamed paths
8. **Risks and Mitigations** — realistic risks with mitigations and rollback notes
9. **Review Guide** — suggested order, noisy mechanical moves, large diffs
10. **Follow-ups** — known TODOs, deferred cleanup, next PRs
11. **GitHub Auto-close** — `- Closes #NNN` only from verified autoclose lists; do not invent issue numbers

## Issue/PR Reference Rules

- Only mention an issue/PR number if it appears verbatim in the provided context.
- Do not treat PR numbers as issues.
- Auto-close bullets must use exactly `- Closes #NNN` format, sourced only from "Issues to autoclose" or "Author-asserted autoclose issues."
- If GitHub validation is unavailable/unverified, do not emit `Closes`; use the fallback `None` bullet.
