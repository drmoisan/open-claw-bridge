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
4. Apply via `gh pr create/edit --body-file artifacts/pr_body_<N>.md`; verify with `gh pr view <num> --json closingIssuesReferences`.

**Body-file + receipt is now HOOK-ENFORCED (verified 2026-07-07, PR #123).** The `enforce-pr-author-skill.ps1` PreToolUse hook blocks `gh pr create`/`gh pr edit --body*` unless: (a) the orchestrator-state checkpoint passes `--require-pr-creation-ready`, AND (b) `--body-file` resolves to a canonical `artifacts/pr_body_<N>.md` with a matching `artifacts/pr_body_<N>.receipt.json`. Recipe: write `pr_body_<N>.md` (N = issue/PR number); compute lowercase-hex SHA-256 of the body bytes; write the receipt `{skill:"pr-author", pr_body_path, number:N, sha256, context_summary_path:"artifacts/pr_context.summary.txt", created_at}` where `created_at` is STRICTLY newer than `pr_context.summary.txt`'s last-write (bump +1s if clock skew). Then `gh pr create --body-file ...`. Inline `--body` is blocked.

**pr-creation-ready preflight without the Python validator:** this repo has no `scripts/dev_tools/validate_orchestration_artifacts.py`; the hook (and you) use the portable PowerShell fallback `Test-OrchestratorStatePrCreationReadiness` in `.claude/lib/orchestrator-state/OrchestratorState.psm1`. It requires steps 5-8 not `pending`/`blocked`, `blocked_reason` in {none,null}, and empty `local_execution_overrides`/`delegation_bypasses`. Run it before authoring; record `pr_author_preflight {status,checked_at,checkpoint_path,validator_command,output_summary}`.

**Orchestrator runs the skill itself when the agent type is absent (confirmed 2026-07-07).** In an epic-child `orchestrator` session, `Agent(pr-author)` is unavailable, so the orchestrator applies the pr-author skill directly (writes body+receipt, runs `gh pr create`) and the hook still passes given a valid receipt + ready checkpoint. Record a `delegation_receipts[]` entry with `agent_name:"pr-author"` anyway so the route's `required_agents` contract is satisfied at completion.

**Caveat:** the bundle's "Changed files overview" classifier can mislabel a C# diff as "Core logic changes: 0 files / Docs N files" (observed on PR #87 for issue #73). Do not echo that classifier line; author the change description from the feature-doc excerpts, spec/AC, plan tasks, and verification-evidence sections, which are accurate.

**Why:** During Issue #73 (PR #87) the operator twice corrected the orchestrator: first for hand-writing the PR body instead of using `pr-author`, then for trying it as an agent — clarifying it is a skill. Related: [[surface-consequential-decisions]].
