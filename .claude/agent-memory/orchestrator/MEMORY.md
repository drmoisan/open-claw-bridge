# Orchestrator Memory Index

- [Surface consequential decisions](feedback_surface-consequential-decisions.md) — get operator confirmation on irreversible harness/scope/CI-gate forks; batch low-risk defaults.
- [Harness governance](project_harness-governance.md) — harness is now version-controlled (Issue #66/PR #68); `.claude/rules/*` is canonical; AGENTS.md + .github/instructions match it.
- [Core agent = namespace not project](project_core-agent-namespace-not-project.md) — new logic for OpenClaw.Core folds into a namespace, not a new project (arch rule 6); enforce with namespace NetArchTest.
- [Checkpoint validator contract](project_checkpoint-validator-contract.md) — orchestrator-state validator requires hyphen keys, stepN enum (verified not complete), and delegation_receipts as a LIST; differs from prompt.
- [csharpier local tool manifest broken](project_csharpier-local-tool-manifest-broken.md) — local dotnet-tools csharpier entry misconfigured; use global csharpier with format/check subcommands.
