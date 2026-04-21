# Phase 0 — Instructions Read

- Timestamp: 2026-04-18T00-00
- Policy Order: AGENTS.md, general-code-change, general-unit-test, powershell, tonality, spec.md, user-story.md, issue.md, research artifact.

## Files Read

- `AGENTS.md` — Repo-root standing instructions (generated from `.github/instructions/*.instructions.md`). Authoritative high-level guidance for all code change, testing, PowerShell, and CI policies. Confirms the 500-line-per-file ceiling, PoshQC toolchain order, and Pester v5 requirements.
- `.claude/rules/general-code-change.md` — Cross-language code change policy. Simplicity-first design priorities, mandatory format -> lint -> type-check -> test toolchain loop with restart-on-change, the 500-line file ceiling, fail-fast error handling.
- `.claude/rules/general-unit-test.md` — Cross-language unit test policy. Five test properties (independence, isolation, speed, determinism, readability), repo-wide line coverage >= 80%, new-module coverage >= 90%, no temporary files, Arrange-Act-Assert structure.
- `.claude/rules/powershell.md` — PowerShell standards. Mandates PoshQC MCP commands for format/analyze/test, PowerShell 7+ compatibility, advanced functions with `CmdletBinding`, `SupportsShouldProcess` for state-changing actions, approved verbs, Pester v5.x, 500-line limit, no external dependencies in unit tests.
- `.claude/rules/tonality.md` — Required professional tone for all agent-authored content. Prohibits humor, hyperbole, and gratuitous metaphor; requires evidence-first wording.
- `docs/features/active/2026-04-18-unified-publish-script-34/spec.md` — Feature spec. Read in full. Confirmed: Definition of Done checklist (9 items), binding owner decisions (self-contained `win-x64` for Core/HostAdapter; CI workflow rename), three delegated open questions resolved by planner as Q1 strict ValidatePattern, Q2 `msix.pubxml` retention, Q3 structural stability.
- `docs/features/active/2026-04-18-unified-publish-script-34/user-story.md` — User story. Read in full. Confirmed: Acceptance Criteria checklist with 10 items authoritative for AC tracking.
- `docs/features/active/2026-04-18-unified-publish-script-34/issue.md` — Issue #34 content. Read in full. Confirmed Work Mode is `full-feature`.
- `artifacts/research/2026-04-18-unified-publish-script.md` — Research artifact. Read. Confirmed Design B (orchestrator + helper module split) drives the plan's architecture and that `OpenClaw.MailBridge` and `OpenClaw.MailBridge.Client` already depend on the `msix.pubxml` publish profile for their self-contained `win-x64` posture.
