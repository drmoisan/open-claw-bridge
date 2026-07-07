# Send-on-Behalf Tenant Validation — Human-Exception Runbook

- **Feature:** send-on-behalf-allowlist (Issue #119)
- **Runbook path:** `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md`
- **Authored:** 2026-07-06
- **Source checklist:** `docs/open-claw-approach.master.md` §5.2 (lines ~347-371) and §5.3 (lines ~373-407); precedent `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` (F11 HI-1)

## Cue

Act on this runbook when the orchestrator records tenant-side execution of the send-on-behalf grant, allowlist reconciliation, and rendered-appearance validation as a permitted human exception: `artifacts/orchestration/orchestrator-state.json` contains a `human_interaction.requirements[]` entry with `response: "exception"` and `runbook_path` pointing to this file, following the F11 HI-1 precedent (`.claude/rules/orchestrator-state.md` invariants). The code-side authorization gate for F15 (`SendOnBehalfAuthorizer`, the `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` options key, and the `GraphHostAdapterClient.SendMailAsync` gate) is fully automated and unit-tested against a mocked Graph client; it cannot verify tenant state, because no Azure/Exchange credentials exist in this environment or in CI (an epic non-goal, and the limitation the two-axis-model-selection spec's Out of Scope section also documents for MCP-first sourcing — see the Source and Citation section below). The trigger event is either (a) F15 code has merged and a tenant is being enabled for on-behalf sends for the first time, or (b) an operator is adding or removing a principal mailbox UPN from the deployed `AllowedPrincipalMailboxUpns` configuration and the tenant-side grant must be kept in agreement with it (spec.md D7 and the "Two independent controls must agree" constraint).

## Prerequisites

All of the following must be true before starting:

1. **Administrator role.** The executing administrator holds a role that can run `Set-Mailbox` with `-GrantSendOnBehalfTo` against the target mailboxes — **Organization Management** in Exchange Online, or a delegated **Recipient Management** role assignment scoped to the affected mailboxes. This is the same role class the F11 runbook's Prerequisite 1 documents; if the F11 Application RBAC setup already ran for this tenant, the same administrator can typically perform this runbook.
2. **PowerShell session.** PowerShell 7+, the `ExchangeOnlineManagement` module installed, and an authenticated session established with `Connect-ExchangeOnline` under the role above.
3. **Assistant and principal identities.** The assistant mailbox SMTP address/UPN (the value bound to `OpenClaw:GraphAdapter:AssistantMailboxUpn`) and the set of principal mailbox UPNs this tenant intends the assistant to represent on send.
4. **The application's deployed configuration value.** The current, deployed value of `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` (or its indexed environment-variable form `OpenClaw__GraphAdapter__AllowedPrincipalMailboxUpns__0`, `__1`, ...) for the target environment, obtained from engineering/deployment configuration — not from the tenant. This runbook reconciles tenant grants against that value; it does not set it.
5. **Test recipients and clients.** Access to a test recipient mailbox reachable from both an Outlook desktop client (Windows or Mac, signed in as the recipient) and Outlook on the web (OWA, same recipient), to visually confirm message rendering after the test send in Step 4.
6. **Relationship to the F11 runbook.** This runbook covers the same `Set-Mailbox -GrantSendOnBehalfTo` grant that the F11 runbook's Step 4 (`Set-OpenClawSendOnBehalf`) already documents for the Application-RBAC-scoped setup path. If the F11 module (`OpenClawRbac`) and its `Set-OpenClawSendOnBehalf` wrapper are already available in this repository checkout for this tenant, use that wrapper for Step 1 below and treat the raw `Set-Mailbox` command here as the underlying reference; do not duplicate a second implementation. If the wrapper is not available (for example, a tenant onboarded before F11), use the raw `Set-Mailbox` command in Step 1 directly.

## Step-by-step Instructions

**All step-by-step instructions in this runbook are PowerShell CLI steps, except the visual rendering check in Step 4, which uses the Outlook desktop and Outlook on the web (OWA) client UIs.**

### Step 1 — Grant Send-on-behalf on each principal mailbox (master §5.2, lines ~347-364)

Repeat for each principal mailbox UPN this tenant is enabling:

```powershell
Connect-ExchangeOnline

# Allow the assistant mailbox to access the principal mailbox when needed
Add-MailboxPermission `
    -Identity "executive@contoso.com" `
    -User "assistant@contoso.com" `
    -AccessRights FullAccess `
    -InheritanceType All `
    -AutoMapping $false

# Grant Send on behalf to the assistant mailbox
Set-Mailbox `
    -Identity "executive@contoso.com" `
    -GrantSendOnBehalfTo @{Add="assistant@contoso.com"}
```

Use the `@{Add=...}` form (not a plain replace-all string) so that granting one additional principal mailbox does not remove existing delegates from `GrantSendOnBehalfTo` on that mailbox.

> **Rule (master §5.2, verbatim intent):** "Do not grant Send As for this workflow." Do not run `Add-ADPermission ... -ExtendedRights "Send As"` for the assistant mailbox against any principal mailbox in this feature's scope. If Send As and Send on Behalf are both present on the same mailbox for the same delegate, Exchange always uses Send As, silently defeating the transparency goal (Microsoft Learn, "Manage permissions for recipients," cited below).

If the F11 `OpenClawRbac` module is available for this tenant, the equivalent wrapper call is:

```powershell
Import-Module ./scripts/powershell/modules/OpenClawRbac/OpenClawRbac.psd1
Set-OpenClawSendOnBehalf -PrincipalMailbox 'executive@contoso.com' -AssistantMailbox 'assistant@contoso.com' -WhatIf
Set-OpenClawSendOnBehalf -PrincipalMailbox 'executive@contoso.com' -AssistantMailbox 'assistant@contoso.com'
```

(See F11 runbook Step 4, `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`, for the wrapper's idempotency and dry-run behavior. This runbook does not restate that content beyond this cross-reference.)

### Step 2 — Confirm the grant landed (Exchange-side check)

```powershell
Get-Mailbox -Identity "executive@contoso.com" | Format-List GrantSendOnBehalfTo
```

Confirm the assistant mailbox UPN appears in the returned `GrantSendOnBehalfTo` list for every principal mailbox granted in Step 1.

### Step 3 — Reconcile the tenant grant against the application's allowlist configuration

The tenant-side grant (Steps 1-2) and the application's `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` configuration (Prerequisite 4) are two independent controls that must agree (spec.md D7 and "Two independent controls must agree"). For each principal mailbox in scope, confirm both directions:

- [ ] Every principal UPN granted `Set-Mailbox -GrantSendOnBehalfTo` in Step 1 also appears in the deployed `AllowedPrincipalMailboxUpns` list (comparison is case-insensitive, trimmed, matching the application's own comparison rule in spec.md).
- [ ] Every principal UPN present in the deployed `AllowedPrincipalMailboxUpns` list also has the tenant-side grant confirmed in Step 2.
- [ ] No stray entries exist on either side: an allowlist entry with no tenant grant will fail at Graph submission time with `ErrorSendAsDenied` (a remote deny); a tenant grant with no allowlist entry will be rejected locally before any request is sent, with `Error.Code == "UNAUTHORIZED"` and `Error.BridgeErrorCode == "SendOnBehalfDenied"` (spec.md, "Error surface"). Both are fail-closed outcomes, but neither is the intended steady state — resolve the mismatch by updating whichever side is behind.

### Step 4 — Send a test on-behalf message and verify the rendered result (master §5.3, line ~407)

1. Using the assistant mailbox's send path (either the application itself with the target principal UPN configured, or a manual test via `Set-Mailbox`-granted delegation in an Outlook client signed in as the assistant mailbox), send a test message from the assistant mailbox on behalf of one allowlisted, tenant-granted principal mailbox to the test recipient mailbox from Prerequisite 5.
2. Open the received message in **Outlook desktop**, signed in as the test recipient. Inspect the sender line above the message body.
3. Confirm the sender line reads in the form **"Assistant on behalf of Executive"** — that is, the assistant mailbox's display name, the literal text "on behalf of," and the principal mailbox's display name. This is the format Microsoft Learn documents for the Send on Behalf permission: the From address shows "*&lt;Delegate&gt;* on behalf of *&lt;MailboxOrGroup&gt;*" (cited below).
4. Open the same received message in **Outlook on the web (OWA)**, signed in as the same test recipient (a second session, or the browser client, so both clients are checked independently — client rendering of delegation metadata has historically differed between Outlook desktop and OWA and both must be checked per-tenant).
5. Confirm OWA also renders the sender line as **"Assistant on behalf of Executive"** in the message list and in the open-message header.

### Step 5 — Confirm the message is not rendered as Send As (transparency requirement)

1. In both Outlook desktop and OWA from Step 4, confirm the sender line is **not** the principal mailbox's address alone with no "on behalf of" qualifier — that rendering would indicate Send As, not Send on Behalf, and would mean the transparency goal (master §5.1) is not being met for this tenant.
2. If either client shows the message as sent directly from the principal mailbox with no delegation indicator, check for a conflicting Send As grant on the same principal mailbox for the same assistant delegate:

   ```powershell
   Get-ADPermission -Identity "executive@contoso.com" | Where-Object {$_.ExtendedRights -like 'Send-As*' -and $_.User -like '*assistant@contoso.com*'}
   ```

3. If a Send As grant is returned, remove it — Send As always takes precedence over Send on Behalf when both are present on the same mailbox for the same delegate (Microsoft Learn, cited below) — then repeat Step 4.

## Verification

Confirm success at each stage by the observable outputs below:

1. **Grant (Step 1):** `Set-Mailbox -GrantSendOnBehalfTo` (or the F11 `Set-OpenClawSendOnBehalf` wrapper) completes without error; a re-run against an already-granted principal is a reported no-op, not an error.
2. **Grant confirmation (Step 2):** `Get-Mailbox ... | Format-List GrantSendOnBehalfTo` lists the assistant mailbox UPN for every principal mailbox granted in Step 1.
3. **Reconciliation (Step 3):** all three checklist items are checked with no unresolved mismatch between the tenant grant list and the deployed `AllowedPrincipalMailboxUpns` configuration.
4. **Rendered appearance (Step 4):** the received test message displays the sender line "Assistant on behalf of Executive" (assistant display name + "on behalf of" + principal display name) in both Outlook desktop and OWA.
5. **Not Send As (Step 5):** `Get-ADPermission` for the principal mailbox returns no Send As grant for the assistant mailbox, and both clients render the delegation qualifier from Step 4 rather than a bare principal-mailbox sender line.
6. **On failure:** if Step 4 or Step 5 does not show the expected rendering, do not proceed to broader rollout for that tenant. Re-check Step 1 (grant present), Step 5 (no conflicting Send As), and allow for Exchange Online permission-propagation latency (up to 30 minutes to 2 hours per the F11 runbook's propagation note) before re-testing.

## Source and Citation

Internal sources: `docs/open-claw-approach.master.md` §5.2 (lines ~347-371, "Selected Configuration") and §5.3 (lines ~373-407, "Selected Send Behavior," including the line-407 instruction to "validate the exact rendered result in Outlook and OWA for your tenant"), captured 2026-07-06; `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/spec.md` (D7, "Constraints & Risks"), captured 2026-07-06; F11 precedent runbook `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` (Step 4 and the HI-1 human-exception pattern), captured 2026-07-06.

Sourcing note (MCP-first / web-second): no callable MCP documentation-retrieval tool is wired as a dependency in this repository at the time this runbook was authored (a repo-wide search found no `mcp__*` tool for this purpose; this gap is documented in the two-axis-model-selection spec's Out of Scope section). Per the human-exception-runbook skill's MCP-first / web-second sourcing rule, the third-party UI and CLI steps below were therefore sourced web-second, directly from current Microsoft Learn documentation via `WebFetch`, and not from training data alone.

| Step | Content documented | Source URL | updated_at |
|---|---|---|---|
| Prerequisites (roles) | Exchange Online recipient-management roles / Organization Management | https://learn.microsoft.com/en-us/exchange/permissions-exo/permissions-exo | 2026-07-06 |
| Step 0 (Connect-ExchangeOnline) | Connect-ExchangeOnline | https://learn.microsoft.com/en-us/powershell/module/exchange/connect-exchangeonline | 2026-07-06 |
| Step 1 | Add-MailboxPermission | https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/add-mailboxpermission | 2026-07-06 |
| Step 1 | Set-Mailbox (-GrantSendOnBehalfTo) | https://learn.microsoft.com/en-us/powershell/module/exchange/set-mailbox | 2026-07-06 |
| Step 1 (Send As prohibition, precedence rule) | Manage permissions for recipients (Send As vs. Send on Behalf, precedence note: "If a user has both Send As and Send on Behalf permissions to a mailbox or group, the Send As permission is always used.") | https://learn.microsoft.com/en-us/exchange/recipients/mailbox-permissions | 2026-07-06 |
| Step 2 | Get-Mailbox | https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-mailbox | 2026-07-06 |
| Step 4 (rendered "on behalf of" format) | Manage permissions for recipients (Send on Behalf description: "The From address of these messages clearly shows that the message was sent by the delegate (\"<Delegate> on behalf of <MailboxOrGroup>\").") | https://learn.microsoft.com/en-us/exchange/recipients/mailbox-permissions | 2026-07-06 |
| Step 5 | Get-AdPermission | https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-adpermission | 2026-07-06 |

Third-party UI note: Step 4's Outlook desktop and OWA rendering checks are observation-only (reading the sender line of a received message) rather than menu navigation; no vendor UI-navigation walkthrough was required beyond confirming the documented rendering format above, which was sourced web-second per the note above.
