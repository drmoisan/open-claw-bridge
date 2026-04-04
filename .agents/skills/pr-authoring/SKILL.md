---
name: pr-authoring
description: 'Write a GitHub-ready pull request body from the canonical PR-context bundle and its enumerated additional context files. Use when Codex or a subagent must produce an evidence-based PR description with strict verification and auto-close rules.'
---

# PR Authoring

Shared workflow for producing a GitHub-ready pull request body.

## Required Shared Skill

Always use:
- `pr-context-artifacts`

## Role

- Generate a PR body that is feature-first, accurate, and review-ready.
- Base every claim on the canonical PR-context bundle and the explicitly enumerated additional context files listed inside that bundle.
- Keep GitHub issue and PR references precise and non-hallucinatory.

## Allowed Sources

Use only:
- the canonical PR-context summary and appendix defined by `pr-context-artifacts`
- files explicitly listed under `Additional context files` inside that context bundle
- optional user directives supplied with the request

Do not cite or summarize any other repository file.

## Context Priority

Prioritize evidence in this order:

1. `PR Intent`
2. enumerated `Additional context files`
3. embedded feature-doc excerpts such as root cause, constraints, proposed fix, acceptance criteria, story statement, problem, or why
4. comparison range, commits, changed files, and diff statistics
5. referenced issues and PRs
6. issues to autoclose

## Core Objectives

1. Accuracy: every statement must be supported by the allowed sources.
2. Signal: emphasize why and semantic intent, not just changed-file inventory.
3. GitHub correctness: auto-close syntax must be valid and must not invent issue numbers.

## Required Output Shape

Output exactly one fenced code block using the language tag `markdown`.

Inside that code block, include only the PR body with exactly this section order:

- `Suggested title: ...`
- `## Summary`
- `## Why`
- `## What Changed`
- `## Architecture / How It Fits Together`
- `## Verification`
- `### Completed`
- `### Recommended`
- `## Backward Compatibility / Migration Notes`
- `## Risks and Mitigations`
- `## Review Guide`
- `## Follow-ups`
- `## GitHub Auto-close`
- `## Related issues / PRs`

## Section Rules

### Suggested title

- one line only
- lead with the primary outcome, not secondary tooling or docs work

### Summary

- 3 to 7 bullets
- first bullet must state the primary change

### Why

- use feature-doc excerpts and PR Intent as the main source of motivation
- if explicit excerpts are missing, infer conservatively from commits and file themes

### What Changed

Group bullets by theme:
- core behavior or architecture
- tooling, automation, CI, or developer experience
- tests
- docs, templates, or agents

### Verification

- `Completed` may include only evidence-backed verification
- if verification is not proven, state: `Not verified in this PR (no tool outputs recorded in the PR-context summary).`
- `Recommended` must include concrete repo-appropriate commands

### GitHub Auto-close

This section must contain only bullets of the form:
- `Closes #NNN`

Source of truth in order:
1. verified or pending issues to autoclose
2. `PR Intent` author-asserted autoclose issues

If GitHub validation is unavailable or no approved source provides issue numbers, include one bullet:
- `None (...)`

Never use `Related:` in this section.

### Related issues / PRs

- non-closed issues: `Related issue: #NNN`
- PRs in range: `Related PR: #NNN`

## Hard Prohibitions

- Do not invent issue or PR numbers.
- Do not treat PR numbers as issues.
- Do not claim verification unless the context proves it.
- Do not cite sources outside the allowed-source list.
- Do not output commentary outside the single fenced Markdown block.
