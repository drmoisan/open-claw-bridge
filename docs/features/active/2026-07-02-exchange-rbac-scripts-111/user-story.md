# `exchange-rbac-scripts` — User Story

- Issue: #111
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-07-02

## Story Statement

- As an **Exchange Online / Entra administrator**, I want the master §12 RBAC checklist delivered as idempotent, parameterized PowerShell functions with `-WhatIf` support and a step-by-step runbook, so that I can execute the scoping setup safely, preview every change before it happens, and rerun any step without side effects — instead of hand-transcribing cmdlets from manual prose.
- As an **engineer receiving the admin handoff**, I want the scope boundary verified with a structured pass/fail result and the §12 Step 8 handoff package assembled from a checklist, so that Product Increment 1 development starts against an app identity that is proven to reach only the intended mailboxes.

## Problem / Why

Product Increment 1 (Stage 1) requires Exchange Online Application RBAC scoping before any cloud mailbox access: service-principal registration, a mailbox management scope, the four minimum role assignments (`Application Mail.Read`, `Application Calendars.Read`, `Application MailboxSettings.Read`, `Application Mail.Send`), Send-on-behalf configuration for the assistant mailbox, and positive/negative scope verification (`docs/open-claw-approach.master.md` §12 Steps 2-7, §13 Step 3). Nothing exists in the repository: no script under `scripts/` references Exchange Online cmdlets. The administrator checklist is executable only as manual prose. Identified as gap F11 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (Epic C item 11).

Manual prose execution carries concrete failure modes the tooling removes: pasting the App Registration object ID where the Enterprise Application service principal Object ID is required, scoping to a group whose members are nested (and therefore silently out of scope), granting Send As instead of Send on behalf, and partial reruns that error out or double-apply.

## Personas & Scenarios

- **Persona: Dana, Exchange Online / Entra administrator**
  - Holds Organization Management in Exchange Online and Exchange Administrator in Entra ID (master §12 Step 1).
  - Cares about least-privilege scoping and being able to prove, before handoff, that the app cannot touch out-of-scope mailboxes.
  - Constraints: works in a production tenant; every change must be previewable (`-WhatIf`) and safe to rerun; may be interrupted mid-checklist and resume later.
  - Frustrations: prose checklists with `<PLACEHOLDER>` cmdlets invite transcription errors; the Object-ID confusion (App Registration vs Enterprise Application) is easy to hit and hard to diagnose.
- **Persona: Evan, bridge engineer**
  - Implements §13 (app-only auth, ingestion) once the admin handoff arrives.
  - Cares about receiving a complete handoff package: tenant/client/SP identifiers, credential expiry, scope definition, assistant mailbox, one in-scope and one out-of-scope test mailbox, and written confirmation of the broad-grant review.
  - Constraint: must not begin coding against a mailbox until both positive and negative test mailboxes are confirmed (master §13 Step 1).

- **Scenario: First-time RBAC setup (happy path)**
  - Dana is asked to enable Stage 1. The orchestrator has recorded live-tenant execution as a permitted human exception, which cues the runbook at `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`. (Orchestrator-verified 2026-07-02: HI-1 present in artifacts/orchestration/orchestrator-state.json with response=exception and matching runbook_path; runbook file exists on disk.)
  - Dana confirms the Prerequisites (roles, app registration values, `Connect-ExchangeOnline` session), then runs each step: `Register-OpenClawServicePrincipal`, `New-OpenClawMailboxScope` (noting the warning that only direct group members count), `Grant-OpenClawRbacRoles` (four roles; calendar write deliberately deferred), and `Set-OpenClawSendOnBehalf` for each principal mailbox — each first with `-WhatIf`, then live.
  - Dana completes the §12 Step 6 broad-grant review checklist in the runbook, runs `Test-OpenClawScopeBoundary`, sees `Succeeded = $true`, and assembles the Step 8 handoff package for Evan.
- **Scenario: Interrupted run, resumed later (idempotency)**
  - Dana ran Steps 2-3 last week before being pulled away. Resuming, Dana reruns the entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1` end to end.
  - The service principal and management scope are reported as already existing (informational no-ops, no errors, no duplicates); the role assignments and Send-on-behalf configuration are applied; the boundary test runs last and the script exits 0.
- **Scenario: Boundary verification fails (negative path)**
  - `Test-OpenClawScopeBoundary` returns `Succeeded = $false` with `FailureReason` stating the out-of-scope mailbox is unexpectedly in scope.
  - Dana follows the runbook's Verification guidance: re-checks the scoping group's direct membership and the §12 Step 6 review for a leftover tenant-wide Graph grant, corrects it, and reruns the test (which bypasses the 30-minute-to-2-hour propagation cache). Handoff to Evan is blocked until `Succeeded = $true`.

## Acceptance Criteria

- [x] The module at `scripts/powershell/modules/OpenClawRbac/` exports exactly five public functions (`Register-OpenClawServicePrincipal`, `New-OpenClawMailboxScope`, `Grant-OpenClawRbacRoles`, `Set-OpenClawSendOnBehalf`, `Test-OpenClawScopeBoundary`) and ships with the thin entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1`; every function uses `CmdletBinding()`, every state-changing function declares `SupportsShouldProcess`, all parameters carry validation attributes, no tenant-specific values are hardcoded, every file is under the 500-line cap, and all code is PowerShell 7+ compatible.
- [x] Every Exchange Online cmdlet call goes through one of the nine wrapper seams (`New-ServicePrincipal`, `Get-ServicePrincipal`, `New-ManagementScope`, `Get-ManagementScope`, `New-ManagementRoleAssignment`, `Get-ManagementRoleAssignment`, `Add-MailboxPermission`, `Set-Mailbox`, `Test-ServicePrincipalAuthorization`), each resolving its cmdlet at runtime so machines without ExchangeOnlineManagement can parse, analyze, and test the module; Pester tests under `tests/scripts/` mirroring the production layout cover parameter validation, idempotency branches, ShouldProcess/WhatIf, wrapper invocation arguments, and the scope-boundary pass/fail matrix using mocked wrappers only (no temp files); PoshQC format, analyze, and test all pass via the MCP commands.
- [x] The runbook exists at `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` with the five required sections (Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation) and covers master §12 Steps 1-8 — including the Step 6 broad-grant review and the Step 8 engineering handoff package — plus §13 Step 3 boundary verification, with a dated Microsoft Learn citation for every step per `.claude/skills/human-exception-runbook/SKILL.md`.
- [x] The orchestrator (not the plan executor) verifies that orchestrator state records live-tenant execution as a `human_interaction` requirement with `response: exception` and `runbook_path: docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`.
- [x] PowerShell coverage thresholds hold for the new code (line >= 85%, branch >= 75%) with coverage evidence recorded under `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/` (baseline and qa-gates); no coverage regression elsewhere.

## Non-Goals

- No CI job or automated test ever calls a live Exchange Online tenant; the Pester suite is fully mocked at the wrapper seams.
- No automation of §12 Step 6: reviewing and removing overlapping tenant-wide Entra Graph application permissions remains a human review carried as a runbook checklist; the module makes no Microsoft Graph calls.
- No app-registration creation: creating or designating the Entra app registration (§12 Step 2 prerequisite) is out of scope and referenced from the runbook's Prerequisites.
- No Send As configuration, in any form (master §12 Step 5 administrative rule).
- No credential or secret handling: authentication is the administrator's ambient `Connect-ExchangeOnline` session; the module never touches certificates, thumbprints, or client secrets.
- No `Application Calendars.ReadWrite` grant by default; it is available only behind the explicit `-IncludeCalendarWrite` switch and otherwise deferred per the master doc.
- No ExchangeOnlineManagement dependency added to the repository; cmdlets resolve at runtime only.
- No `Get-MailboxPermission`/`Get-Mailbox` wrappers in this feature (documented follow-up if the targeted existing-ACE handling in `Set-OpenClawSendOnBehalf` proves brittle).
