# Purview/Graph Activity-Log Live-Tenant Verification — Human-Exception Runbook

- **Feature:** graph-activity-log-purview (Issue #124)
- **Runbook path:** `docs/features/active/2026-07-07-graph-activity-log-purview-124/runbooks/purview-activity-log-live-tenant-verification.runbook.md`
- **Authored:** 2026-07-07
- **Source checklist:** feature spec `docs/features/active/2026-07-07-graph-activity-log-purview-124/spec.md` (Design Decisions §4); research `docs/features/active/2026-07-07-graph-activity-log-purview-124/research/2026-07-07T05-10-graph-activity-log-purview-research.md` §3
- **Precedent:** F11 HI-1 runbook `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`; F17 HI-1 runbook `docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md`

## Cue

Act on this runbook when the orchestrator records live-tenant verification of the Purview/Graph activity-log projection as a permitted human exception: `artifacts/orchestration/orchestrator-state.json` contains a `human_interaction.requirements[]` entry with `response: "exception"` and `runbook_path` pointing to this file.

This feature adds `PurviewActivityLogProjection` (`src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogProjection.cs`), a pure, host-neutral mapping from the local `ActionAuditRecord` audit shape to a Microsoft Graph `directoryAudit`-shaped record. The mapping is pinned to the `directoryAudit` resource shape per spec Design Decision 4 and is explicitly aspirational: it is exercised only by mocked-Graph/Purview contract tests in the per-commit suite (`tests/OpenClaw.Core.Tests/Agent/Contracts/PurviewActivityLogProjectionTests.cs`), because no live Microsoft Purview or Graph activity-log endpoint, and no Azure/Exchange/Purview tenant credentials, exist in this environment or in CI. A human administrator with real tenant access performs the confirmation described below: trigger a real CloudSync activity, observe whether a corresponding record appears in the tenant's own audit log, and manually compare its fields against this feature's projection output for the same local `audit_log` row.

## Prerequisites

All of the following must be true before starting.

1. **Test tenant and CloudSync already provisioned.** A build of the `OpenClaw.Core` host that includes this feature, running against a non-production test tenant with F14 CloudSync already enabled and functioning (`OpenClaw:CloudSync:Enabled=true`, a working notification URL, and an already-created Graph change subscription on a test mailbox). If CloudSync is not yet running in the target tenant, complete its own setup first; this runbook does not provision CloudSync.
2. **Tenant administrator role.** The executing administrator holds a role that can both read Entra/Graph directory audit logs and search the Purview unified audit log:
   - **Reports Reader** (Microsoft Entra built-in role) is the least-privileged role that grants `microsoft.directory/auditLogs/allProperties/read`, sufficient for `GET /auditLogs/directoryAudits` and `Get-MgAuditLogDirectoryAudit`. `Security Reader` and `Global Reader` also grant this and may already be held for other purposes.
   - **Audit Logs** or **View-Only Audit Logs** role in the Microsoft Purview portal (and the same-named role in the Exchange admin center, needed to run `Search-UnifiedAuditLog` in Exchange Online PowerShell) is required for the compliance-portal audit search and the PowerShell cmdlet path. These are assigned by default to the **Compliance Management** and **Organization Management** role groups.
   - Confirm unified audit log ingestion is turned on for the tenant: `Get-AdminAuditLogConfig | Format-List UnifiedAuditLogIngestionEnabled` (run in Exchange Online PowerShell) must return `True`.
3. **Graph API permission scope.** The credential used for `GET /auditLogs/directoryAudits` (application or delegated) must carry `AuditLog.Read.All`, the least-privileged permission for this API; `Directory.Read.All` is a higher-privileged alternative and is not required.
4. **Tooling.** Either the Microsoft Graph PowerShell SDK's `Microsoft.Graph.Reports` module (`Get-MgAuditLogDirectoryAudit`) or the Exchange Online Management module (`Search-UnifiedAuditLog`) available on the administrator's workstation, plus a browser session for the Microsoft Purview portal (`https://purview.microsoft.com`). `Connect-MgGraph` (Graph PowerShell SDK) or `Connect-ExchangeOnline` (Exchange Online Management module) must succeed against the target tenant before proceeding.
5. **Host and local access.** Access to start or observe the running `OpenClaw.Core` host against the test tenant, and read access to its `audit_log` table (via `IActionAuditLog.GetByMessageIdAsync`, e.g., through a diagnostic script or debugger, or direct SQLite inspection of the underlying store) so the locally recorded `ActionAuditRecord` for the triggered activity can be retrieved and projected.

## Step-by-step Instructions

Replace every placeholder (tenant name, mailbox UPN, subscription id) with real test-tenant values. Perform these steps only against the designated non-production test tenant.

### Step 1 — Trigger a real CloudSync activity

Pick one of the following triggers; a delta-reconciliation run is the simplest to force on demand.

- **Delta reconciliation (recommended).** Temporarily set `OpenClaw__CloudSync__ReconcileIntervalMinutes=1` on the test host and restart it, or wait for the existing `ReconcileIntervalMinutes` cadence (default 60) to elapse. `DeltaReconciliationWorker` calls `GraphDeltaReconciler.ReconcileAsync` on this cadence for the configured principal mailbox; each run generates one `requestId` (`Guid.NewGuid().ToString()`) and records its outcome.
- **Subscription renewal.** Temporarily set a short `OpenClaw__CloudSync__SubscriptionLifetimeMinutes` (above `RenewalLeadMinutes`, e.g. lifetime `35`, lead `30`) on a newly created test subscription so `GraphSubscriptionManager.RenewAsync` fires within a few minutes, or wait for the natural renewal window on an existing subscription.
- **Webhook receipt.** Send or move a message in the CloudSync-monitored test mailbox while the subscription is active; Microsoft Graph posts a change notification to the configured `NotificationUrl`, which `NotificationRequestProcessor.ProcessItemAsync` processes.

Record the wall-clock time (UTC) the trigger fired; the audit search in Step 3 needs this to narrow the results.

### Step 2 — Retrieve the local audit record and project it

1. Identify the local `ActionAuditRecord.MessageId` for the triggered event: the Graph subscription id (subscription lifecycle events), the notification's `resourceData.id` (webhook events), or the delta-reconcile `requestId` (reconciliation events) — per spec Design Decision 1.
2. Call `IActionAuditLog.GetByMessageIdAsync(messageId, ct)` (or inspect the `audit_log` table directly) to retrieve the recorded `ActionAuditRecord` row, including its `CorrelationId`, `ActionType`, `ResultCode`, and `RecordedAtUtc`.
3. Run `PurviewActivityLogProjection.Project(record)` against that row (for example from a scratch console harness or the existing test project) and record the resulting `PurviewActivityLogRecord` fields: `activityDisplayName`, `category`, `correlationId`, `operationType`, `result`, `resultReason`, `targetResources`, `additionalDetails`.

### Step 3 — Search the real tenant audit log for the same activity

Use either path; both search the same underlying unified audit log.

- **Microsoft Purview portal.** Sign in to `https://purview.microsoft.com`, select the **Audit** solution card (under **View all solutions → Core** if not pinned), and on the **Search** page set the UTC date/time range around the Step 1 trigger time, optionally filter by **Users** (the CloudSync service identity) or **Record types**, then select **Search**. Open the completed search job and inspect matching entries in the job details dashboard.
- **PowerShell (`Search-UnifiedAuditLog`).** Connect to Exchange Online PowerShell (`Connect-ExchangeOnline`) and run:
  ```powershell
  Search-UnifiedAuditLog -StartDate <trigger-time-minus-1h> -EndDate <trigger-time-plus-1h> -RecordType AzureActiveDirectory -ResultSize 100
  ```
- **Graph API (`Get-MgAuditLogDirectoryAudit`).** Connect with the Graph PowerShell SDK (`Connect-MgGraph -Scopes "AuditLog.Read.All"`), import `Microsoft.Graph.Reports`, and run:
  ```powershell
  Get-MgAuditLogDirectoryAudit -Filter "activityDateTime ge <trigger-time-minus-1h>"
  ```

Audit record availability after an event is not instantaneous and Microsoft does not guarantee a specific delay; allow up to 60–90 minutes before concluding no record appeared, then re-run the search.

### Step 4 — Cross-check fields against the projection

For the tenant-returned record that corresponds in time to the Step 1 trigger, compare its `activityDisplayName`, `category`, `correlationId`, `operationType`, `result`, `resultReason`, and `targetResources` fields against the Step 2 projection output for the same event. `correlationId` is the most reliable join key when a real Graph/Purview record for this exact CloudSync activity type exists; when the tenant does not yet ingest CloudSync-originated events at all (a plausible outcome, since no Graph/Purview write path ships in this feature), there is no tenant-side record to compare and the finding is "no ingested record observed," not a field mismatch.

## Verification

Verification is complete once one of the two outcomes below is recorded, dated, and attached to this runbook's execution notes (not committed as part of the codebase).

1. **Pass — fields align.** A tenant audit record matching the Step 1 trigger's time window was found, its `correlationId` (or another unambiguous identifier) ties it to the Step 2 local `ActionAuditRecord`, and the compared fields (`activityDisplayName`, `category`, `operationType`, `result`, `targetResources`) are consistent with the `PurviewActivityLogProjection` output, allowing for expected differences in `id` (tenant-assigned vs. locally generated) and `loggedByService`/`additionalDetails` (fields the real API populates that the local projection does not, or vice versa).
2. **Documented gap, not a blocking failure.** No corresponding tenant record was found (this feature ships no direct network call to Purview/Graph — export/shipping is explicitly out of scope per spec Design Decision 4), or a found record's field names/values diverge from the illustrative projection. Record the specific divergence (missing record vs. named field mismatch) as a known-limitation follow-up against this feature, quoting the observed tenant field values. This condition does not block the feature merging under the existing `human_interaction: exception` disposition; it documents that the projection is aspirational pending a future export/shipping path.

## Source and Citation

Per the current repository limitation, no callable MCP documentation tool is wired as a dependency in this repository; `WebFetch` (web-second) is therefore the sole available sourcing mechanism, and it was used to confirm each source below. Each source was fetched on 2026-07-07; the `updated_at` column records the documentation page's own last-updated date as read on that capture date.

The internal source for step content is the feature spec (`spec.md` Design Decision 4) and research file §3, both captured 2026-07-07.

| Step | Documented topic | Source URL | updated_at |
|---|---|---|---|
| Prerequisites 3, Step 3 | `GET /auditLogs/directoryAudits` — least-privileged permission (`AuditLog.Read.All`), supporting built-in roles (Reports Reader, Security Administrator, Security Reader), request/response shape | https://learn.microsoft.com/en-us/graph/api/directoryaudit-list | 2026-07-04 |
| Prerequisites 2 | Microsoft Entra built-in roles reference (Reports Reader / Security Reader / Global Reader audit-log read permissions) | https://learn.microsoft.com/en-us/entra/identity/role-based-access-control/permissions-reference | 2026-07-01 |
| Prerequisites 2, 4, Step 3 | `Search-UnifiedAuditLog` cmdlet, required Exchange Online role (`View-Only Audit Logs`/`Audit Logs`), `UnifiedAuditLogIngestionEnabled` check | https://learn.microsoft.com/en-us/purview/audit-log-search-script | 2026-06-24 |
| Step 3 | Microsoft Purview portal audit search UI navigation (Audit solution card, Search page fields, search job dashboard) | https://learn.microsoft.com/en-us/purview/audit-search | 2026-06-24 |
| Design Decision 4, Step 2 | Microsoft Graph `directoryAudit` resource shape (`id`, `activityDateTime`, `activityDisplayName`, `category`, `correlationId`, `operationType`, `result`, `resultReason`, `initiatedBy`, `targetResources`, `additionalDetails`) | https://learn.microsoft.com/en-us/graph/api/resources/directoryaudit | 2026-07-07 (per spec Design Decision 4 capture) |
