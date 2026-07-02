---
name: openclaw-delivery-loop
description: Validated per-feature delivery loop for open-claw-bridge (worked for PRs 96-102); hook constraints, promotion mechanics, pr-author receipt recipe
metadata:
  type: project
---

Validated per-feature delivery loop in open-claw-bridge (proven on F1-F6 / PRs #96, #97, #98, #100, #102 during the vision-program session of 2026-07-01/02):

1. `git fetch origin main && git checkout -b <type>/<short-name>-<issue> origin/main` (worktree's local `main` is stale — always use `origin/main` for branch base AND merge-base; executors/reviewers must be told this explicitly).
2. Existing issue: `new_active_feature_folder` directly. New feature: `new_potential_entry` -> fill the potential file (Read scaffold first; Write tool requires a read) -> `potential_to_issue` (creates the GitHub issue) -> `new_active_feature_folder`.
3. Always `full-feature`/`full-bug` mode with a `prd-feature` delegation producing issue.md + spec.md + user-story.md. **Why:** `enforce-prd-feature-before-planner` and `enforce-feature-folder-order` hooks require spec.md AND user-story.md unconditionally (no minor-audit exemption), despite the promotion-lifecycle skill saying minor-audit folders must omit them. For bugs, user-story.md is justified in-file as a planner-gate requirement.
4. atomic-planner writes into the scaffolded `plan.<ts>.md` (in place); validate with `validate_orchestration_artifacts(plan)`; atomic-executor preflight (`DIRECTIVE: PREFLIGHT VALIDATION ONLY`) -> revise via planner in place if CHANGES REQUIRED -> execute (`DIRECTIVE: FULL EXECUTION AUTHORIZED`, "do not commit").
5. Orchestrator commits (conventional message + Refs + trailers), refreshes `collect_pr_context(base=main)`, delegates feature-review with merge-base/folder/context/AC-source only (no scope language). Blocking findings -> R1-R5 remediation loop with `remediation_loop.cycles[]` checkpoint shape.
6. PR: no pr-author subagent exists — the orchestrator itself follows the pr-author SKILL: refresh collect_pr_context, Write `artifacts/pr_body_<N>.md`, then receipt via `pwsh -NoProfile -Command '...Get-FileHash...'` (single-quote the whole -Command for bash), then `gh pr create --body-file artifacts/pr_body_<N>.md`. Hook verifies receipt.
7. CI poll: `gh pr checks <n> --json bucket,name,state` in a sleep-20 loop; merge with `gh pr merge <n> --merge` (merge commit only); auto-close needs `- Closes #N` bullets (multiple allowed).

**How to apply:** reuse this loop verbatim for the remaining program features (F7-F20 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`); resume position lives in `artifacts/orchestration/orchestrator-state.json` (gitignored, local to the worktree). Related: [[openclaw-vision-program-status]].
