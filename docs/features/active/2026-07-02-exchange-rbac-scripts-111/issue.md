# exchange-rbac-scripts (Issue #111)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/exchange-rbac-scripts/ (Issue #111)

- Issue: #111
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/111
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

Product Increment 1 (Stage 1) requires Exchange Online Application RBAC scoping before any cloud mailbox access: service-principal registration, a mailbox management scope, the four minimum role assignments (`Application Mail.Read`, `Application Calendars.Read`, `Application MailboxSettings.Read`, `Application Mail.Send`), Send-on-behalf configuration for the assistant mailbox, and positive/negative scope verification (`docs/open-claw-approach.master.md` §12 Steps 2-7, §13 Step 3). Nothing exists in the repository: no script under `scripts/` references Exchange Online cmdlets. The administrator checklist is executable only as manual prose. Identified as gap F11 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (Epic C item 11).

## Proposed Behavior

- New PowerShell module `scripts/exchange-rbac/OpenClawRbac.psm1` (plus thin entry scripts) implementing the master §12 checklist as idempotent, parameterized advanced functions with `SupportsShouldProcess`:
  - `Register-OpenClawServicePrincipal` (wraps `New-ServicePrincipal`, idempotent on existing SP)
  - `New-OpenClawMailboxScope` (wraps `New-ManagementScope` with group-DN filter, or accepts an Administrative Unit id alternative)
  - `Grant-OpenClawRbacRoles` (wraps `New-ManagementRoleAssignment` for the four read+send roles; `Calendars.ReadWrite` behind an explicit `-IncludeCalendarWrite` switch, default off)
  - `Set-OpenClawSendOnBehalf` (wraps `Add-MailboxPermission` + `Set-Mailbox -GrantSendOnBehalfTo` per principal mailbox)
  - `Test-OpenClawScopeBoundary` (wraps `Test-ServicePrincipalAuthorization` for an in-scope and an out-of-scope mailbox, asserting the expected success/failure split)
- All Exchange cmdlet calls go through mockable wrapper seams per the repository PowerShell seam pattern, so Pester unit tests cover parameter validation, idempotency logic, control flow, and ShouldProcess without any tenant connectivity.
- Live-tenant execution is explicitly a human-administrator action: the feature ships a runbook at `<feature-folder>/runbooks/exchange-rbac-setup.runbook.md` (five required sections per the human-exception-runbook skill) that walks the administrator through connecting (`Connect-ExchangeOnline`) and invoking the functions with real tenant values, plus the §12 Step 6 broad-grant review checklist and the Step 8 engineering handoff package.
- No CI job attempts a live call; the per-commit loop runs only the mocked Pester suite (PoshQC toolchain).

## Acceptance Criteria

- [x] The module at `scripts/powershell/modules/OpenClawRbac/` exports exactly five public functions (`Register-OpenClawServicePrincipal`, `New-OpenClawMailboxScope`, `Grant-OpenClawRbacRoles`, `Set-OpenClawSendOnBehalf`, `Test-OpenClawScopeBoundary`) and ships with the thin entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1`; every function uses `CmdletBinding()`, every state-changing function declares `SupportsShouldProcess`, all parameters carry validation attributes, no tenant-specific values are hardcoded, every file is under the 500-line cap, and all code is PowerShell 7+ compatible.
- [x] Every Exchange Online cmdlet call goes through one of the nine wrapper seams (`New-ServicePrincipal`, `Get-ServicePrincipal`, `New-ManagementScope`, `Get-ManagementScope`, `New-ManagementRoleAssignment`, `Get-ManagementRoleAssignment`, `Add-MailboxPermission`, `Set-Mailbox`, `Test-ServicePrincipalAuthorization`), each resolving its cmdlet at runtime so machines without ExchangeOnlineManagement can parse, analyze, and test the module; Pester tests under `tests/scripts/` mirroring the production layout cover parameter validation, idempotency branches, ShouldProcess/WhatIf, wrapper invocation arguments, and the scope-boundary pass/fail matrix using mocked wrappers only (no temp files); PoshQC format, analyze, and test all pass via the MCP commands.
- [x] The runbook exists at `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` with the five required sections (Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation) and covers master §12 Steps 1-8 — including the Step 6 broad-grant review and the Step 8 engineering handoff package — plus §13 Step 3 boundary verification, with a dated Microsoft Learn citation for every step per `.claude/skills/human-exception-runbook/SKILL.md`.
- [x] The orchestrator (not the plan executor) verifies that orchestrator state records live-tenant execution as a `human_interaction` requirement with `response: exception` and `runbook_path: docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`. (Orchestrator-verified 2026-07-02: HI-1 present in artifacts/orchestration/orchestrator-state.json with response=exception and matching runbook_path; runbook file exists on disk.)
- [x] PowerShell coverage thresholds hold for the new code (line >= 85%, branch >= 75%) with coverage evidence recorded under `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/` (baseline and qa-gates); no coverage regression elsewhere.

## Constraints & Risks

- PowerShell change budget: per-batch cap 3 production + 3 test files — plan batches accordingly (module may need splitting into function-per-file layout under `scripts/exchange-rbac/`).
- ExchangeOnlineManagement module is NOT a repo dependency; scripts must not import it at parse time in a way that breaks analysis/tests on machines without it (guard imports; wrappers resolve at runtime).
- PoshQC toolchain (format -> analyze -> Pester) via MCP commands; PowerShell 7+; no temp files in tests.

## Test Conditions to Consider

- [ ] Unit: each function's parameter validation, idempotency short-circuits, ShouldProcess honored, wrapper invocation arguments.
- [ ] Unit: Test-OpenClawScopeBoundary pass/fail matrix (in-scope allowed + out-of-scope denied = success; any other combination = failure with precise reason).

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create active feature folder from the template
