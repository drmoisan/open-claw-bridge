---
name: gitignore-blocks-new-workflows
description: .gitignore ignores all .github/workflows/*.yml except explicitly re-included files; a new reusable workflow needs a `!` re-include line or it is silently not committed.
metadata:
  type: project
---

`.gitignore` (around lines 76-84) does `.github/*` then `.github/workflows/*` then re-includes ONLY specific files with `!` (originally just `ci.yml` and `publish.yml`). Any NEW `.github/workflows/*.yml` file is therefore ignored and will NOT be staged/committed by `git add -A` — silently. `git status` won't list it; `git check-ignore -v <file>` confirms the block.

This bites the repo's own convention (per `.claude/skills/orchestrate/SKILL.md`): every new CI gate ships as a reusable `_<name>.yml`. When you add one, you MUST also add `!.github/workflows/_<name>.yml` to `.gitignore`, or `ci.yml`'s `uses: ./.github/workflows/_<name>.yml` reference will dangle (the callee file isn't in the repo) and CI breaks.

Detection: the `commit-message` agent flagged this on F16/#125 by noting the file was excluded from the staged diff. Always verify a new workflow file is tracked (`git ls-files .github/workflows/<file>`) before committing.

`.gitignore` is a normal repo config file, NOT a policy doc under `.claude/rules/` — editing it to re-include a legitimately-needed workflow is appropriate.

**How to apply:** Whenever a feature adds a `.github/workflows/*.yml`, add the matching `!` re-include line to `.gitignore` in the same change and confirm the file is tracked. Related: [[epic-child-ci-and-merge]], [[harness-governance]].
