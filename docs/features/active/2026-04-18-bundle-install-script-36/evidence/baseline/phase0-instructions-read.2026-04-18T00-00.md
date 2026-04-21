# Phase 0 — Instructions Read Evidence

Timestamp: 2026-04-18T00-00
Policy Order:
1. `AGENTS.md` (repo-root standing instructions)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/powershell.md`
5. `.claude/rules/tonality.md`

## Files Read

- [P0-T1] `AGENTS.md` — Repo-root standing instructions. Generated from `.github/instructions/*.instructions.md`. Declares required order: Copilot instructions -> general policies -> language-specific -> CI policies.
- [P0-T2] `.claude/rules/general-code-change.md` — Cross-language code change policy: simplicity first, reusability, extensibility, separation of concerns; mandatory format -> lint -> type-check -> test loop; 500-line file cap.
- [P0-T3] `.claude/rules/general-unit-test.md` — Cross-language unit test policy: independence, isolation, fast, deterministic, readable; repo-wide coverage >= 80%, new-code >= 90%; no external services or temp files.
- [P0-T4] `.claude/rules/powershell.md` — PowerShell-specific standards: PoshQC format/analyze/test via MCP; PS 7+ compatibility; advanced functions with `CmdletBinding`; `SupportsShouldProcess` for state changes; approved verbs; <= 500 lines/file; Pester v5; mirror test structure; mock sparingly; no temp files.
- [P0-T5] `.claude/rules/tonality.md` — Required professional tone; no jokes, hyperbole, or decorative metaphors; evidence-first wording.
- [P0-T6] `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` — Confirmed. Definition of Done enumerates 11 items directly on the DoD list plus 11 additional Seeded Test Conditions (22 reconciliation items total). Planner Decisions Q1 (90s/3s), Q2 (admin precheck on `-AllowUnsigned`), Q3 (Path D in runbook) all recorded in the plan.
- [P0-T7] `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` — Confirmed. Acceptance Criteria section contains 14 checkboxes.
- [P0-T8] `docs/features/active/2026-04-18-bundle-install-script-36/issue.md` — Confirmed. Work Mode: full-feature. Proposed behavior and 14 acceptance criteria align with spec.md and user-story.md.
- [P0-T9] `artifacts/research/2026-04-18-bundle-install-script.md` — Confirmed. Research recommends three-file split (`Install.ps1` + `Uninstall.ps1` + `Install.Helpers.psm1`) mirroring the `Publish.ps1` pattern.
- [P0-T10] `docs/features/active/2026-04-18-unified-publish-script-34/plan.2026-04-18T00-00.md` — Confirmed. Serves as precedent pattern for this plan: per-helper batches with QA loop per batch.

Command: Read tool invocations (no shell command)
EXIT_CODE: 0
Output Summary: All 10 policy/spec/user-story/issue/research/precedent files read successfully. No conflicts detected with the approved plan. Work Mode confirmed as `full-feature`; AC source files are `spec.md` (Definition of Done + Seeded Test Conditions) and `user-story.md` (Acceptance Criteria).
