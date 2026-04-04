---
name: commit-message-conventions
description: Generate a single high-signal conventional commit message from staged Git changes or an explicit commit-context artifact. Use when Codex or a subagent must classify dominant change intent, choose a precise commit type and optional scope, and emit an audit-quality message that is immediately usable with `git commit`.
---

# Commit Message Conventions

Shared commit-message workflow for repository agents and prompts.

## Inputs And Scope

- Treat staged changes as the primary source of truth.
- Accept an explicitly provided commit-context file or pasted commit context when one is supplied.
- Ignore unstaged changes unless they are explicitly included in the provided context.
- If there are no staged changes and no provided context artifact, stop and report that there is no in-scope commit content to summarize.

## Context Gathering Order

1. Use the supplied commit-context artifact first when one is present.
2. Otherwise inspect the local staged state with non-destructive Git commands such as:
   - `git status --short`
   - `git diff --cached --stat`
   - `git diff --cached`
   - `git diff --cached --name-only`
3. Do not infer intent from unstaged files or unrelated commit history.

## Commit Classification Rules

- Follow Conventional Commits semantics.
- Choose exactly one primary type from:
  - `feat`
  - `fix`
  - `docs`
  - `test`
  - `refactor`
  - `ci`
  - `chore`
- Use the dominant intent of the staged changes.
- Prefer `docs`, `test`, or `fix` over `chore` when classification is ambiguous.
- Add a scope only when it materially improves clarity.
- Keep scopes concrete and subsystem-oriented. Avoid vague scopes such as `misc`, `general`, or `updates`.

## Commit Message Format

- Output exactly one fenced code block with the language tag `text`.
- The code block must contain only the commit message.
- Use this preferred structure inside the code block:

```text
(type(optional-scope)): concise imperative summary

- Bullet describing meaningful change #1
- Bullet describing meaningful change #2
- Bullet describing meaningful change #3 only when justified

Refs: #<issue>, #<issue>
```

Header rules:
- Use imperative mood.
- Prefer 72 characters or fewer.
- Do not end the header with a period.
- Avoid filler words such as `various`, `some`, or `updates`.

Body rules:
- Use bullets only when they add real signal.
- Make each bullet add information beyond the header.
- Avoid implementation trivia unless it materially affects understanding.

Reference rules:
- Include a `Refs:` footer only when issue or PR references are explicitly present or clearly implied by the provided context.
- Do not invent issue or PR references.

## Interpretation Rules

1. Identify the primary architectural or behavioral intent first.
2. Treat incidental formatting or mechanical edits as secondary unless they dominate the staged diff.
3. Keep the commit to one coherent narrative.
4. Distinguish clearly between planning, documentation, investigation, and implementation.
5. Do not imply a fix was implemented when the staged changes only document or plan the work.

## Hard Prohibitions

- No emojis
- No commentary outside the required fenced code block
- No Markdown inside the commit body beyond plain-text hyphen bullets
- No references to `this commit`
- No speculation about unstaged work
- No multiple alternative commit messages

## Quality Bar

- The message must be immediately usable with `git commit`.
- Optimize for clarity, traceability, and long-term audit value.
- If multiple plausible narratives exist, choose the one that best reflects the dominant staged intent.
