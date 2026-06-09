---
name: commit-message
description: Generate a conventional commit message from staged Git changes following the repository's commit message conventions.
allowed-tools:
  - Read
  - "Bash(git log *)"
  - "Bash(git diff *)"
---

# Commit Message Skill

Generate a single conventional commit message from staged Git changes or a provided commit-context artifact.

## Inputs

- Staged Git changes are the primary source of truth.
- Accept a commit-context artifact file when explicitly provided.
- Ignore unstaged changes unless included in the provided context.
- If no staged changes and no context artifact exist, stop and report that there is no in-scope commit content.

## Context Gathering

1. Use the supplied commit-context artifact first when present.
2. Otherwise inspect the staged diff and commit history only:
   - `git diff --cached --stat`
   - `git diff --cached`
   - `git diff --cached --name-only`
   - `git log --oneline -n 20`
3. Do not infer intent from unstaged files or unrelated commit history.

## Classification

Follow Conventional Commits semantics. Choose exactly one primary type from: `feat`, `fix`, `docs`, `test`, `refactor`, `ci`, `chore`.

- Use the dominant intent of the staged changes.
- Prefer `docs`, `test`, or `fix` over `chore` when classification is ambiguous.
- Add a scope only when it materially improves clarity. Keep scopes concrete and subsystem-oriented.

## Message Format

Output exactly one fenced code block with language tag `text` containing the commit message:

```text
type(optional-scope): concise imperative summary

- Bullet describing meaningful change #1
- Bullet describing meaningful change #2

Refs: #<issue>
```

Header rules: imperative mood, 72 characters or fewer, no trailing period, no filler words.

Body rules: use bullets only when they add signal beyond the header. Avoid implementation trivia.

Reference rules: include `Refs:` footer only when issue or PR references are present in the context. Do not invent references.

## Prohibitions

- No emojis
- No commentary outside the fenced code block
- No Markdown inside the commit body beyond plain-text hyphen bullets
- No references to "this commit"
- No speculation about unstaged work
- No multiple alternative commit messages
