---
name: pr-author-skill
description: PR descriptions are authored via the pr-author SKILL (not manually, not a subagent); it consumes the collect_pr_context bundle.
metadata:
  type: reference
---

When opening or updating a pull request body, author it with the **`pr-author` skill** at `.claude/skills/pr-author/SKILL.md`. It is a **skill, not a subagent** — there is no `pr-author` agent type (delegating to `subagent_type: pr-author` errors with "Agent type not found"). Apply the skill directly in the main thread.

**Procedure:**
1. Refresh the canonical PR-context bundle first: `mcp__drm-copilot__collect_pr_context` with `base` set to the PR base (e.g. `main`). It writes `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`.
2. Author the body per the skill's 11-section structure (Suggested title, Summary, Why, What Changed, Architecture, Verification, Backward Compatibility, Risks, Review Guide, Follow-ups, GitHub Auto-close).
3. Reference/auto-close rules: only cite files enumerated under "Additional context files"; emit `- Closes #NNN` only from the bundle's verified/pending "Issues to autoclose" list (do not invent issue numbers or treat PR numbers as issues).
4. Apply via `gh pr edit <num> --title ... --body-file ...`; verify with `gh pr view <num> --json closingIssuesReferences`.

**Caveat:** the bundle's "Changed files overview" classifier can mislabel a C# diff as "Core logic changes: 0 files / Docs N files" (observed on PR #87 for issue #73). Do not echo that classifier line; author the change description from the feature-doc excerpts, spec/AC, plan tasks, and verification-evidence sections, which are accurate.

**Why:** During Issue #73 (PR #87) the operator twice corrected the orchestrator: first for hand-writing the PR body instead of using `pr-author`, then for trying it as an agent — clarifying it is a skill. Related: [[surface-consequential-decisions]].
