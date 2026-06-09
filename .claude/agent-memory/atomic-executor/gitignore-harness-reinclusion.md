---
name: gitignore-harness-reinclusion
description: How the repo .gitignore excludes the agent harness and the exact negation forms that re-include .github/* subtrees and .claude/ for tracking
metadata:
  type: project
---

The repo `.gitignore` excludes the agent harness from version control: `.github/*` (with `!.github/workflows/{ci.yml,publish.yml}` whitelist + `.github/workflows/*` re-ignore) at ~L76-80, and a bare `.claude` line at ~L83. `artifacts/` is ignored at L22.

**Why:** The harness was copied from another repo without adapting `.gitignore`, so the bulk of harness edits lived in untracked files invisible to PRs and git-diff-based review (Issue #66 scope extension, Option 1A).

**How to apply (re-inclusion mechanics, verified 2026-06-08):**
- For `.github/*`: the exclusion is at the immediate-child level (no recursive `.github/**`). A directory-form negation alone — `!.github/agents/`, `!.github/instructions/`, `!.github/prompts/`, `!.github/skills/` — is sufficient to un-ignore deep files (e.g. `.github/agents/orchestrator.agent.md`). `git check-ignore` confirms NOT-IGNORED and `git add -n` would stage them. The standard "cannot re-include under an excluded parent" caveat does NOT bite here because the parent exclusion is `.github/*`, not `.github/**`.
- For `.claude`: the bare `.claude` line is REMOVED (not negated), so there is no excluded parent; `.claude/` becomes tracked. Adding `!.claude/` is belt-and-suspenders.
- `.github/workflows/*`, `artifacts/` (L22), and a new `.claude/settings.local.json` ignore remain ignored after these edits — confirmed by simulation.
