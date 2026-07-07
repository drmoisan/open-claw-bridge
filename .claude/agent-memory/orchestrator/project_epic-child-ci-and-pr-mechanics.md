---
name: epic-child-ci-and-pr-mechanics
description: Epic-child PRs into the integration branch report NO checks (ci.yml triggers only on main/development PRs) and never autoclose issues; CI-gate via workflow_dispatch at branch head.
metadata:
  type: project
---

Mechanics proven on F14 / PR #121 (2026-07-07), the first true epic-child PR into `epic/openclaw-vision-integration`:

1. **No checks on integration-branch PRs.** `.github/workflows/ci.yml` triggers on `pull_request: branches: [main, development]` only, so a PR based on the epic integration branch reports "no checks reported" forever. Satisfy the S9 CI-green gate by dispatching the same workflow at the branch head: `gh workflow run ci.yml --ref <feature-branch>` then `gh run watch <run-id> --exit-status`; record the run id/URL and confirm the run's `headSha` equals the PR head before `gh pr merge --merge`. Record the accommodation in `ci_gate.note`.
2. **No issue autoclose.** GitHub only processes `Closes #N` for PRs targeting the default branch. `closingIssuesReferences` stays empty and the issue stays OPEN after merge; the issue closes when the epic integration branch merges to main (or is closed manually with evidence at that point).
3. **`collect_pr_context` MCP no-op overwrite in the worktree.** The MCP tool returned `ok:true` with artifact paths but did not rewrite `artifacts/pr_context.summary.txt` (mtime/content unchanged). Verify the bundle's `Head ref` matches the actual head; if stale, regenerate summary+appendix via git in the established banner format (same accommodation feature-review uses) and note the regeneration in the banner.
4. **No `Invoke-CiGateParser.ps1` / Python validator in this repo.** `scripts/orchestration/` does not exist; parse `gh pr checks/run view --json` directly. The PR-creation preflight runs via `Invoke-OrchestratorStatePreflight` in `.claude/lib/orchestrator-state/OrchestratorState.psm1` (portable fallback); it requires `step5..8_status` non-pending — set `step8_status` to `in_progress` before the first `gh pr create` of the branch. Note the MCP orchestrator-state validator rejects `step9_status: "passed"`; use `verified` per [[checkpoint-validator-contract]].

**Why:** the orchestrate SKILL's S9 text assumes required PR checks exist; on epic children that assumption fails silently ("no checks reported") and would stall or, worse, allow an unobserved merge.

**How to apply:** for every remaining epic child (F15-F20), run step 1's dispatch-and-watch sequence as the S9 gate and expect steps 2-4 verbatim. Related: [[openclaw-vision-program-status]], [[openclaw-delivery-loop]].
