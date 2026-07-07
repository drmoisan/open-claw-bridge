# graph-activity-log-purview - Plan

- **Issue:** #124
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07T03-00
- **Status:** Draft
- **Version:** 0.3 (revised post-execution: architecture-boundary seam added as Phase 9; see "Revision Note" below)
- **Work Mode:** full-feature

## Required References

Policy reading order (per `.claude/skills/policy-compliance-order`, C#-only scope):

1. `CLAUDE.md` (auto-loaded standing instructions)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/quality-tiers.md`
6. `.claude/rules/architecture-boundaries.md`

There is no `.claude/rules/csharp-suppressions.md` in this repository (confirmed by glob at
planning time); it is not part of the reading order for this feature.

**All work must comply with these policies; do not duplicate their content here.**

## Binding Design Decisions (from spec.md — not re-litigated by this plan)

1. **Required-field reuse.** No `audit_log` DDL change and no `IActionAuditLog` interface
   change. `MessageId` carries the CloudSync event's subject-resource identifier
   (subscription id / `resourceData.id` / delta-reconcile `requestId`, per event type).
   `ActingFlags` carries the new fixed constant `CloudSyncActingFlags.NotApplicable =
   "N/A:CloudSyncActivity"`.
2. **Webhook correlation id.** A new `Guid.NewGuid().ToString()` is generated once per
   notification item at the top of `NotificationRequestProcessor.ProcessItemAsync`, reused
   at whichever exit point (enqueue or rejection) the item takes.
3. **Rejected webhooks are in scope.** Unknown-subscription, client-state-mismatch, and
   missing-resource-id rejections each emit a `WebhookRejected` audit record with a
   `CloudSyncActivityResultCode` rejection-reason constant.
4. **Purview target shape.** Pinned to the Microsoft Graph `directoryAudit` resource shape
   (`id`, `activityDateTime`, `activityDisplayName`, `category`, `correlationId`,
   `operationType`, `result`, `resultReason`, `initiatedBy`, `targetResources`,
   `additionalDetails`); the mapping is illustrative/aspirational, not verified against a
   live endpoint.

No new fields are added to `ActionAuditRecord` itself — decision 1 resolves the
required-non-empty-field gap entirely by reusing the two existing fields with new
semantic values, and the spec's own "New classes/functions to add" list names only new
classes (`CloudSyncActivityType`, `CloudSyncActivityResultCode`, `CloudSyncActingFlags`,
`PurviewActivityLogRecord`, `PurviewActivityLogProjection`), not a change to
`ActionAuditRecord.cs`.

### Implementation-gap fills made by this plan (not new design decisions)

The four binding decisions above do not enumerate every code branch. This plan fills two
gaps conservatively, consistent with decision 1's intent (reuse existing fields with a
subject-resource identifier) and decision 3's three named rejection reasons:

- `NotificationRequestProcessor.ProcessItemAsync`'s "no `subscriptionId`" branch (the
  earliest guard clause) has neither a subscription id nor `resourceData.id` available.
  This plan uses the fallback chain `item.ResourceData?.Id ?? item.SubscriptionId ??
  "(unresolvable)"` for `MessageId` on that branch only, so `ThrowIfEmpty` is always
  satisfied.
- `GraphSubscriptionManager.HandleLifecycleAsync`'s `Missed` branch needs no direct audit
  emission of its own: it only calls `IDeltaReconcileTrigger.TriggerResyncAsync`, which
  routes to `GraphDeltaReconciler.RunAsync` — already instrumented in Phase 5. Emitting an
  additional audit record in `HandleLifecycleAsync` for this branch would double-count the
  same underlying operation.

## Revision Note — Architecture-Boundary Seam (Post-Execution Revision)

Phases 0–8 below were executed literally as originally written. Execution surfaced a
genuine, blocking architecture-boundary conflict, recorded in full at
`evidence/other/architecture-boundary-conflict.md`: instrumenting `GraphSubscriptionManager`,
`NotificationRequestProcessor`, and `GraphDeltaReconciler` (`OpenClaw.Core.CloudSync`) with a
direct `IActionAuditLog` constructor dependency and the `CloudSyncActivityType`/
`CloudSyncActivityResultCode`/`CloudSyncActingFlags` constants (all `OpenClaw.Core.Agent`)
creates an `OpenClaw.Core.CloudSync -> OpenClaw.Core.Agent` dependency that fails the
pre-existing, issue-#117 `CloudSyncArchitectureBoundaryTests` (`CloudSync_DoesNotDependOnTheAgentPartition`
and `CloudSync_DependsOnlyOnTheAllowedOpenClawSurfaces`).

The binding resolution (Option 2 from the conflict analysis, already decided — not
re-litigated by this plan) is a boundary-preserving interface seam:

- A new narrow port, `ICloudSyncActivityAuditor`, is defined in the bare `OpenClaw.Core`
  namespace (the one non-CloudSync namespace `CloudSyncArchitectureBoundaryTests` explicitly
  allows CloudSync to depend on).
- The three CloudSync classes depend only on this port; they no longer reference
  `IActionAuditLog`, the `CloudSyncActivity*`/`CloudSyncActingFlags` constants, or
  `OpenClaw.Core.Agent` at all.
- An Agent-side adapter, `CloudSyncActivityAuditor` (`OpenClaw.Core.Agent`), implements the
  port and owns the mapping to `ActionAuditRecord`/`IActionAuditLog.RecordAsync` using the
  existing constants and decision-1 field conventions.
- The adapter is registered at the composition root (`src/OpenClaw.Core/Program.cs`, which
  `CloudSyncArchitectureBoundaryTests` explicitly exempts), not in
  `CloudSyncServiceCollectionExtensions.cs`.

This revision is captured entirely in **Phase 9** below, added after Phase 8 without
renumbering or duplicating Phases 0–8. Phase 9 supersedes the outcome (not the task text) of
`P6-T2`, `P8-T3`, `P8-T4`, `P8-T5`, and `P8-T6`, each of which is annotated in place below with
a pointer to the superseding Phase 9 task; those original tasks are left unmodified as an
audit trail of what the literal-execution attempt actually found. `spec.md` decision 1 has
been amended (version 0.2) to record the port + adapter mediation; the rest of the binding
design decisions are unchanged.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture & Policy Reads

- [x] [P0-T1] Read `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, and `.claude/rules/architecture-boundaries.md` in that order, then write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/baseline/phase0-instructions-read.md` with `Timestamp:`, `Policy Order:`, and the explicit list of files read.
  - Acceptance: the evidence file exists with all required fields and lists exactly the five rule files plus the standing `CLAUDE.md` instructions.
- [x] [P0-T2] Run `csharpier check .` from the repository root (global CSharpier tool; do not use `dotnet csharpier` or `dotnet format`) and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/baseline/phase0-baseline-01-csharpier-check.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: evidence file exists with all four fields; `EXIT_CODE` and file count are recorded verbatim from the command output.
- [x] [P0-T3] Run `dotnet build` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/baseline/phase0-baseline-02-dotnet-build.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (warning/error counts per project).
  - Acceptance: evidence file exists with all four fields and states the build succeeded with 0 errors.
- [x] [P0-T4] Run `dotnet test --filter "FullyQualifiedName~ArchitectureBoundary"` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/baseline/phase0-baseline-03-architecture-tests.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (passed/failed/total counts, including `CloudSyncArchitectureBoundaryTests`).
  - Acceptance: evidence file exists with all four fields and records the baseline architecture-test pass count.
- [x] [P0-T5] Run `dotnet test --collect:"XPlat Code Coverage"` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric baseline `OpenClaw.Core` line coverage and branch coverage read from the Cobertura report.
  - Acceptance: evidence file exists with all four fields and records numeric line and branch coverage percentages for `OpenClaw.Core` (not placeholders).

### Phase 1 — CloudSync Activity-Type, Result-Code, and Acting-Flags Constants

- [x] [P1-T1] Create `src/OpenClaw.Core/Agent/Contracts/CloudSyncActivityType.cs` with a `public static class CloudSyncActivityType` (mirroring `SentActionKey`'s const-string pattern) containing exactly seven `public const string` members: `SubscriptionCreated = "subscription-created"`, `SubscriptionRenewed = "subscription-renewed"`, `SubscriptionExpired = "subscription-expired"`, `SubscriptionRemoved = "subscription-removed"`, `WebhookReceived = "webhook-received"`, `WebhookRejected = "webhook-rejected"`, `DeltaReconciliationRun = "delta-reconciliation-run"`.
  - Acceptance: the file exists, compiles, and contains exactly these seven members with these exact literal values.
- [x] [P1-T2] Create `src/OpenClaw.Core/Agent/Contracts/CloudSyncActivityResultCode.cs` with a `public static class CloudSyncActivityResultCode` containing `Success = "success"`, `Failure = "failure"`, `UnknownSubscription = "unknown-subscription"`, `ClientStateMismatch = "client-state-mismatch"`, and `MissingResourceId = "missing-resource-id"`.
  - Acceptance: the file exists, compiles, and contains exactly these five members with these exact literal values (matching decision 3's named rejection reasons verbatim).
- [x] [P1-T3] Create `src/OpenClaw.Core/Agent/Contracts/CloudSyncActingFlags.cs` with a `public static class CloudSyncActingFlags` containing `public const string NotApplicable = "N/A:CloudSyncActivity"`.
  - Acceptance: the file exists, compiles, and the constant's literal value matches decision 1 verbatim.
- [x] [P1-T4] Create `tests/OpenClaw.Core.Tests/Agent/Contracts/CloudSyncActivityConstantsTests.cs` (MSTest + FluentAssertions) with test methods asserting: each `CloudSyncActivityType` constant is a non-empty, distinct string; each `CloudSyncActivityResultCode` constant is a non-empty, distinct string; `CloudSyncActingFlags.NotApplicable` equals `"N/A:CloudSyncActivity"`.
  - Acceptance: the test file exists under `tests/OpenClaw.Core.Tests/Agent/Contracts/` and all its test methods pass.
- [x] [P1-T5] Add a new `[TestMethod]` to `tests/OpenClaw.Core.Tests/CoreCacheRepositoryAuditLogTests.cs` that constructs an `ActionAuditRecord` with `MessageId` set to a subscription id, `ActingFlags = CloudSyncActingFlags.NotApplicable`, and `ActionType = CloudSyncActivityType.SubscriptionCreated`, calls `RecordAsync` then `GetByMessageIdAsync`, and asserts the round-tripped record's `ActingFlags` and `ActionType` are unchanged — proving the existing `audit_log` schema and `IActionAuditLog` interface require no change for CloudSync event types.
  - Acceptance: the new test method exists in `CoreCacheRepositoryAuditLogTests.cs` and passes.
- [x] [P1-T6] Run `dotnet test --filter "FullyQualifiedName~CloudSyncActivityConstantsTests|FullyQualifiedName~CoreCacheRepositoryAuditLogTests"` and confirm exit code 0.
  - Acceptance: command exits 0 with all Phase 1 tests (new and pre-existing) passing.

### Phase 2 — PurviewActivityLogRecord and PurviewActivityLogProjection (Pure Mapping)

- [x] [P2-T1] Create `src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogRecord.cs` with a `public sealed record PurviewActivityLogRecord` carrying the pinned `directoryAudit`-style fields (decision 4): `Id`, `ActivityDateTime`, `ActivityDisplayName`, `Category`, `CorrelationId`, `OperationType`, `Result`, `ResultReason`, `InitiatedBy`, `TargetResources`, `AdditionalDetails`, with XML doc noting the shape is illustrative/aspirational per decision 4.
  - Acceptance: the file exists, compiles, and the record's positional/init properties match the eleven named fields above.
- [x] [P2-T2] Create `src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogProjection.cs` with a `public static class PurviewActivityLogProjection` exposing a pure `public static PurviewActivityLogRecord Project(ActionAuditRecord record)` that maps `RecordedAtUtc → ActivityDateTime`, `ActionType → ActivityDisplayName`/`OperationType`, `CorrelationId → CorrelationId`, `ResultCode → Result`, `ErrorDetail → ResultReason`, `Mailbox`/`MessageId → TargetResources`, is total over every `ActionType`/`ResultCode` value currently in use (existing send/calendar values plus the seven new CloudSync values), and never throws for any valid `ActionAuditRecord`.
  - Acceptance: the file exists, compiles, has no I/O or network dependency, and `Project` is a static method with no side effects.
- [x] [P2-T3] Create `tests/OpenClaw.Core.Tests/Agent/Contracts/PurviewActivityLogProjectionTests.cs` (MSTest + FluentAssertions) covering: each of the seven `CloudSyncActivityType` values maps to a non-empty `ActivityDisplayName`/`OperationType`; a record with `EventId = null` and `ErrorDetail = null` maps without a null-reference fault; an existing send/calendar `ActionType` (e.g., `SentActionKey.ProposalReply`) still maps without throwing.
  - Acceptance: the test file exists under `tests/OpenClaw.Core.Tests/Agent/Contracts/` and all its test methods pass.
- [x] [P2-T4] Create `tests/OpenClaw.Core.Tests/Agent/Contracts/PurviewActivityLogProjectionPropertyTests.cs` using CsCheck with at least one property-based test asserting `Project` never throws and always returns a non-empty `Id`/`ActivityDateTime`/`CorrelationId` across generated `ActionAuditRecord` instances (satisfying the T1 property-test-density obligation for this pure function per `.claude/rules/csharp.md` and `.claude/rules/quality-tiers.md`).
  - Acceptance: the property-test file exists, uses CsCheck, and passes on a fixed/printed seed on failure.
- [x] [P2-T5] Create `tests/OpenClaw.Core.Tests/Agent/Contracts/PurviewActivityLogProjectionContractTests.cs` (mocked-Graph/Purview contract test, AC4) asserting that for representative CloudSync records (one per `CloudSyncActivityType` value) and one representative existing send/calendar record, the projected `PurviewActivityLogRecord`'s populated field set matches the pinned `directoryAudit`-style schema (decision 4) exactly — no missing and no extra fields.
  - Acceptance: the contract-test file exists under `tests/OpenClaw.Core.Tests/Agent/Contracts/` and all its test methods pass.
- [x] [P2-T6] Run `dotnet test --filter "FullyQualifiedName~PurviewActivityLogProjection"` and confirm exit code 0.
  - Acceptance: command exits 0 with all Phase 2 tests passing.

### Phase 3 — GraphSubscriptionManager Instrumentation

- [x] [P3-T1] Add an `IActionAuditLog actionAuditLog` constructor parameter (with `ArgumentNullException.ThrowIfNull` guard and a private readonly field) to `GraphSubscriptionManager` in `src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs`.
  - Acceptance: the file compiles; the constructor throws `ArgumentNullException` when `actionAuditLog` is null.
- [x] [P3-T2] In `CreateAsync`'s success path (`src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs`, after `subscriptionStore.UpsertSubscriptionAsync`), emit an audit record via `actionAuditLog.RecordAsync` with `ActionType = CloudSyncActivityType.SubscriptionCreated`, `MessageId = record.SubscriptionId`, `ActingFlags = CloudSyncActingFlags.NotApplicable`, `CorrelationId = envelope.Meta.RequestId`, `ResultCode = CloudSyncActivityResultCode.Success`, `Mailbox = graphOptions.PrincipalMailboxUpn`.
  - Acceptance: `CreateAsync`'s success path calls `RecordAsync` exactly once with the fields above.
- [x] [P3-T3] In `CreateAsync`'s failure path (the `if (!envelope.Ok) return ...` branch), emit an audit record with `ActionType = CloudSyncActivityType.SubscriptionCreated`, `ResultCode = CloudSyncActivityResultCode.Failure`, `ErrorDetail = envelope.Error?.Message`, `CorrelationId = envelope.Meta.RequestId`, `MessageId` set to `graphOptions.PrincipalMailboxUpn` (no subscription id exists yet on a failed create).
  - Acceptance: `CreateAsync`'s failure path calls `RecordAsync` exactly once with the fields above before returning.
- [x] [P3-T4] In `RenewAsync`'s success path (after `subscriptionStore.UpsertSubscriptionAsync(updated, ...)`), emit an audit record with `ActionType = CloudSyncActivityType.SubscriptionRenewed`, `MessageId = updated.SubscriptionId`, `ResultCode = CloudSyncActivityResultCode.Success`, `CorrelationId = envelope.Meta.RequestId`.
  - Acceptance: `RenewAsync`'s success path calls `RecordAsync` exactly once with the fields above.
- [x] [P3-T5] In both of `RenewAsync`'s failure returns (the `!envelope.Ok` branch and the "stored is null" branch), emit an audit record with `ActionType = CloudSyncActivityType.SubscriptionRenewed`, `ResultCode = CloudSyncActivityResultCode.Failure`, `MessageId = subscriptionId`, `ErrorDetail` set from the corresponding error message, `CorrelationId = envelope.Meta.RequestId`.
  - Acceptance: both `RenewAsync` failure branches call `RecordAsync` exactly once each before returning.
- [x] [P3-T6] In `HandleLifecycleAsync`'s `ReauthorizationRequired` branch, after `subscriptionStore.UpdateSubscriptionStatusAsync`, emit an audit record with `ActionType = CloudSyncActivityType.SubscriptionExpired`, `MessageId = item.SubscriptionId`, `ResultCode = CloudSyncActivityResultCode.Failure`, `ErrorDetail = renewal.Error?.Message`, `CorrelationId = renewal.Meta.RequestId`.
  - Acceptance: the `ReauthorizationRequired`-failure branch calls `RecordAsync` exactly once with the fields above.
- [x] [P3-T7] In `HandleLifecycleAsync`'s `Removed` branch, after `subscriptionStore.DeleteSubscriptionAsync`, generate `var correlationId = Guid.NewGuid().ToString();` inline and emit an audit record with `ActionType = CloudSyncActivityType.SubscriptionRemoved`, `MessageId = item.SubscriptionId`, `ResultCode = CloudSyncActivityResultCode.Success`, `CorrelationId = correlationId`.
  - Acceptance: the `Removed` branch calls `RecordAsync` exactly once with the fields above before recreating the subscription.
- [x] [P3-T8] Update the shared `Manager(...)` test factory in `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerTests.cs` to accept an optional `IActionAuditLog? actionAuditLog = null` parameter, defaulting to a new `FakeActionAuditLog` instance when null, and pass it to the `GraphSubscriptionManager` constructor.
  - Acceptance: `GraphSubscriptionManagerTests.cs` and `GraphSubscriptionManagerLifecycleTests.cs` compile unchanged (no call-site edits required in either file).
- [x] [P3-T9] Create `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerAuditTests.cs` (MSTest + Moq + FluentAssertions) with test methods verifying `CreateAsync` emits exactly one `SubscriptionCreated` audit record with `ResultCode.Success` on a successful Graph response and exactly one with `ResultCode.Failure` on a Graph error response.
  - Acceptance: the test file exists under `tests/OpenClaw.Core.Tests/CloudSync/` and both test methods pass.
- [x] [P3-T10] Add test methods to `GraphSubscriptionManagerAuditTests.cs` verifying `RenewAsync` emits exactly one `SubscriptionRenewed` audit record with `ResultCode.Success` on success and exactly one with `ResultCode.Failure` on a Graph error response.
  - Acceptance: both new test methods pass.
- [x] [P3-T11] Add test methods to `GraphSubscriptionManagerAuditTests.cs` verifying `HandleLifecycleAsync` emits exactly one `SubscriptionExpired` audit record on a failed `ReauthorizationRequired` renewal and exactly one `SubscriptionRemoved` audit record on a `Removed` lifecycle event.
  - Acceptance: both new test methods pass.
- [x] [P3-T12] Run `dotnet test --filter "FullyQualifiedName~GraphSubscriptionManager"` and confirm exit code 0.
  - Acceptance: command exits 0 with all pre-existing and new `GraphSubscriptionManager`-related tests passing.

### Phase 4 — NotificationRequestProcessor Instrumentation

- [x] [P4-T1] Add an `IActionAuditLog actionAuditLog` parameter (with null guard) to `NotificationRequestProcessor`'s primary constructor in `src/OpenClaw.Core/CloudSync/NotificationRequestProcessor.cs`.
  - Acceptance: the file compiles; the class stores the dependency for use by `ProcessItemAsync`.
- [x] [P4-T2] At the top of `ProcessItemAsync`, add `var correlationId = Guid.NewGuid().ToString();` generated once per notification item (decision 2), reused at every exit point of the method.
  - Acceptance: exactly one `Guid.NewGuid()` call exists in `ProcessItemAsync`, and every audit emission added by this phase reads `correlationId`.
- [x] [P4-T3] In the "no `subscriptionId`" guard branch (the first `return` in `ProcessItemAsync`), emit a `WebhookRejected` audit record with `ResultCode = CloudSyncActivityResultCode.UnknownSubscription`, `MessageId = item.ResourceData?.Id ?? item.SubscriptionId ?? "(unresolvable)"`, `CorrelationId = correlationId`.
  - Acceptance: this branch calls `RecordAsync` exactly once with the fields above before returning.
- [x] [P4-T4] In the "subscription not found" branch (`subscription is null`), emit a `WebhookRejected` audit record with `ResultCode = CloudSyncActivityResultCode.UnknownSubscription`, `MessageId = item.SubscriptionId`, `CorrelationId = correlationId`.
  - Acceptance: this branch calls `RecordAsync` exactly once with the fields above before returning.
- [x] [P4-T5] In the `clientState` mismatch branch, emit a `WebhookRejected` audit record with `ResultCode = CloudSyncActivityResultCode.ClientStateMismatch`, `MessageId = item.SubscriptionId`, `CorrelationId = correlationId`.
  - Acceptance: this branch calls `RecordAsync` exactly once with the fields above before returning.
- [x] [P4-T6] At the lifecycle-item `queue.TryEnqueue(...)` call site, emit a `WebhookReceived` audit record with `MessageId = item.SubscriptionId`, `ResultCode = CloudSyncActivityResultCode.Success`, `CorrelationId = correlationId`.
  - Acceptance: this call site calls `RecordAsync` exactly once with the fields above.
- [x] [P4-T7] In the missing-`resourceData.id` guard branch, emit a `WebhookRejected` audit record with `ResultCode = CloudSyncActivityResultCode.MissingResourceId`, `MessageId = item.SubscriptionId`, `CorrelationId = correlationId`.
  - Acceptance: this branch calls `RecordAsync` exactly once with the fields above before returning.
- [x] [P4-T8] At the change-notification `queue.TryEnqueue(...)` call site, emit a `WebhookReceived` audit record with `MessageId = item.ResourceData.Id`, `ResultCode = CloudSyncActivityResultCode.Success`, `CorrelationId = correlationId`.
  - Acceptance: this call site calls `RecordAsync` exactly once with the fields above.
- [x] [P4-T9] Update the three direct `new NotificationRequestProcessor(...)` call sites in `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorTests.cs` to pass a new `FakeActionAuditLog` instance as the fourth constructor argument.
  - Acceptance: `NotificationRequestProcessorTests.cs` compiles and its pre-existing tests pass unchanged.
- [x] [P4-T10] Update the four direct `new NotificationRequestProcessor(...)` call sites in `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorEdgeTests.cs` to pass a new `FakeActionAuditLog` instance as the fourth constructor argument.
  - Acceptance: `NotificationRequestProcessorEdgeTests.cs` compiles and its pre-existing tests pass unchanged.
- [x] [P4-T11] Create `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorAuditTests.cs` (MSTest + Moq + FluentAssertions) verifying: a valid lifecycle notification emits exactly one `WebhookReceived` audit record with a freshly generated correlation id; a valid change notification emits exactly one `WebhookReceived` audit record with `MessageId` equal to `resourceData.id`.
  - Acceptance: the test file exists under `tests/OpenClaw.Core.Tests/CloudSync/` and both test methods pass.
- [x] [P4-T12] Add test methods to `NotificationRequestProcessorAuditTests.cs` verifying each of the three rejection branches (no subscriptionId, unknown subscription, clientState mismatch, missing resourceData.id — four scenarios) emits exactly one `WebhookRejected` audit record with the matching `CloudSyncActivityResultCode`.
  - Acceptance: all four rejection-scenario test methods pass.
- [x] [P4-T13] Run `dotnet test --filter "FullyQualifiedName~NotificationRequestProcessor"` and confirm exit code 0.
  - Acceptance: command exits 0 with all pre-existing and new `NotificationRequestProcessor`-related tests passing.

### Phase 5 — GraphDeltaReconciler Instrumentation

- [x] [P5-T1] Add an `IActionAuditLog actionAuditLog` constructor parameter (with null guard) to `GraphDeltaReconciler` in `src/OpenClaw.Core/CloudSync/GraphDeltaReconciler.cs`.
  - Acceptance: the file compiles; the constructor throws `ArgumentNullException` when `actionAuditLog` is null.
- [x] [P5-T2] Add a `mailbox` parameter to the private `RecordRunAsync` method and update its three existing call sites within `RunAsync` (the two failure returns and the final success call) to pass the enclosing `mailbox` argument.
  - Acceptance: `RecordRunAsync`'s signature includes `mailbox`; all three call sites compile with the new argument.
- [x] [P5-T3] Inside `RecordRunAsync`, alongside the existing `repository.AddIngestRunAsync(...)` call, emit an audit record via `actionAuditLog.RecordAsync` with `ActionType = CloudSyncActivityType.DeltaReconciliationRun`, `MessageId = requestId`, `CorrelationId = requestId`, `Mailbox = mailbox`, `ActingFlags = CloudSyncActingFlags.NotApplicable`, `ResultCode` mapped from the `outcome` string (`"success"` → `CloudSyncActivityResultCode.Success`, `"failed"` → `CloudSyncActivityResultCode.Failure`), `ErrorDetail = errorMessage`.
  - Acceptance: every call to `RecordRunAsync` (success and both failure paths) results in exactly one `RecordAsync` call with the fields above.
- [x] [P5-T4] Update the shared `Reconciler(...)` test factory in `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerTests.cs` to accept an optional `IActionAuditLog? actionAuditLog = null` parameter, defaulting to a new `FakeActionAuditLog` instance when null, and pass it to the `GraphDeltaReconciler` constructor.
  - Acceptance: `GraphDeltaReconcilerTests.cs` and `GraphDeltaReconcilerRecoveryTests.cs` compile unchanged (no call-site edits required in either file).
- [x] [P5-T5] Create `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerAuditTests.cs` (MSTest + Moq + FluentAssertions) verifying a successful `RunAsync` emits exactly one `DeltaReconciliationRun` audit record with `ResultCode.Success` and `CorrelationId == MessageId == requestId`.
  - Acceptance: the test file exists under `tests/OpenClaw.Core.Tests/CloudSync/` and the test method passes.
- [x] [P5-T6] Add a test method to `GraphDeltaReconcilerAuditTests.cs` verifying a failed `RunAsync` (Graph error on page 1) emits exactly one `DeltaReconciliationRun` audit record with `ResultCode.Failure` and a populated `ErrorDetail`.
  - Acceptance: the new test method passes.
- [x] [P5-T7] Run `dotnet test --filter "FullyQualifiedName~GraphDeltaReconciler"` and confirm exit code 0.
  - Acceptance: command exits 0 with all pre-existing and new `GraphDeltaReconciler`-related tests passing.

### Phase 6 — Shared Test Double and Regression Verification

- [x] [P6-T1] Add an internal `FakeActionAuditLog` class implementing `IActionAuditLog` to `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncTestDoubles.cs`, recording every `RecordAsync` call in a `List<ActionAuditRecord> Recorded` property for assertions, and returning an empty list from `GetByMessageIdAsync`.
  - Acceptance: the class exists in `CloudSyncTestDoubles.cs`, implements both `IActionAuditLog` methods, and is used by the Phase 3–5 factory defaults and `*AuditTests.cs` files.
- [ ] [P6-T2] Run `dotnet test --filter "FullyQualifiedName~OpenClaw.Core.Tests.CloudSync"` and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/regression-testing/cloudsync-suite-regression.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming every CloudSync test (the 16 pre-existing files plus the three new `*AuditTests.cs` files) passes with zero failures.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
  - **Superseded (architecture-boundary conflict):** left unchecked as an accurate record of the
    literal-execution attempt (it failed against the direct `IActionAuditLog`/`OpenClaw.Core.Agent`
    dependency — see `evidence/regression-testing/cloudsync-suite-regression.md` and
    `evidence/other/architecture-boundary-conflict.md`). The post-revision regression run is
    **P9-T37**.
- [x] [P6-T3] Run `dotnet test --filter "FullyQualifiedName~SchedulingWorkerAuditTests"` and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/regression-testing/f9-audit-suite-regression.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming the existing F9 `SchedulingWorkerAuditTests.cs` suite passes unchanged.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
- [x] [P6-T4] Run `dotnet test --filter "FullyQualifiedName~CoreCacheRepositoryAuditLogTests|FullyQualifiedName~CoreCacheRepositoryAuditLogPropertyTests"` and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/regression-testing/f9-store-suite-regression.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming both F9 store test suites pass, including the new CloudSync round-trip test added in Phase 1.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.

### Phase 7 — Human-Interaction Exception Checkpoint

- [x] [P7-T1] Write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/other/human-interaction-checkpoint.md` recording that live-tenant Purview/Graph activity-log ingestion verification (AC5) requires a `human_interaction` entry with `response: exception` and a non-empty `runbook_path` (recommended path: `docs/features/active/2026-07-07-graph-activity-log-purview-124/runbooks/purview-live-tenant-ingestion.runbook.md`), mirroring the F11 HI-1 / F17 precedent; state explicitly that this plan does not author the runbook content and that the orchestrator must delegate to the `human-exception-runbook` agent separately after this plan's preflight clears.
  - Acceptance: the evidence file exists, names the recommended `runbook_path`, and states the runbook authorship is out of this plan's scope.

### Phase 8 — Final QA Loop

- [x] [P8-T1] Run `csharpier check .` from the repository root; if any file is reported unformatted, run `csharpier format .` and restart the full toolchain loop from step 1. Write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-01-csharpier-check.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0` on the final recorded run.
- [x] [P8-T2] Run `dotnet build` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-02-dotnet-build.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (0 warnings, 0 errors).
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
- [ ] [P8-T3] Run `dotnet test --filter "FullyQualifiedName~ArchitectureBoundary"` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-03-architecture-tests.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming zero new architecture-boundary violations versus the Phase 0 baseline.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
  - **Superseded (architecture-boundary conflict):** left unchecked as an accurate record — this
    command failed 2/4 in `CloudSyncArchitectureBoundaryTests` against the literal Phase 3–5
    instrumentation (see `evidence/other/architecture-boundary-conflict.md`). The post-revision
    re-run is **P9-T36**.
- [ ] [P8-T4] Run `dotnet test --collect:"XPlat Code Coverage"` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-04-dotnet-test-coverage.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric post-change `OpenClaw.Core` line and branch coverage read from the Cobertura report.
  - Acceptance: evidence file exists with all four fields, `EXIT_CODE: 0`, and numeric coverage values (not placeholders).
  - **Superseded (architecture-boundary conflict):** left unchecked; a full-suite coverage run
    over code that fails the architecture-boundary gate is not a valid final-QC coverage
    artifact. The post-revision re-run is **P9-T38**.
- [ ] [P8-T5] Compare the Phase 0 baseline (`evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`) against the Phase 8 post-change coverage (`evidence/qa-gates/final-qa-04-dotnet-test-coverage.md`) and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-05-coverage-delta.md` reporting baseline vs. post-change line/branch percentages, the delta, the >=85%/>=75% threshold check, and per-new-file line/branch coverage for every production file added in Phases 1–5.
  - Acceptance: the evidence file exists, reports numeric baseline and post-change values, and states a PASS/FAIL verdict against both thresholds with no regression on changed lines.
  - **Superseded (architecture-boundary conflict):** un-checked and superseded — this comparison
    was computed against the P8-T4 run, which is itself superseded, and the production file set
    changes materially in Phase 9 (new port + adapter, retargeted classes). The post-revision
    coverage-delta re-check is **P9-T39**.
- [ ] [P8-T6] Confirm the seven-stage toolchain loop (format, lint/analyzers, nullable type-check, architecture tests, unit tests with coverage) completed in a single clean pass with no restart required, and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-06-toolchain-clean-pass.md` recording that confirmation. If any stage required a restart, repeat the loop from stage 1 before writing this artifact.
  - Acceptance: the evidence file exists and states the single-clean-pass confirmation; mutation testing (Stryker.NET) is explicitly noted as out of scope for this per-commit loop per `.claude/rules/general-code-change.md`.
  - **Superseded (architecture-boundary conflict):** left unchecked — stage 4 (architecture-boundary
    tests) did not pass, so no single clean pass occurred. The post-revision confirmation is
    **P9-T40**.

### Phase 9 — Architecture-Boundary Seam Revision

Resolves the blocking finding in `evidence/other/architecture-boundary-conflict.md` via
Option 2 (interface seam + composition-root adapter). See "Revision Note" above. Method
signatures below are binding for this phase's tasks; `mailbox`/`subscriptionId`/`messageId`/
`requestId` are the same identifiers already available at each call site (spec.md decision 1,
now amended). All CancellationToken parameters below are named `ct`.

`ICloudSyncActivityAuditor` (bare `OpenClaw.Core` namespace) exposes exactly these seven
methods, one per `CloudSyncActivityType` value:

- `Task RecordSubscriptionCreatedAsync(string mailbox, string? subscriptionId, string? correlationId, bool success, string? errorDetail, CancellationToken ct)`
- `Task RecordSubscriptionRenewedAsync(string mailbox, string subscriptionId, string? correlationId, bool success, string? errorDetail, CancellationToken ct)`
- `Task RecordSubscriptionExpiredAsync(string mailbox, string subscriptionId, string? correlationId, string? errorDetail, CancellationToken ct)`
- `Task RecordSubscriptionRemovedAsync(string mailbox, string subscriptionId, string correlationId, CancellationToken ct)`
- `Task RecordWebhookReceivedAsync(string mailbox, string messageId, string correlationId, CancellationToken ct)`
- `Task RecordWebhookRejectedAsync(string mailbox, string messageId, string rejectionReasonCode, string correlationId, CancellationToken ct)`
- `Task RecordDeltaReconciliationRunAsync(string mailbox, string requestId, bool success, string? errorDetail, CancellationToken ct)`

**Sub-group A — Port, adapter, adapter tests**

- [ ] [P9-T1] Create `src/OpenClaw.Core/ICloudSyncActivityAuditor.cs` with an `internal interface ICloudSyncActivityAuditor` in the bare `OpenClaw.Core` namespace (file-scoped `namespace OpenClaw.Core;`, no sub-namespace) containing exactly the seven method signatures listed above, each with an XML doc comment naming the `CloudSyncActivityType` value it records.
  - Acceptance: the file exists, compiles, the namespace is exactly `OpenClaw.Core` (verified by inspection — not `OpenClaw.Core.Agent`, not `OpenClaw.Core.CloudSync`), and the interface declares exactly these seven members.
- [ ] [P9-T2] Create `src/OpenClaw.Core/Agent/Contracts/CloudSyncActivityAuditor.cs` with an `internal sealed class CloudSyncActivityAuditor : ICloudSyncActivityAuditor` in namespace `OpenClaw.Core.Agent`, constructor `(IActionAuditLog actionAuditLog, TimeProvider timeProvider)` with `ArgumentNullException.ThrowIfNull` guards on both, implementing all seven interface methods by constructing an `ActionAuditRecord` and calling `actionAuditLog.RecordAsync`: `ActingFlags = CloudSyncActingFlags.NotApplicable` in every method; `ActionType` set to the matching `CloudSyncActivityType` constant per method; `ResultCode` derived from the `success`/`errorDetail` parameters (`CloudSyncActivityResultCode.Success`/`.Failure`, or the literal `rejectionReasonCode` for `RecordWebhookRejectedAsync`); `MessageId` set to `subscriptionId ?? mailbox` for `RecordSubscriptionCreatedAsync` and to the corresponding id parameter for every other method; `CorrelationId` set to the `correlationId`/`requestId` parameter; `RecordedAtUtc = timeProvider.GetUtcNow()`.
  - Acceptance: the file exists, compiles, implements all seven interface methods, and every method calls `actionAuditLog.RecordAsync` exactly once with the field mapping above.
- [ ] [P9-T3] Create `tests/OpenClaw.Core.Tests/Agent/Contracts/CloudSyncActivityAuditorTests.cs` (MSTest + Moq + FluentAssertions) with a `Mock<IActionAuditLog>` and a `FakeTimeProvider`, containing one test method per port method (seven total) asserting the adapter calls `RecordAsync` exactly once with an `ActionAuditRecord` whose `ActionType`/`ResultCode`/`ActingFlags`/`MessageId`/`CorrelationId` match the mapping in P9-T2, plus one additional test method asserting that for `RecordDeltaReconciliationRunAsync(mailbox, requestId, ...)` the resulting record's `CorrelationId` and `MessageId` are both equal to the supplied `requestId` (the CorrelationId==MessageId invariant, now validated here rather than at the CloudSync call site).
  - Acceptance: the test file exists under `tests/OpenClaw.Core.Tests/Agent/Contracts/` and contains at least eight test methods, all passing.
- [ ] [P9-T4] Run `dotnet test --filter "FullyQualifiedName~CloudSyncActivityAuditorTests"` and confirm exit code 0.
  - Acceptance: command exits 0 with all `CloudSyncActivityAuditorTests` methods passing.

**Sub-group B — Shared test-double addition**

- [ ] [P9-T5] Add an internal `NoOpCloudSyncActivityAuditor : ICloudSyncActivityAuditor` class to `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncTestDoubles.cs` whose seven methods each return `Task.CompletedTask` without recording anything, for use as the default audit-port dependency in tests that do not assert on audit behavior.
  - Acceptance: the class exists in `CloudSyncTestDoubles.cs`, implements all seven `ICloudSyncActivityAuditor` methods, and compiles.

**Sub-group C — Retarget `GraphSubscriptionManager`**

- [ ] [P9-T6] In `src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs`, replace the `IActionAuditLog actionAuditLog` constructor parameter/field with `ICloudSyncActivityAuditor activityAuditor` (same null-guard pattern), and remove the `using OpenClaw.Core.Agent;` import.
  - Acceptance: the file compiles; the constructor throws `ArgumentNullException` when `activityAuditor` is null; no `OpenClaw.Core.Agent` using directive remains in the file.
- [ ] [P9-T7] In `CreateAsync`'s failure path, replace the `actionAuditLog.RecordAsync(new ActionAuditRecord(...))` call with `await activityAuditor.RecordSubscriptionCreatedAsync(graphOptions.PrincipalMailboxUpn, subscriptionId: null, envelope.Meta.RequestId, success: false, envelope.Error?.Message, ct);`.
  - Acceptance: this branch calls `RecordSubscriptionCreatedAsync` exactly once with these arguments before returning.
- [ ] [P9-T8] In `CreateAsync`'s success path, replace the audit call with `await activityAuditor.RecordSubscriptionCreatedAsync(graphOptions.PrincipalMailboxUpn, record.SubscriptionId, envelope.Meta.RequestId, success: true, errorDetail: null, ct);`.
  - Acceptance: this branch calls `RecordSubscriptionCreatedAsync` exactly once with these arguments.
- [ ] [P9-T9] In `RenewAsync`'s `!envelope.Ok` failure branch, replace the audit call with `await activityAuditor.RecordSubscriptionRenewedAsync(graphOptions.PrincipalMailboxUpn, subscriptionId, envelope.Meta.RequestId, success: false, envelope.Error?.Message, ct);`.
  - Acceptance: this branch calls `RecordSubscriptionRenewedAsync` exactly once with these arguments before returning.
- [ ] [P9-T10] In `RenewAsync`'s "stored is null" failure branch, replace the audit call with `await activityAuditor.RecordSubscriptionRenewedAsync(graphOptions.PrincipalMailboxUpn, subscriptionId, envelope.Meta.RequestId, success: false, noRecordError.Message, ct);`.
  - Acceptance: this branch calls `RecordSubscriptionRenewedAsync` exactly once with these arguments before returning.
- [ ] [P9-T11] In `RenewAsync`'s success path, replace the audit call with `await activityAuditor.RecordSubscriptionRenewedAsync(graphOptions.PrincipalMailboxUpn, updated.SubscriptionId, envelope.Meta.RequestId, success: true, errorDetail: null, ct);`.
  - Acceptance: this branch calls `RecordSubscriptionRenewedAsync` exactly once with these arguments.
- [ ] [P9-T12] In `HandleLifecycleAsync`'s `ReauthorizationRequired`-failure branch, replace the audit call with `await activityAuditor.RecordSubscriptionExpiredAsync(graphOptions.PrincipalMailboxUpn, item.SubscriptionId, renewal.Meta.RequestId, renewalError.Message, ct);`.
  - Acceptance: this branch calls `RecordSubscriptionExpiredAsync` exactly once with these arguments.
- [ ] [P9-T13] In `HandleLifecycleAsync`'s `Removed` branch, replace the audit call with `await activityAuditor.RecordSubscriptionRemovedAsync(graphOptions.PrincipalMailboxUpn, item.SubscriptionId, correlationId, ct);` (reusing the existing inline-generated `correlationId`).
  - Acceptance: this branch calls `RecordSubscriptionRemovedAsync` exactly once with these arguments before recreating the subscription.
- [ ] [P9-T14] Update the shared `Manager(...)` test factory in `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerTests.cs`: replace the `OpenClaw.Core.Agent.IActionAuditLog? actionAuditLog = null` parameter with `ICloudSyncActivityAuditor? activityAuditor = null`, defaulting to `activityAuditor ?? new NoOpCloudSyncActivityAuditor()`, and pass it as the constructor's last argument.
  - Acceptance: `GraphSubscriptionManagerTests.cs` and `GraphSubscriptionManagerLifecycleTests.cs` compile unchanged (no other call-site edits required in either file).
- [ ] [P9-T15] Rewrite `tests/OpenClaw.Core.Tests/CloudSync/GraphSubscriptionManagerAuditTests.cs` so each test method constructs a `Mock<ICloudSyncActivityAuditor>()`, passes `mock.Object` to `GraphSubscriptionManagerTests.Manager(..., activityAuditor: mock.Object)`, and replaces the prior `FakeActionAuditLog`/`.Recorded` assertions with `mock.Verify(...)` calls against the matching port method and arguments (e.g. `CreateAsync` success verifies `RecordSubscriptionCreatedAsync("paula@contoso.com", "sub-audit-1", It.IsAny<string?>(), true, null, It.IsAny<CancellationToken>())` called `Times.Once()`; the `Removed`-lifecycle test verifies `RecordSubscriptionRemovedAsync("paula@contoso.com", "sub-1", It.IsAny<string>(), It.IsAny<CancellationToken>())` called `Times.Once()`), preserving every test method's original scenario and asserted outcome.
  - Acceptance: the file compiles and all six existing test methods pass against the port-based mock.
- [ ] [P9-T16] Run `dotnet test --filter "FullyQualifiedName~GraphSubscriptionManager"` and confirm exit code 0.
  - Acceptance: command exits 0 with all pre-existing and revised `GraphSubscriptionManager`-related tests passing.

**Sub-group D — Retarget `NotificationRequestProcessor`**

- [ ] [P9-T17] In `src/OpenClaw.Core/CloudSync/NotificationRequestProcessor.cs`, replace the `IActionAuditLog actionAuditLog` primary-constructor parameter/field with `ICloudSyncActivityAuditor activityAuditor` (same null-guard pattern), and remove the `using OpenClaw.Core.Agent;` import.
  - Acceptance: the file compiles; the class stores `activityAuditor` for use by `ProcessItemAsync`'s helpers; no `OpenClaw.Core.Agent` using directive remains in the file.
- [ ] [P9-T18] Replace the private `RecordWebhookReceivedAsync(mailbox, messageId, correlationId, ct)` helper's `ActionAuditRecord` construction with a direct call: `activityAuditor.RecordWebhookReceivedAsync(mailbox, messageId, correlationId, ct)`.
  - Acceptance: the helper delegates directly to the port method with the same three arguments plus `ct`; no `ActionAuditRecord` construction remains in this helper.
- [ ] [P9-T19] Replace the private `RecordWebhookRejectedAsync(mailbox, messageId, resultCode, correlationId, ct)` helper's `ActionAuditRecord` construction with a direct call: `activityAuditor.RecordWebhookRejectedAsync(mailbox, messageId, resultCode, correlationId, ct)`.
  - Acceptance: the helper delegates directly to the port method with the same arguments; no `ActionAuditRecord` construction remains in this helper.
- [ ] [P9-T20] Update the three direct `new NotificationRequestProcessor(...)` call sites in `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorTests.cs` to pass `new NoOpCloudSyncActivityAuditor()` instead of `new FakeActionAuditLog()` as the fourth constructor argument.
  - Acceptance: `NotificationRequestProcessorTests.cs` compiles and its pre-existing tests pass unchanged.
- [ ] [P9-T21] Update the four direct `new NotificationRequestProcessor(...)` call sites in `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorEdgeTests.cs` to pass `new NoOpCloudSyncActivityAuditor()` instead of `new FakeActionAuditLog()` as the fourth constructor argument.
  - Acceptance: `NotificationRequestProcessorEdgeTests.cs` compiles and its pre-existing tests pass unchanged.
- [ ] [P9-T22] Rewrite `tests/OpenClaw.Core.Tests/CloudSync/NotificationRequestProcessorAuditTests.cs`: update the local `NewProcessor(...)` factory to accept a `Mock<ICloudSyncActivityAuditor>` and pass `mock.Object` as the fourth constructor argument, and replace each `auditLog.Recorded`/`ContainSingle` assertion with a `mock.Verify(...)` call against the matching port method (`RecordWebhookReceivedAsync`/`RecordWebhookRejectedAsync`) and its previously-asserted arguments (e.g. the lifecycle-notification test verifies `RecordWebhookReceivedAsync("paula@contoso.com", "sub-1", It.Is<string>(c => !string.IsNullOrWhiteSpace(c)), It.IsAny<CancellationToken>())`; the missing-`resourceData.id` test verifies `RecordWebhookRejectedAsync(It.IsAny<string>(), "sub-1", CloudSyncActivityResultCode.MissingResourceId, It.IsAny<string>(), It.IsAny<CancellationToken>())`), preserving every test method's original scenario.
  - Acceptance: the file compiles and all six existing test methods pass against the port-based mock.
- [ ] [P9-T23] Update `tests/OpenClaw.Core.Tests/CloudSync/GraphNotificationsEndpointTests.cs`'s `StartHostAsync` DI setup: replace `builder.Services.AddSingleton<IActionAuditLog>(new FakeActionAuditLog());` with `builder.Services.AddSingleton<ICloudSyncActivityAuditor>(new NoOpCloudSyncActivityAuditor());`.
  - Acceptance: the file compiles and its three pre-existing tests pass unchanged.
- [ ] [P9-T24] Run `dotnet test --filter "FullyQualifiedName~NotificationRequestProcessor|FullyQualifiedName~GraphNotificationsEndpoint"` and confirm exit code 0.
  - Acceptance: command exits 0 with all pre-existing and revised tests passing.

**Sub-group E — Retarget `GraphDeltaReconciler`**

- [ ] [P9-T25] In `src/OpenClaw.Core/CloudSync/GraphDeltaReconciler.cs`, replace the `IActionAuditLog actionAuditLog` constructor parameter/field with `ICloudSyncActivityAuditor activityAuditor` (same null-guard pattern), and remove the `using OpenClaw.Core.Agent;` import.
  - Acceptance: the file compiles; the constructor throws `ArgumentNullException` when `activityAuditor` is null; no `OpenClaw.Core.Agent` using directive remains in the file.
- [ ] [P9-T26] Replace the private `RecordRunAsync` method's `ActionAuditRecord` construction with a direct call: `await activityAuditor.RecordDeltaReconciliationRunAsync(mailbox, requestId, success: outcome == "success", errorMessage, CancellationToken.None);`.
  - Acceptance: every call to `RecordRunAsync` (success and both failure paths) results in exactly one `RecordDeltaReconciliationRunAsync` call with these arguments.
- [ ] [P9-T27] Update the shared `Reconciler(...)` test factory in `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerTests.cs`: replace the `OpenClaw.Core.Agent.IActionAuditLog? actionAuditLog = null` parameter with `ICloudSyncActivityAuditor? activityAuditor = null`, defaulting to `activityAuditor ?? new NoOpCloudSyncActivityAuditor()`, and pass it as the constructor's last argument.
  - Acceptance: `GraphDeltaReconcilerTests.cs` and `GraphDeltaReconcilerRecoveryTests.cs` compile unchanged (no other call-site edits required in either file).
- [ ] [P9-T28] Rewrite `tests/OpenClaw.Core.Tests/CloudSync/GraphDeltaReconcilerAuditTests.cs` so each test method constructs a `Mock<ICloudSyncActivityAuditor>()`, passes `mock.Object` to `GraphDeltaReconcilerTests.Reconciler(..., activityAuditor: mock.Object)`, and replaces the prior `FakeActionAuditLog`/`.Recorded` assertions with `mock.Verify(a => a.RecordDeltaReconciliationRunAsync(Mailbox, It.Is<string>(id => !string.IsNullOrWhiteSpace(id)), true, null, It.IsAny<CancellationToken>()), Times.Once())` for the success test and the analogous `success: false` + non-null `errorDetail` matcher for the failure test.
  - Acceptance: the file compiles and both existing test methods pass against the port-based mock.
- [ ] [P9-T29] Run `dotnet test --filter "FullyQualifiedName~GraphDeltaReconciler"` and confirm exit code 0.
  - Acceptance: command exits 0 with all pre-existing and revised `GraphDeltaReconciler`-related tests passing.

**Sub-group F — Composition root, remaining DI test fixups, boundary re-verification**

- [ ] [P9-T30] In `src/OpenClaw.Core/Program.cs`, add `builder.Services.AddSingleton<ICloudSyncActivityAuditor, OpenClaw.Core.Agent.CloudSyncActivityAuditor>();` immediately after the existing `builder.Services.AddSingleton<IActionAuditLog>(sp => sp.GetRequiredService<CoreCacheRepository>());` registration (currently line 94).
  - Acceptance: the registration line exists in `Program.cs` at the location described; it does not appear in `src/OpenClaw.Core/CloudSync/CloudSyncServiceCollectionExtensions.cs`; `dotnet build` succeeds.
- [ ] [P9-T31] Update `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncServiceCollectionExtensionsTests.cs`'s `BuildProvider` helper: replace `services.AddSingleton<IActionAuditLog>(new FakeActionAuditLog());` with `services.AddSingleton<ICloudSyncActivityAuditor>(new NoOpCloudSyncActivityAuditor());`.
  - Acceptance: the file compiles and all its pre-existing tests pass unchanged.
- [ ] [P9-T32] Inspect (via `Grep` over `src/OpenClaw.Core/CloudSync/**/*.cs`) that no production type in `OpenClaw.Core.CloudSync` references `OpenClaw.Core.Agent` in any `using` directive or fully-qualified name, and record the confirmation inline in this task's completion note.
  - Acceptance: zero matches for `OpenClaw.Core.Agent` under `src/OpenClaw.Core/CloudSync/`.
- [ ] [P9-T33] Remove the now-unused `FakeActionAuditLog` class from `tests/OpenClaw.Core.Tests/CloudSync/CloudSyncTestDoubles.cs` (superseded by `NoOpCloudSyncActivityAuditor` and `Mock<ICloudSyncActivityAuditor>` for every CloudSync test consumer).
  - Acceptance: the class no longer exists in the file; `dotnet build` and a full-solution `dotnet test` compile with zero remaining references to `FakeActionAuditLog`.

**Sub-group G — Full re-verification**

- [ ] [P9-T34] Run `csharpier check .` from the repository root; if any file is reported unformatted, run `csharpier format .` and restart this sub-group's loop from this step. Write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/p9-qa-01-csharpier-check.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0` on the final recorded run.
- [ ] [P9-T35] Run `dotnet build` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/p9-qa-02-dotnet-build.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (0 warnings, 0 errors).
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
- [ ] [P9-T36] Run `dotnet test --filter "FullyQualifiedName~ArchitectureBoundary"` and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/p9-qa-03-architecture-tests.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming `CloudSyncArchitectureBoundaryTests` passes 4/4 (all architecture-boundary tests in the solution pass, `EXIT_CODE: 0`). Supersedes P8-T3.
  - Acceptance: evidence file exists with all four fields, `EXIT_CODE: 0`, and states the 4/4 `CloudSyncArchitectureBoundaryTests` pass count explicitly.
- [ ] [P9-T37] Run `dotnet test --filter "FullyQualifiedName~OpenClaw.Core.Tests.CloudSync"` and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/regression-testing/p9-cloudsync-suite-regression.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming every CloudSync test passes with zero failures. Supersedes P6-T2.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
- [ ] [P9-T38] Run `dotnet test --collect:"XPlat Code Coverage"` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/p9-qa-04-dotnet-test-coverage.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric post-revision `OpenClaw.Core` line and branch coverage read from the Cobertura report. Supersedes P8-T4.
  - Acceptance: evidence file exists with all four fields, `EXIT_CODE: 0`, and numeric coverage values (not placeholders).
- [ ] [P9-T39] Compare the Phase 0 baseline (`evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`) against the P9-T38 post-revision coverage and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/p9-qa-05-coverage-delta.md` reporting baseline vs. post-revision line/branch percentages, the delta, the >=85%/>=75% threshold check, and per-file line/branch coverage for `ICloudSyncActivityAuditor.cs` and `CloudSyncActivityAuditor.cs`. Supersedes P8-T5.
  - Acceptance: the evidence file exists, reports numeric baseline and post-revision values, and states a PASS/FAIL verdict against both thresholds with no regression on changed lines.
- [ ] [P9-T40] Confirm the toolchain loop (format, lint/analyzers, nullable type-check, architecture tests, unit tests with coverage) completed in a single clean pass across P9-T34–P9-T39 with no restart required, and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/p9-qa-06-toolchain-clean-pass.md` recording that confirmation. If any stage required a restart, repeat the loop from P9-T34 before writing this artifact. Supersedes P8-T6.
  - Acceptance: the evidence file exists and states the single-clean-pass confirmation.
- [ ] [P9-T41] Confirm `spec.md`'s Design Decisions decision 1 has been amended (version 0.2) to record the `ICloudSyncActivityAuditor` port + `CloudSyncActivityAuditor` adapter mediation, and that the remaining three binding decisions are textually unchanged from version 0.1.
  - Acceptance: `spec.md` decision 1 contains the boundary-preserving-mediation paragraph; decisions 2–4 are unchanged; `spec.md`'s `Version` field reads `0.2`.

## Acceptance Criteria Mapping

| issue.md / user-story.md AC | Plan tasks |
|---|---|
| AC1 — F9 audit seam extended with CloudSync activity event types, correlation id, no `IActionAuditLog` contract change | P1-T1–P1-T3, P1-T5 |
| AC2 — F14 CloudSync components emit activity events additively; existing F14 test suite passes unchanged | P3-T1–P3-T12, P4-T1–P4-T13, P5-T1–P5-T7, P6-T2 (superseded), **P9-T6–P9-T29, P9-T37** |
| AC3 — host-neutral, pure Purview-activity-log projection, testable without network access | P2-T1–P2-T3 |
| AC4 — mocked-Graph/Purview contract tests cover projection mapping and event-emission paths; coverage thresholds held | P2-T4, P2-T5, P3-T9–P3-T11, P4-T11–P4-T12, P5-T5–P5-T6, P8-T4/P8-T5 (superseded), **P9-T3–P9-T4, P9-T38–P9-T39** |
| AC5 — human runbook + `human_interaction` exception with valid `runbook_path` | P7-T1 |
| Architecture boundary (issue #117 AC-4, re-verified after this revision) | P9-T1–P9-T2, P9-T30–P9-T33, P9-T36 |

## Test Plan

- Unit: Phase 1 constants tests, Phase 2 projection tests, Phase 3–5 `*AuditTests.cs` files
  (revised in Phase 9 to assert against `Mock<ICloudSyncActivityAuditor>`), and the new
  `CloudSyncActivityAuditorTests.cs` (Phase 9, adapter mapping coverage).
- Property-based: `PurviewActivityLogProjectionPropertyTests.cs` (CsCheck, T1 obligation).
- Contract: `PurviewActivityLogProjectionContractTests.cs` (mocked-Graph/Purview shape match).
- Regression: `evidence/regression-testing/cloudsync-suite-regression.md` (Phase 6, superseded),
  `f9-audit-suite-regression.md`, `f9-store-suite-regression.md` (Phase 6),
  `evidence/regression-testing/p9-cloudsync-suite-regression.md` (Phase 9, authoritative).
- Architecture boundary: `evidence/qa-gates/final-qa-03-architecture-tests.md` (Phase 8,
  superseded — recorded the blocking failure), `evidence/qa-gates/p9-qa-03-architecture-tests.md`
  (Phase 9, authoritative post-revision result).
- Coverage evidence:
  - Baseline: `evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`
  - Post-change (Phase 8, superseded — not run against passing architecture gate):
    `evidence/qa-gates/final-qa-04-dotnet-test-coverage.md` / `final-qa-05-coverage-delta.md`
  - Post-revision (Phase 9, authoritative): `evidence/qa-gates/p9-qa-04-dotnet-test-coverage.md`
    / `evidence/qa-gates/p9-qa-05-coverage-delta.md`

## Open Questions / Notes

- The two implementation-gap fills documented above (webhook `MessageId` fallback chain for
  the no-`subscriptionId` branch; no separate audit emission for the `Missed` lifecycle
  branch) are plan-level fills, not new design decisions — they do not re-litigate any of
  the four binding decisions in spec.md; both fills survive the Phase 9 seam revision
  unchanged (they concern which identifier is passed to the port, not the boundary the port
  crosses).
- The human-tenant Purview ingestion runbook itself is out of this plan's scope (Phase 7
  records the checkpoint only); it is delegated to the `human-exception-runbook` agent by
  the orchestrator after this plan's preflight clears.
- **Resolved (Phase 9):** the architecture-boundary conflict recorded in
  `evidence/other/architecture-boundary-conflict.md` is resolved by the
  `ICloudSyncActivityAuditor` port + `CloudSyncActivityAuditor` adapter seam. See the
  "Revision Note" near the top of this plan for the full account.
