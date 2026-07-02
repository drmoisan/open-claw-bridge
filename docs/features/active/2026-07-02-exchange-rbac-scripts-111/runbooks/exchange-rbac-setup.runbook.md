# Exchange Application RBAC Setup — Human-Exception Runbook

- **Feature:** exchange-rbac-scripts (Issue #111)
- **Runbook path:** `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`
- **Authored:** 2026-07-02
- **Source checklist:** `docs/open-claw-approach.master.md` section 12 Steps 1-8 and section 13 Step 3

## Cue

Act on this runbook when the orchestrator records live-tenant execution of the Exchange Application RBAC scoping as a permitted human exception: `artifacts/orchestration/orchestrator-state.json` contains a `human_interaction.requirements[]` entry (HI-1) with `response: "exception"` and `runbook_path` pointing to this file. Live execution against the Exchange Online tenant cannot and must not run in CI; a human Exchange administrator executes the steps below in a connected session. The trigger event is the request to enable Product Increment 1 (Stage 1) mailbox access, which requires the RBAC scoping to exist and the scope boundary to be verified before engineering handoff.

## Prerequisites

All of the following must be true before starting:

1. **Administrator roles (master section 12 Step 1).** The executing administrator holds:
   - **Organization Management** in Exchange Online (that role group can assign the Application RBAC roles), and
   - **Exchange Administrator** in Microsoft Entra ID.
   If a separate identity team manages the app registration or admin-consent workflow, coordinate with them before starting.
2. **App registration values (master section 12 Step 2 checklist, referenced here as a prerequisite — creating the app registration is out of scope for this runbook).** A new app registration has been created or designated for this assistant workflow only, and the following values are recorded for engineering:
   - Tenant ID
   - Client / Application ID
   - Enterprise Application service principal Object ID
   - Certificate thumbprint / path / key identifier, or client secret (certificate-based auth preferred for production)
   - Expiration date for that credential

   > **Warning (verbatim from the master checklist):** "Important: use the Enterprise Application service principal Object ID from Entra, not the App Registration object ID."
3. **PowerShell session.** PowerShell 7+, the `ExchangeOnlineManagement` module installed, and an authenticated session established with `Connect-ExchangeOnline` under the roles above.
4. **Repository checkout.** A checkout of this repository so the `OpenClawRbac` module at `scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psd1` and the entry script `scripts/Invoke-OpenClawExchangeRbacSetup.ps1` are available.
5. **Tenant input values.** The scoping group DistinguishedName (Option A) or Administrative Unit GUID (Option B), the principal mailbox SMTP address(es), the assistant mailbox SMTP address, one known in-scope test mailbox, and one known out-of-scope test mailbox.

## Step-by-step Instructions

**All step-by-step instructions in this runbook are PowerShell CLI steps. No third-party UI navigation is required.**

Replace every placeholder value (GUIDs, DNs, mailbox addresses) with real tenant values. Run each state-changing step first with `-WhatIf`, review the dry-run output, then run it live. Every function is idempotent: re-running against existing state is a reported no-op, never an error.

### Step 0 — Connect and import the module

```powershell
Connect-ExchangeOnline
Import-Module ./scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psd1
```

### Step 1 — Register the service principal (master section 12 Step 2)

```powershell
# Dry run first, then repeat without -WhatIf
Register-OpenClawServicePrincipal `
    -AppId '00000000-0000-0000-0000-000000000001' `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -WhatIf

Register-OpenClawServicePrincipal `
    -AppId '00000000-0000-0000-0000-000000000001' `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002'
```

> **Warning (verbatim from the master checklist):** "Important: use the Enterprise Application service principal Object ID from Entra, not the App Registration object ID."

### Step 2 — Define the mailbox scope (master section 12 Step 3, Option A)

Capture the scoping group DN first, then create the management scope:

```powershell
Get-Group -Identity 'OpenClaw Scoped Mailboxes' | Format-List Name, DistinguishedName

New-OpenClawMailboxScope `
    -GroupDistinguishedName 'CN=OpenClaw Scoped Mailboxes,OU=Groups,DC=contoso,DC=com' `
    -WhatIf

New-OpenClawMailboxScope `
    -GroupDistinguishedName 'CN=OpenClaw Scoped Mailboxes,OU=Groups,DC=contoso,DC=com'
```

> **Warning (verbatim from the master checklist):** "Important limitation: only direct group membership counts; nested members are not considered in scope." The function also emits this warning on every invocation.

**Option B — Administrative Unit:** if the organization manages mailbox boundaries through a Microsoft Entra Administrative Unit, skip this step entirely and pass `-AdministrativeUnitId '<ADMIN_UNIT_GUID>'` to `Grant-OpenClawRbacRoles` in Step 3 instead of `-ScopeName`.

### Step 3 — Assign the minimum Exchange Application RBAC roles (master section 12 Step 4)

```powershell
Grant-OpenClawRbacRoles `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -ScopeName 'OpenClaw-ScopedMailboxes' `
    -WhatIf

Grant-OpenClawRbacRoles `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -ScopeName 'OpenClaw-ScopedMailboxes'
```

This grants exactly the four minimum roles: `Application Mail.Read`, `Application Calendars.Read`, `Application MailboxSettings.Read`, `Application Mail.Send` (one summary row each: `Created` or `AlreadyExists`). `Application Calendars.ReadWrite` is deliberately deferred; add it later, only when rescheduling is enabled, by re-running with `-IncludeCalendarWrite`.

### Step 4 — Configure Send on Behalf for the assistant mailbox (master section 12 Step 5)

Repeat for each principal mailbox the assistant represents (pipeline input supported):

```powershell
Set-OpenClawSendOnBehalf `
    -PrincipalMailbox 'executive@contoso.com' `
    -AssistantMailbox 'assistant@contoso.com' `
    -WhatIf

Set-OpenClawSendOnBehalf `
    -PrincipalMailbox 'executive@contoso.com' `
    -AssistantMailbox 'assistant@contoso.com'
```

Administrative rules (master section 12 Step 5): keep the assistant mailbox in scope; document exactly which principal mailboxes the assistant mailbox may represent.

> **Warning (verbatim from the master checklist):** "Do not grant Send As for this workflow." The function never configures Send As.

### Step 5 — Review and remove overlapping unscoped Entra application permissions (master section 12 Step 6 — human review checklist)

This is the most important scoping safeguard and is a human judgment review; it is not automated by this feature (the module makes no Microsoft Graph calls). If the app already has tenant-wide Microsoft Graph application permissions such as `Mail.Read`, `Mail.Send`, `Calendars.Read`, `Calendars.ReadWrite`, or `MailboxSettings.Read`, review them and remove overlapping broad grants before relying on Application RBAC scoping. Complete this checklist:

- [ ] The Enterprise Application's admin-consent list was checked.
- [ ] A list of any Microsoft Graph application permissions that remain has been produced for engineering.
- [ ] Confirmed that no broad overlapping Exchange-data application permissions were left in place unintentionally.

### Step 6 — Verify the scope boundary (master section 12 Step 7 / section 13 Step 3)

```powershell
$result = Test-OpenClawScopeBoundary `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -InScopeMailbox 'in-scope-user@contoso.com' `
    -OutOfScopeMailbox 'out-of-scope-user@contoso.com'
$result
```

Note on propagation latency: RBAC permission propagation can take 30 minutes to 2 hours outside the test cmdlet. The underlying `Test-ServicePrincipalAuthorization` cmdlet bypasses that cache and is the fastest way to validate the configuration, so this step can run immediately after Steps 1-4.

Alternatively, run the entire sequence (Steps 1-4 plus this boundary check) in one shot with the entry script, which maps the boundary result to the process exit code (0 = boundary holds, 1 = boundary violated):

```powershell
./scripts/Invoke-OpenClawExchangeRbacSetup.ps1 `
    -AppId '00000000-0000-0000-0000-000000000001' `
    -EnterpriseApplicationObjectId '00000000-0000-0000-0000-000000000002' `
    -GroupDistinguishedName 'CN=OpenClaw Scoped Mailboxes,OU=Groups,DC=contoso,DC=com' `
    -PrincipalMailbox 'executive@contoso.com' `
    -AssistantMailbox 'assistant@contoso.com' `
    -InScopeMailbox 'in-scope-user@contoso.com' `
    -OutOfScopeMailbox 'out-of-scope-user@contoso.com'
```

### Step 7 — Assemble the engineering handoff package (master section 12 Step 8 — checklist)

Provide engineering with all of the following:

- [ ] Tenant ID
- [ ] Client / Application ID
- [ ] Enterprise Application service principal Object ID
- [ ] Secret or certificate access details and expiration
- [ ] Name of the Exchange Management Scope or the Administrative Unit GUID
- [ ] The scoped mailbox list (or the source group / AU that defines it)
- [ ] The assistant mailbox SMTP / UPN that the agent sends from
- [ ] Confirmation that these roles are assigned: `Application Mail.Read`, `Application Calendars.Read`, `Application MailboxSettings.Read`, `Application Mail.Send`; plus `Application Calendars.ReadWrite` either enabled now or explicitly deferred
- [ ] One known in-scope mailbox for positive testing
- [ ] One known out-of-scope mailbox for negative testing
- [ ] Confirmation that overlapping broad Entra Graph app permissions were reviewed and removed where necessary (Step 5 checklist output)
- [ ] Confirmation that Send As is not configured for the selected workflow

## Verification

Confirm success at each stage by the observable outputs below:

1. **Service principal (Step 1):** the live run returns the service-principal object; a re-run reports "already exists" as an informational no-op with no error and no duplicate.
2. **Management scope (Step 2):** the live run returns the scope object and emits the direct-membership warning; a re-run reports "already exists".
3. **Role assignments (Step 3):** exactly four summary rows with `Status: Created` (first run) or `Status: AlreadyExists` (re-run); no `Calendars.ReadWrite` row unless `-IncludeCalendarWrite` was set.
4. **Send on Behalf (Step 4):** both wrapper operations complete; a re-run reports the existing permission entry as a no-op.
5. **Scope boundary (Step 6):** `$result.Succeeded` is `$true`, meaning `InScopeAllowed = $true` (assigned roles appear with `InScope = True` for the in-scope mailbox) and `OutOfScopeDenied = $true` (no effective role or `InScope = False` for the out-of-scope mailbox). The entry script exits with code 0.
6. **On failure:** if `$result.Succeeded` is `$false`, read `FailureReason`: for an unexpectedly-in-scope mailbox, re-check the scoping group's direct membership and the Step 5 broad-grant review for a leftover tenant-wide Graph grant; for a denied in-scope mailbox, confirm the role assignments in Step 3 and the mailbox's direct group membership. Correct and re-run Step 6 (the test cmdlet bypasses the propagation cache). Handoff to engineering is blocked until `Succeeded = $true`.

## Source and Citation

All step-by-step instructions above are PowerShell CLI steps; no third-party UI navigation is required, so the MCP-first/web-second UI sourcing order is not applicable. Every CLI step carries a dated Microsoft Learn documentation citation below (`updated_at` records the citation capture date, 2026-07-02, at which these URLs were referenced as the current documentation source for the command used). The internal source for step content is `docs/open-claw-approach.master.md` section 12 (Steps 1-8) and section 13 (Step 3), captured 2026-07-02.

| Step | Command documented | Source URL | updated_at |
|---|---|---|---|
| Prerequisites (roles) | Exchange Online permissions / Application RBAC | https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac | 2026-07-02 |
| Prerequisites (app registration values) | Register an application with the Microsoft identity platform | https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app | 2026-07-02 |
| Step 0 | Connect-ExchangeOnline | https://learn.microsoft.com/en-us/powershell/module/exchange/connect-exchangeonline | 2026-07-02 |
| Step 1 | New-ServicePrincipal | https://learn.microsoft.com/en-us/powershell/module/exchange/new-serviceprincipal | 2026-07-02 |
| Step 2 | New-ManagementScope | https://learn.microsoft.com/en-us/powershell/module/exchange/new-managementscope | 2026-07-02 |
| Step 2 (group DN) | Get-Group | https://learn.microsoft.com/en-us/powershell/module/exchange/get-group | 2026-07-02 |
| Step 3 | New-ManagementRoleAssignment | https://learn.microsoft.com/en-us/powershell/module/exchange/new-managementroleassignment | 2026-07-02 |
| Step 4 | Add-MailboxPermission | https://learn.microsoft.com/en-us/powershell/module/exchange/add-mailboxpermission | 2026-07-02 |
| Step 4 | Set-Mailbox (-GrantSendOnBehalfTo) | https://learn.microsoft.com/en-us/powershell/module/exchange/set-mailbox | 2026-07-02 |
| Step 5 | Review permissions granted to enterprise applications | https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/manage-application-permissions | 2026-07-02 |
| Step 6 | Test-ServicePrincipalAuthorization | https://learn.microsoft.com/en-us/powershell/module/exchange/test-serviceprincipalauthorization | 2026-07-02 |
| Step 7 | Application RBAC handoff values (Application RBAC overview) | https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac | 2026-07-02 |
