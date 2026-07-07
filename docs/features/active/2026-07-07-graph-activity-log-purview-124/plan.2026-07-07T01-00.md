# graph-activity-log-purview - Plan

- **Issue:** #124
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07T01-00
- **Status:** Draft
- **Version:** 0.2
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
- [x] [P6-T3] Run `dotnet test --filter "FullyQualifiedName~SchedulingWorkerAuditTests"` and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/regression-testing/f9-audit-suite-regression.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming the existing F9 `SchedulingWorkerAuditTests.cs` suite passes unchanged.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
- [x] [P6-T4] Run `dotnet test --filter "FullyQualifiedName~CoreCacheRepositoryAuditLogTests|FullyQualifiedName~CoreCacheRepositoryAuditLogPropertyTests"` and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/regression-testing/f9-store-suite-regression.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming both F9 store test suites pass, including the new CloudSync round-trip test added in Phase 1.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.

### Phase 7 — Human-Interaction Exception Checkpoint

- [x] [P7-T1] Write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/other/human-interaction-checkpoint.md` recording that live-tenant Purview/Graph activity-log ingestion verification (AC5) requires a `human_interaction` entry with `response: exception` and a non-empty `runbook_path` (recommended path: `docs/features/active/2026-07-07-graph-activity-log-purview-124/runbooks/purview-live-tenant-ingestion.runbook.md`), mirroring the F11 HI-1 / F17 precedent; state explicitly that this plan does not author the runbook content and that the orchestrator must delegate to the `human-exception-runbook` agent separately after this plan's preflight clears.
  - Acceptance: the evidence file exists, names the recommended `runbook_path`, and states the runbook authorship is out of this plan's scope.

### Phase 8 — Final QA Loop

- [ ] [P8-T1] Run `csharpier check .` from the repository root; if any file is reported unformatted, run `csharpier format .` and restart the full toolchain loop from step 1. Write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-01-csharpier-check.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0` on the final recorded run.
- [ ] [P8-T2] Run `dotnet build` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-02-dotnet-build.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (0 warnings, 0 errors).
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
- [ ] [P8-T3] Run `dotnet test --filter "FullyQualifiedName~ArchitectureBoundary"` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-03-architecture-tests.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` confirming zero new architecture-boundary violations versus the Phase 0 baseline.
  - Acceptance: evidence file exists with all four fields and `EXIT_CODE: 0`.
- [ ] [P8-T4] Run `dotnet test --collect:"XPlat Code Coverage"` from the repository root and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-04-dotnet-test-coverage.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric post-change `OpenClaw.Core` line and branch coverage read from the Cobertura report.
  - Acceptance: evidence file exists with all four fields, `EXIT_CODE: 0`, and numeric coverage values (not placeholders).
- [ ] [P8-T5] Compare the Phase 0 baseline (`evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`) against the Phase 8 post-change coverage (`evidence/qa-gates/final-qa-04-dotnet-test-coverage.md`) and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-05-coverage-delta.md` reporting baseline vs. post-change line/branch percentages, the delta, the >=85%/>=75% threshold check, and per-new-file line/branch coverage for every production file added in Phases 1–5.
  - Acceptance: the evidence file exists, reports numeric baseline and post-change values, and states a PASS/FAIL verdict against both thresholds with no regression on changed lines.
- [ ] [P8-T6] Confirm the seven-stage toolchain loop (format, lint/analyzers, nullable type-check, architecture tests, unit tests with coverage) completed in a single clean pass with no restart required, and write `docs/features/active/2026-07-07-graph-activity-log-purview-124/evidence/qa-gates/final-qa-06-toolchain-clean-pass.md` recording that confirmation. If any stage required a restart, repeat the loop from stage 1 before writing this artifact.
  - Acceptance: the evidence file exists and states the single-clean-pass confirmation; mutation testing (Stryker.NET) is explicitly noted as out of scope for this per-commit loop per `.claude/rules/general-code-change.md`.

## Acceptance Criteria Mapping

| issue.md / user-story.md AC | Plan tasks |
|---|---|
| AC1 — F9 audit seam extended with CloudSync activity event types, correlation id, no `IActionAuditLog` contract change | P1-T1–P1-T3, P1-T5 |
| AC2 — F14 CloudSync components emit activity events additively; existing F14 test suite passes unchanged | P3-T1–P3-T12, P4-T1–P4-T13, P5-T1–P5-T7, P6-T2 |
| AC3 — host-neutral, pure Purview-activity-log projection, testable without network access | P2-T1–P2-T3 |
| AC4 — mocked-Graph/Purview contract tests cover projection mapping and event-emission paths; coverage thresholds held | P2-T4, P2-T5, P3-T9–P3-T11, P4-T11–P4-T12, P5-T5–P5-T6, P8-T4, P8-T5 |
| AC5 — human runbook + `human_interaction` exception with valid `runbook_path` | P7-T1 |

## Test Plan

- Unit: Phase 1 constants tests, Phase 2 projection tests, Phase 3–5 `*AuditTests.cs` files.
- Property-based: `PurviewActivityLogProjectionPropertyTests.cs` (CsCheck, T1 obligation).
- Contract: `PurviewActivityLogProjectionContractTests.cs` (mocked-Graph/Purview shape match).
- Regression: `evidence/regression-testing/cloudsync-suite-regression.md`, `f9-audit-suite-regression.md`, `f9-store-suite-regression.md` (Phase 6).
- Coverage evidence:
  - Baseline: `evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`
  - Post-change: `evidence/qa-gates/final-qa-04-dotnet-test-coverage.md`
  - Delta/threshold: `evidence/qa-gates/final-qa-05-coverage-delta.md`

## Open Questions / Notes

- The two implementation-gap fills documented above (webhook `MessageId` fallback chain for
  the no-`subscriptionId` branch; no separate audit emission for the `Missed` lifecycle
  branch) are plan-level fills, not new design decisions — they do not re-litigate any of
  the four binding decisions in spec.md.
- The human-tenant Purview ingestion runbook itself is out of this plan's scope (Phase 7
  records the checkpoint only); it is delegated to the `human-exception-runbook` agent by
  the orchestrator after this plan's preflight clears.
