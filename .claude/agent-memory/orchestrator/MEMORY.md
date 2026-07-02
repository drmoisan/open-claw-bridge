# Orchestrator Memory Index

- [OpenClaw delivery loop](project_openclaw-delivery-loop.md) — validated per-feature loop (PRs 96-102): hooks force full-feature docs; pr-author receipt recipe; origin/main for merge-base.
- [OpenClaw vision program status](project_openclaw-vision-program-status.md) — 4-epic/20-feature program; F1-F6 done as of 2026-07-02; queue + resume instructions.
- [Surface consequential decisions](feedback_surface-consequential-decisions.md) — confirm only NOVEL irreversible forks; policy-defined steps (PR open, commits, CI monitoring) run autonomously.
- [Harness governance](project_harness-governance.md) — harness is now version-controlled (Issue #66/PR #68); `.claude/rules/*` is canonical; AGENTS.md + .github/instructions match it.
- [Core agent = namespace not project](project_core-agent-namespace-not-project.md) — new logic for OpenClaw.Core folds into a namespace, not a new project (arch rule 6); enforce with namespace NetArchTest.
- [Checkpoint validator contract](project_checkpoint-validator-contract.md) — orchestrator-state validator requires hyphen keys, stepN enum (verified not complete), and delegation_receipts as a LIST; differs from prompt.
- [csharpier local tool manifest broken](project_csharpier-local-tool-manifest-broken.md) — local dotnet-tools csharpier entry misconfigured; use global csharpier with format/check subcommands.
- [HostAdapter scheduling = Design A](project_hostadapter-scheduling-design-a.md) — track #76->#74->#75 uses Graph-shaped HostAdapter endpoints (not Core-cache compute); #75 should follow the same pattern.
- [Unify COM vs Modern behind adapter](feedback_unify-com-vs-modern-behind-adapter.md) — model-specific field resolution goes behind a unifying interface + data-type adapter so only the adapter swaps (COM->Modern).
- [pr-author skill](reference_pr-author-skill.md) — author PR bodies via the pr-author SKILL (not manual, not a subagent); refresh collect_pr_context bundle first.
- [Clean worktree before ready](feedback_clean-worktree-before-ready.md) — `git status --porcelain` must be empty before declaring a PR ready to merge; commit memory/docs, don't leave them.
- [Autonomous finish sequence](feedback_autonomous-finish-sequence.md) — after a passing exit gate, commit+push all, open PR, remediate CI, commit memories, ensure green CI — without asking. Overrides "commit only when asked".
- [.env files denied to tools](project_env-files-permission-denied-to-tools.md) — `.env`/`.env.example` blocked from Read/Write/Edit/Bash cat; inspect via git diff/show, have operator edit.
- [Merge-commit policy](feedback_merge-commit-policy.md) — every PR merges to main with a MERGE COMMIT (`gh pr merge --merge`); never squash/rebase; never commit directly to main.
