# exchange-rbac-scripts — Spec

- **Issue:** #111
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02
- **Status:** Ready
- **Version:** 1.0

## Overview

Product Increment 1 (Stage 1) requires Exchange Online Application RBAC scoping before any cloud mailbox access: service-principal registration, a mailbox management scope, the four minimum role assignments (`Application Mail.Read`, `Application Calendars.Read`, `Application MailboxSettings.Read`, `Application Mail.Send`), Send-on-behalf configuration for the assistant mailbox, and positive/negative scope verification (`docs/open-claw-approach.master.md` §12 Steps 2-7, §13 Step 3). Nothing exists in the repository: no script under `scripts/` references Exchange Online cmdlets. The administrator checklist is executable only as manual prose. Identified as gap F11 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (Epic C item 11).

This feature delivers three artifacts:

1. A PowerShell module (`OpenClawRbac`) that implements master §12 Steps 2-5 and 7 as idempotent, parameterized advanced functions, plus a thin entry script that sequences them.
2. A fully mocked Pester suite exercising the module without tenant connectivity.
3. A human-exception runbook that a live Exchange administrator follows to execute the functions against the real tenant (§12 Steps 1-8, §13 Step 3), because live-tenant execution cannot and must not run in CI.

## Design Decisions (recorded)

### D1 — Module layout

The repository's existing module convention is `scripts/powershell/modules/<ModuleName>/` with a `.psd1` manifest and a root `.psm1` (observed: `scripts/powershell/modules/OpenClawContainerValidation/`). The issue draft's `scripts/exchange-rbac/` path is superseded by the observed convention. Because five public functions plus nine wrapper seams with comment-based help would risk the 500-line cap in a single `.psm1`, the root module dot-sources one `.ps1` file per public function plus one seams file:

| # | Production file | Purpose |
|---|---|---|
| 1 | `scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psd1` | Manifest; `RootModule = 'OpenClawRbac.psm1'`; exports exactly the five public functions and the nine wrapper functions (wrappers exported so Pester can mock them with `-ModuleName OpenClawRbac` and tests can verify invocation arguments) |
| 2 | `scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psm1` | Root module; dot-sources the sibling `.ps1` files |
| 3 | `scripts/powershell/modules/OpenClawRbac/OpenClawRbac.Seams.ps1` | Nine wrapper functions, one per Exchange cmdlet |
| 4 | `scripts/powershell/modules/OpenClawRbac/Register-OpenClawServicePrincipal.ps1` | §12 Step 2 |
| 5 | `scripts/powershell/modules/OpenClawRbac/New-OpenClawMailboxScope.ps1` | §12 Step 3 Option A |
| 6 | `scripts/powershell/modules/OpenClawRbac/Grant-OpenClawRbacRoles.ps1` | §12 Step 4 (both scope options) |
| 7 | `scripts/powershell/modules/OpenClawRbac/Set-OpenClawSendOnBehalf.ps1` | §12 Step 5 |
| 8 | `scripts/powershell/modules/OpenClawRbac/Test-OpenClawScopeBoundary.ps1` | §12 Step 7 / §13 Step 3 |
| 9 | `scripts/Invoke-OpenClawExchangeRbacSetup.ps1` | Thin entry script sequencing the functions |

Test counterparts (mirroring production structure per `.claude/rules/general-unit-test.md` and `.claude/rules/powershell.md`):

| # | Test file |
|---|---|
| 1 | `tests/scripts/powershell/modules/OpenClawRbac/OpenClawRbac.Module.Tests.ps1` (manifest loads, exports match) |
| 2 | `tests/scripts/powershell/modules/OpenClawRbac/OpenClawRbac.Seams.Tests.ps1` |
| 3 | `tests/scripts/powershell/modules/OpenClawRbac/Register-OpenClawServicePrincipal.Tests.ps1` |
| 4 | `tests/scripts/powershell/modules/OpenClawRbac/New-OpenClawMailboxScope.Tests.ps1` |
| 5 | `tests/scripts/powershell/modules/OpenClawRbac/Grant-OpenClawRbacRoles.Tests.ps1` |
| 6 | `tests/scripts/powershell/modules/OpenClawRbac/Set-OpenClawSendOnBehalf.Tests.ps1` |
| 7 | `tests/scripts/powershell/modules/OpenClawRbac/Test-OpenClawScopeBoundary.Tests.ps1` |
| 8 | `tests/scripts/Invoke-OpenClawExchangeRbacSetup.Tests.ps1` |

Note: existing tests under `tests/scripts/` are flat, but the written rules require the mirrored layout; new files follow the rules.

Batching under the per-batch cap (3 production + 3 test files, `.claude/rules/powershell.md`):

- **Batch 1:** production 1-3 (`.psd1`, `.psm1`, `Seams.ps1`) + tests 1-2.
- **Batch 2:** production 4-6 (Register, Scope, Grant) + tests 3-5.
- **Batch 3:** production 7-9 (SendOnBehalf, ScopeBoundary, entry script) + tests 6-8.
- **Batch 4 (docs only, no code budget):** runbook + evidence artifacts.

### D2 — Wrapper seams (one per Exchange cmdlet)

Per the repository seam pattern (`Invoke-<Tool>Exe -<Tool>Args` for executables; for cmdlets the analog is one wrapper function per external cmdlet), all nine Exchange Online cmdlets used are wrapped:

| Wrapper function | Wrapped cmdlet |
|---|---|
| `Invoke-OpenClawNewServicePrincipal` | `New-ServicePrincipal` |
| `Invoke-OpenClawGetServicePrincipal` | `Get-ServicePrincipal` |
| `Invoke-OpenClawNewManagementScope` | `New-ManagementScope` |
| `Invoke-OpenClawGetManagementScope` | `Get-ManagementScope` |
| `Invoke-OpenClawNewManagementRoleAssignment` | `New-ManagementRoleAssignment` |
| `Invoke-OpenClawGetManagementRoleAssignment` | `Get-ManagementRoleAssignment` |
| `Invoke-OpenClawAddMailboxPermission` | `Add-MailboxPermission` |
| `Invoke-OpenClawSetMailbox` | `Set-Mailbox` |
| `Invoke-OpenClawTestServicePrincipalAuthorization` | `Test-ServicePrincipalAuthorization` |

Wrapper contract:

- Explicit named parameters covering exactly the parameter subset the module uses (mock-signature-parity rule); no parameter named `Args`.
- The wrapped cmdlet is resolved at **runtime** via `Get-Command -Name '<Cmdlet>' -ErrorAction SilentlyContinue`; if unresolved, the wrapper throws a specific error naming the missing cmdlet and directing the operator to install `ExchangeOnlineManagement` and run `Connect-ExchangeOnline`. There is no parse-time `Import-Module ExchangeOnlineManagement` and no `#Requires -Modules` directive, so machines without the module can format, analyze, and run the mocked Pester suite.
- `Get-*` wrappers return `$null` when the object is not found (not-found is a data value, not an error); all other wrapper errors propagate.
- Unit tests never mock Exchange cmdlets directly; they mock the wrapper functions (`Mock -ModuleName OpenClawRbac Invoke-OpenClaw...`).

### D3 — Idempotency (check-before-create)

- `Register-OpenClawServicePrincipal`, `New-OpenClawMailboxScope`, and `Grant-OpenClawRbacRoles` (per role assignment) call their `Get-` wrapper first. When the object already exists, the function emits a clear informational message (`Write-Information`/`Write-Verbose`) identifying the existing object, performs no write, and returns the existing object (or an `AlreadyExists` summary row).
- `Set-OpenClawSendOnBehalf` has no `Get-` wrapper in the approved seam set. Its idempotency rests on: (a) `Set-Mailbox -GrantSendOnBehalfTo @{Add=...}` is additive and safe to re-run; (b) a re-run of `Add-MailboxPermission` for an existing ACE is caught by a **targeted** error check (matching the documented existing-permission failure) and reported as a no-op; any other error is re-thrown. If this targeted matching proves brittle in practice, the follow-up is to add a `Get-MailboxPermission` wrapper — deliberately out of scope for this feature to keep the seam surface at the approved nine.
- Every state-changing call site is inside a `$PSCmdlet.ShouldProcess(...)` gate, so `-WhatIf` produces a complete dry-run with zero wrapper write calls.

### D4 — Runbook authored in this feature

Live-tenant execution is a human-administrator action. This feature authors the runbook at the fixed path `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` (already recorded in orchestrator state). Contract per `.claude/skills/human-exception-runbook/SKILL.md`: the five required sections in order (Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation).

Content coverage:

- §12 Step 1 (required admin roles) as Prerequisites.
- §12 Step 2 (app registration + `Register-OpenClawServicePrincipal`), Step 3 (`New-OpenClawMailboxScope` or the Administrative Unit alternative), Step 4 (`Grant-OpenClawRbacRoles`), Step 5 (`Set-OpenClawSendOnBehalf`) as step-by-step instructions invoking the new functions with placeholder tenant values.
- §12 Step 6 broad-grant review as an explicit review checklist (human judgment; not automated).
- §12 Step 7 and §13 Step 3 boundary verification via `Test-OpenClawScopeBoundary`, including the propagation-latency note (30 minutes to 2 hours outside the test cmdlet; the test cmdlet bypasses that cache).
- §12 Step 8 handoff-package checklist.
- Warnings surfaced verbatim from the master doc: use the **Enterprise Application service principal Object ID**, not the App Registration object ID; only **direct** group membership counts for the management scope (nested members are out of scope); do **not** grant Send As for this workflow.

Sourcing: Microsoft Learn is the documentation source, with a dated citation (`updated_at`) for every step. All step-by-step instructions in this runbook are PowerShell CLI steps — no third-party UI navigation is required, and the runbook states this explicitly. The one potentially UI-bound activity (creating the app registration itself) is scoped as a Prerequisite that references master §12 Step 2's own checklist; if UI navigation guidance is added later it must be sourced MCP-first/web-second per the skill.

### D5 — Structured boundary-test result and exit semantics

`Test-OpenClawScopeBoundary` returns a structured `[pscustomobject]` and never calls `exit`:

```
InScopeMailbox      : in-scope-user@contoso.com
OutOfScopeMailbox   : out-of-scope-user@contoso.com
InScopeAllowed      : $true    # assigned roles present and InScope = True
OutOfScopeDenied    : $true    # no effective role or InScope = False
Succeeded           : $true    # InScopeAllowed -and OutOfScopeDenied
FailureReason       : $null    # precise reason string when Succeeded = $false
InScopeDetails      : <raw Test-ServicePrincipalAuthorization rows>
OutOfScopeDetails   : <raw Test-ServicePrincipalAuthorization rows>
```

Pass/fail matrix: `Succeeded = $true` only when the in-scope mailbox is allowed **and** the out-of-scope mailbox is denied. Every other combination sets `Succeeded = $false` with a precise `FailureReason` ("in-scope mailbox has no effective role or InScope=False", "out-of-scope mailbox is unexpectedly in scope", or both, joined). Exit-code mapping lives only in the entry script: `Invoke-OpenClawExchangeRbacSetup.ps1` exits `0` when `Succeeded` is `$true` and `1` otherwise, so the runbook's verification step and any future automation get deterministic process semantics while the function stays composable.

## Behavior

Five public advanced functions, all `CmdletBinding()`, PowerShell 7+, no hardcoded tenant values, approved verbs, comment-based help:

- **`Register-OpenClawServicePrincipal`** (§12 Step 2; `SupportsShouldProcess`) — parameters `-AppId <guid>` (mandatory), `-EnterpriseApplicationObjectId <guid>` (mandatory; help text warns this is the Enterprise Application service principal Object ID, not the App Registration object ID), `-DisplayName <string>` (default `'OpenClaw Assistant'`, `ValidateNotNullOrEmpty`). Checks `Invoke-OpenClawGetServicePrincipal` by AppId; no-ops with a message when present; otherwise creates via `Invoke-OpenClawNewServicePrincipal`.
- **`New-OpenClawMailboxScope`** (§12 Step 3 Option A; `SupportsShouldProcess`) — parameters `-Name <string>` (default `'OpenClaw-ScopedMailboxes'`), `-GroupDistinguishedName <string>` (mandatory). Checks `Invoke-OpenClawGetManagementScope`; creates with filter `"MemberOfGroup -eq '<DN>'"`. Always emits a warning that only direct group membership counts (nested members are not in scope). Option B (Administrative Unit) needs no scope object; callers skip this function and use `Grant-OpenClawRbacRoles -AdministrativeUnitId`.
- **`Grant-OpenClawRbacRoles`** (§12 Step 4; `SupportsShouldProcess`) — parameters `-EnterpriseApplicationObjectId <guid>` (mandatory), two parameter sets: `-ScopeName <string>` (maps to `-CustomResourceScope`) or `-AdministrativeUnitId <guid>` (maps to `-RecipientAdministrativeUnitScope`), `-RoleAssignmentPrefix <string>` (default `'OpenClaw'`), `-IncludeCalendarWrite` (switch, default off). Iterates the four minimum roles — `Application Mail.Read` → `<prefix>-MailRead`, `Application Calendars.Read` → `<prefix>-CalendarsRead`, `Application MailboxSettings.Read` → `<prefix>-MailboxSettingsRead`, `Application Mail.Send` → `<prefix>-MailSend` — plus `Application Calendars.ReadWrite` → `<prefix>-CalendarsReadWrite` only when the switch is set. Each assignment checks `Invoke-OpenClawGetManagementRoleAssignment` by name before creating; returns one summary object per role (`RoleName`, `AssignmentName`, `Status: Created|AlreadyExists|WhatIf`).
- **`Set-OpenClawSendOnBehalf`** (§12 Step 5; `SupportsShouldProcess`) — parameters `-PrincipalMailbox <string>` (mandatory, accepts pipeline for multiple principals), `-AssistantMailbox <string>` (mandatory). Calls `Invoke-OpenClawAddMailboxPermission` (`FullAccess`, `InheritanceType All`, `AutoMapping $false`) and `Invoke-OpenClawSetMailbox` (`-GrantSendOnBehalfTo @{Add=<assistant>}`). Never configures Send As. Idempotency per D3.
- **`Test-OpenClawScopeBoundary`** (§12 Step 7 / §13 Step 3; read-only, no ShouldProcess) — parameters `-EnterpriseApplicationObjectId <guid>`, `-InScopeMailbox <string>`, `-OutOfScopeMailbox <string>` (all mandatory). Two `Invoke-OpenClawTestServicePrincipalAuthorization` calls; returns the structured result per D5.

Entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1` (`SupportsShouldProcess`) imports the module by relative path and sequences Register → Scope (Option A only; skipped under `-AdministrativeUnitId`) → Grant → SendOnBehalf (repeated per principal mailbox) → Boundary test, forwarding `-WhatIf`, and maps the boundary result to exit code 0/1. It contains no logic beyond sequencing and exit-code mapping.

No CI job attempts a live tenant call; the per-commit loop runs only the mocked Pester suite via the PoshQC MCP toolchain.

## Inputs / Outputs

- **Inputs:** function parameters supplied by the administrator at invocation time — AppId (GUID), Enterprise Application service principal Object ID (GUID), scope name, group DN or Administrative Unit GUID, principal/assistant mailbox SMTP addresses, in/out-of-scope test mailboxes. No environment variables, no config files, no secrets: authentication is the ambient `Connect-ExchangeOnline` session, which is out of scope for the module.
- **Outputs:** PowerShell objects (service-principal object, management-scope object, per-role assignment summaries, structured boundary-test result); `Write-Information`/`Write-Verbose` progress messages; `Write-Warning` for the direct-membership limitation. No files written, no telemetry.
- **Config keys and defaults:** none. Defaults are parameter defaults only (`DisplayName 'OpenClaw Assistant'`, scope name `'OpenClaw-ScopedMailboxes'`, prefix `'OpenClaw'`, `IncludeCalendarWrite:$false`).
- **Versioning / backward compatibility:** new module (`ModuleVersion 0.1.0`), no existing callers; no public API is broken.

## API / CLI Surface

Example invocations (placeholder tenant values):

```powershell
Import-Module ./scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psd1

Register-OpenClawServicePrincipal `
    -AppId '00000000-0000-0000-0000-000000000001' `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -WhatIf

New-OpenClawMailboxScope -GroupDistinguishedName 'CN=OpenClaw Scoped Mailboxes,...'
# WARNING: only direct group membership counts; nested members are not in scope.

Grant-OpenClawRbacRoles `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -ScopeName 'OpenClaw-ScopedMailboxes'
# -> four summary rows: Status Created or AlreadyExists

Set-OpenClawSendOnBehalf -PrincipalMailbox 'executive@contoso.com' -AssistantMailbox 'assistant@contoso.com'

$result = Test-OpenClawScopeBoundary `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -InScopeMailbox 'in-scope-user@contoso.com' `
    -OutOfScopeMailbox 'out-of-scope-user@contoso.com'
$result.Succeeded   # $true when the boundary holds
```

Contracts and validation rules:

- GUID parameters typed `[guid]` (malformed input fails at binding).
- Mailbox and DN parameters `ValidateNotNullOrEmpty`; mailbox parameters additionally validated with a minimal SMTP pattern.
- Re-running any function against existing state is a reported no-op, never an error (safe-to-rerun contract).
- `-WhatIf` on any state-changing function or the entry script performs zero wrapper write calls.

## Data & State

- All state changes occur in the Exchange Online tenant, and only when a human administrator runs the functions in a connected session. The repository stores no tenant state, no credentials, and no output artifacts.
- Invariants: check-before-create per D3; the boundary-test function is strictly read-only; `Succeeded` is true iff in-scope allowed and out-of-scope denied.
- No caching, persistence, migration, or backfill.

## Constraints & Risks

- **Change budget:** per-batch cap 3 production + 3 test files — layout and batching per D1 (9 production files, 8 test files, 3 code batches).
- **ExchangeOnlineManagement is not a repo dependency:** wrappers resolve cmdlets at runtime (D2); no parse-time import; format/analyze/test must pass on machines without the module.
- **Toolchain:** PoshQC via MCP (`run_poshqc_format` → `run_poshqc_analyze` → `run_poshqc_test`), PowerShell 7+, Pester v5, no temp files in tests. Note: `.claude/rules/powershell.md` references `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`, but no `scripts/powershell/PoshQC/` directory exists in this worktree; the MCP server supplies the settings.
- **Wrong-ID risk:** confusing the App Registration object ID with the Enterprise Application service principal Object ID silently mis-registers the SP. Mitigated by the explicit parameter name `EnterpriseApplicationObjectId`, help text, and a runbook warning quoting master §12 Step 2.
- **Nested-group risk:** `MemberOfGroup` scope filters count direct members only. Mitigated by an unconditional `Write-Warning` in `New-OpenClawMailboxScope` and a runbook callout.
- **`Add-MailboxPermission` re-run handling:** the targeted existing-ACE error match (D3) could be brittle across ExchangeOnlineManagement versions; documented follow-up is a `Get-MailboxPermission` wrapper if it proves so.
- **RBAC propagation latency:** 30 minutes to 2 hours outside `Test-ServicePrincipalAuthorization`; the runbook instructs verification via the test cmdlet, which bypasses the cache.
- **Broad-grant review (§12 Step 6) is not automatable here:** removing overlapping tenant-wide Graph application permissions is a human Entra review; the runbook carries it as a checklist, and the module makes no Graph calls.

## Implementation Strategy

- **Scope of change:** new module directory `scripts/powershell/modules/OpenClawRbac/` (8 files), new entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1`, new test tree `tests/scripts/powershell/modules/OpenClawRbac/` (7 files) plus `tests/scripts/Invoke-OpenClawExchangeRbacSetup.Tests.ps1`, and the runbook under this feature folder. No existing file is modified except possibly README cross-links.
- **New functions:** five public functions + nine wrapper seams per D1/D2; no changes to existing modules.
- **Dependency changes:** none. ExchangeOnlineManagement is intentionally not added; Pester/PSScriptAnalyzer come via the PoshQC MCP toolchain.
- **Logging:** `Write-Information`/`Write-Verbose` for idempotent no-ops and progress; `Write-Warning` for the direct-membership limitation; `throw`/`Write-Error` for missing-cmdlet and unexpected failures. No silent catch-alls.
- **Rollout:** no feature flags; the module is inert until a human runs it in a connected session. `-WhatIf` is the dry-run path. Evidence (coverage baseline, PoshQC gate outputs, coverage comparison) is written under `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/<kind>/` (`baseline/`, `qa-gates/`) per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`.

## Acceptance Criteria

- [x] The module at `scripts/powershell/modules/OpenClawRbac/` exports exactly five public functions (`Register-OpenClawServicePrincipal`, `New-OpenClawMailboxScope`, `Grant-OpenClawRbacRoles`, `Set-OpenClawSendOnBehalf`, `Test-OpenClawScopeBoundary`) and ships with the thin entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1`; every function uses `CmdletBinding()`, every state-changing function declares `SupportsShouldProcess`, all parameters carry validation attributes, no tenant-specific values are hardcoded, every file is under the 500-line cap, and all code is PowerShell 7+ compatible.
- [x] Every Exchange Online cmdlet call goes through one of the nine wrapper seams (`New-ServicePrincipal`, `Get-ServicePrincipal`, `New-ManagementScope`, `Get-ManagementScope`, `New-ManagementRoleAssignment`, `Get-ManagementRoleAssignment`, `Add-MailboxPermission`, `Set-Mailbox`, `Test-ServicePrincipalAuthorization`), each resolving its cmdlet at runtime so machines without ExchangeOnlineManagement can parse, analyze, and test the module; Pester tests under `tests/scripts/` mirroring the production layout cover parameter validation, idempotency branches, ShouldProcess/WhatIf, wrapper invocation arguments, and the scope-boundary pass/fail matrix using mocked wrappers only (no temp files); PoshQC format, analyze, and test all pass via the MCP commands.
- [x] The runbook exists at `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` with the five required sections (Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation) and covers master §12 Steps 1-8 — including the Step 6 broad-grant review and the Step 8 engineering handoff package — plus §13 Step 3 boundary verification, with a dated Microsoft Learn citation for every step per `.claude/skills/human-exception-runbook/SKILL.md`.
- [x] The orchestrator (not the plan executor) verifies that orchestrator state records live-tenant execution as a `human_interaction` requirement with `response: exception` and `runbook_path: docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`. (Orchestrator-verified 2026-07-02: HI-1 present in artifacts/orchestration/orchestrator-state.json with response=exception and matching runbook_path; runbook file exists on disk.)
- [x] PowerShell coverage thresholds hold for the new code (line >= 85%, branch >= 75%) with coverage evidence recorded under `docs/features/active/2026-07-02-exchange-rbac-scripts-111/evidence/` (baseline and qa-gates); no coverage regression elsewhere.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments (including machines without ExchangeOnlineManagement)
- [ ] Tests updated/added (mocked Pester unit tests; no integration tests — live tenant is human-only per the runbook)
- [ ] Edge cases and error handling covered by tests (missing cmdlet, existing objects, boundary-test failure matrix, WhatIf)
- [ ] Docs updated (runbook authored; feature-folder links current)
- [ ] Logging added per Implementation Strategy (no telemetry applicable)
- [ ] Toolchain pass completed (PoshQC format → analyze → test, restart on any failure or auto-fix)

## Seeded Test Conditions (from potential)

- [ ] Unit: each function's parameter validation (GUID binding failures, empty mailbox/DN rejection), idempotency short-circuits (Get-wrapper returns existing object → no write-wrapper call, informational message emitted), ShouldProcess honored (`-WhatIf` → zero write-wrapper invocations), wrapper invocation arguments (exact role names, assignment names, `-CustomResourceScope` vs `-RecipientAdministrativeUnitScope`, `AutoMapping $false`, `@{Add=}` payload).
- [ ] Unit: `Test-OpenClawScopeBoundary` pass/fail matrix — (allowed, denied) → `Succeeded = $true`; (denied, denied), (allowed, allowed), (denied, allowed) → `Succeeded = $false` with a precise `FailureReason` naming the failing side(s).
- [ ] Unit: wrapper runtime resolution — wrapper throws a specific, actionable error naming the missing Exchange cmdlet when `Get-Command` cannot resolve it.
- [ ] Unit: `Grant-OpenClawRbacRoles` grants exactly four roles by default and adds `Application Calendars.ReadWrite` only with `-IncludeCalendarWrite`.
- [ ] Unit: `Set-OpenClawSendOnBehalf` treats the existing-ACE outcome as a reported no-op and re-throws any other error; never invokes any Send As configuration.
- [ ] Unit: entry script maps boundary `Succeeded = $true` to exit 0 and `$false` to exit 1, and forwards `-WhatIf` to every state-changing call.
