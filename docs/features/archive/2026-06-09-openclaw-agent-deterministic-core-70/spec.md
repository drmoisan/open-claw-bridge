# openclaw-agent-deterministic-core — Spec

- **Issue:** #70
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-09T12-02
- **Status:** Draft
- **Version:** 0.1

## Overview

Deliver the middleware-agnostic, deterministic agent components for the OpenClaw scheduling assistant so the same agent logic runs unchanged across the delivery stages defined in `docs/open-claw-approach.master.md` ("Delivery Stages"). The agent never calls Outlook COM or vendor-specific APIs directly; it consumes data through a Graph-shaped seam. In the Local MVP (Stage 0) that seam is backed by `OpenClaw.HostAdapter` over `OpenClaw.MailBridge`; in Product Increment 1 (Stage 1) the same seam is backed by Microsoft Graph. Only the implementation behind the seam changes between stages.

This feature delivers the deterministic decision logic (D1–D4), the configuration model (D5), and the middleware seam plus an orchestrating worker (D6). It does not deliver the HostAdapter/MailBridge data-source work that feeds the agent; that work is tracked in separate issues (#71–#76) and the agent logic is complete and correct regardless of when those land.

- Target users/personas and primary use cases: the mailbox owner (executive) whose inbound meeting traffic is triaged and scheduled deterministically by the agent; the operator who configures policy lists and kill switches; the engineering team who must refine agent logic once against a stable contract and carry it forward unchanged.
- Success metrics or expected impact: the deterministic components (D1–D4) carry zero references to `OpenClaw.MailBridge`, `OpenClaw.HostAdapter`, or any COM type, so the `ISchedulingService` implementation can be swapped from HostAdapter-backed to Graph-backed with no change to D1–D4; deterministic, reproducible triage and scheduling output for identical inputs.

## Behavior

The feature introduces a deterministic pipeline that the new `SchedulingWorker` orchestrates: poll → hydrate context → triage → priority-classify → propose slots. Each stage is a pure function over value-typed inputs except the worker itself, which performs I/O exclusively through `ISchedulingService`.

- Main user flow (happy path):
  1. `SchedulingWorker` obtains a candidate message identifier and calls `ISchedulingService.GetSchedulingMessageAsync` and `GetEventForMessageAsync` to hydrate the Graph-shaped `SchedulingMessageDto` and optional `SchedulingEventDto`.
  2. D1 `MeetingContextNormalizer.Normalize(mailboxUpn, message, event)` produces a `NormalizedMeetingContext` (flat, string-typed).
  3. D2 `TriageEngine.Triage(context, triagePolicy)` returns a `TriageResult` with one of the five decision classes (`IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE`) and the supporting reasons.
  4. When the decision is `AUTO_COORDINATE` or `HUMAN_APPROVAL`, D3 `OwnerPriorityClassifier` assigns a priority (`P0`–`P4`, `ESCALATE_TO_OWNER`, `DIGEST_IGNORED`), `RecurringMeetingClassifier` classifies recurrence (`NON_RECURRING`, `ONE_ON_ONE`, `RECURRING_FORUM`, `RECURRING_OTHER`), and `MovePolicy.CanMove` decides whether an occupied slot may be displaced.
  5. When scheduling is required, D4 `SlotProposer.ProposeTimes(request, mailboxSettings, freeBusy, workingHoursPolicy)` returns an ordered list of `CandidateSlot` values using the injected `TimeProvider`.
- Alternate/edge flows:
  - No associated event (ordinary mail): D1 falls back to message subject and body; downstream stages still execute.
  - Private sensitivity: D2 returns `PRIVATE_BUSY_ONLY`; no meeting semantics are ingested; the priority and proposal layers do not run on its content.
  - Empty subject and body: D2 returns `IGNORE`.
  - `PROTECTED_MEETING` remains protected regardless of requester priority; `PRIVATE_BUSY_ONLY` remains opaque regardless of priority.
  - Kill switches: when `SendEnabled` is `false` the worker does not invoke `ISchedulingService.SendMailAsync`; when `CalendarWriteEnabled` is `false` the worker does not invoke any calendar-write operation. The deterministic pipeline still computes its result for logging and review.
- Error handling and recovery behavior:
  - D1–D4 fail fast on invariant violations (for example a null required argument) and do not silently coerce. Pure functions raise specific exceptions rather than returning ambiguous defaults.
  - The worker isolates I/O errors from `ISchedulingService` and does not let a single message failure halt the loop; failed items are logged with their identifier.
  - Partial upstream data (issues #71–#76 not yet landed) yields a `NormalizedMeetingContext` with empty or default fields; the pipeline produces a deterministic, defensible decision on the available data.

## Inputs / Outputs

- Inputs (CLI flags, files, env vars):
  - Agent policy configuration bound from `appsettings.json` section `OpenClaw:AgentPolicy` (and environment overrides), per OR-2.
  - Runtime data from `ISchedulingService` (`SchedulingMessageDto`, `SchedulingEventDto`, `MailboxSettingsDto`, `FreeBusyScheduleDto`).
  - Current time supplied via injected `TimeProvider` (`FakeTimeProvider` in tests).
- Outputs (artifacts, logs, telemetry):
  - `TriageResult`, `OwnerPriority`, `RecurringMeetingKind`, `MovePolicy` decision, and an ordered `CandidateSlot` list.
  - Structured logs recording each decision and its reasons. Evidence artifacts produced during verification are written to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/<kind>/` per the evidence-and-timestamp conventions.
- Config keys and defaults (`OpenClaw:AgentPolicy`, bound to `AgentPolicyOptions`):
  - Triage policy: `InternalDomains`, `VipOrganizers`, `ProtectedCategories`, `ProtectedSubjectPatterns`, `LargeMeetingThreshold` (default 6).
  - Owner policy: `VipEmails`, `DirectReports`, `Priority1`, `Priority2`, `Priority3`, `InternalDomain`, `EmblemEmailDomain`.
  - Working-hours policy: `NoMeetingBlocks`, `MinNoticeMinutes`, `PreferredDays`.
  - Kill switches: `SendEnabled` (default `false`), `CalendarWriteEnabled` (default `false`).
- Versioning or backward-compatibility constraints:
  - The D6 `ISchedulingService` interface and its DTOs are a T2 contract surface; a breaking change requires a major version bump and a contract/schema compatibility check at the interface boundary.

## API / CLI Surface

The middleware seam is `ISchedulingService` (D6), the single contract the deterministic components depend on through value-typed DTOs.

```csharp
public interface ISchedulingService
{
    Task<SchedulingMessageDto?> GetSchedulingMessageAsync(string messageId, CancellationToken ct);
    Task<SchedulingEventDto?> GetEventForMessageAsync(string messageId, CancellationToken ct);
    Task<IReadOnlyList<SchedulingEventDto>> GetCalendarViewAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<SchedulingEventDto?> GetEventAsync(string eventId, CancellationToken ct);
    Task<MailboxSettingsDto> GetMailboxSettingsAsync(CancellationToken ct);
    Task<FreeBusyScheduleDto> GetFreeBusyAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task SendMailAsync(SendMailRequest request, CancellationToken ct);
}
```

Deterministic component signatures (pure functions):

- `NormalizedMeetingContext Normalize(string mailboxUpn, SchedulingMessageDto message, SchedulingEventDto? event)`
- `int DependencyScorer.Score(NormalizedMeetingContext ctx, TriagePolicy policy)`
- `TriageResult TriageEngine.Triage(NormalizedMeetingContext ctx, TriagePolicy policy)`
- `OwnerPriority OwnerPriorityClassifier.Classify(NormalizedMeetingContext ctx, OwnerSchedulingPolicy policy)`
- `RecurringMeetingKind RecurringMeetingClassifier.Classify(NormalizedMeetingContext ctx, string ownerEmail)`
- `bool MovePolicy.CanMove(NormalizedMeetingContext meeting, string ownerEmail, string requesterEmail, OwnerPriority requestPriority, OwnerSchedulingPolicy policy)`
- `IReadOnlyList<CandidateSlot> SlotProposer.ProposeTimes(SchedulingRequest request, MailboxSettingsDto mailboxSettings, FreeBusyScheduleDto freeBusy, WorkingHoursPolicy policy, TimeProvider timeProvider)`

- Example invocations with expected outputs (concise):
  - A meeting message with `sensitivity=private` → `TriageEngine.Triage` returns `PRIVATE_BUSY_ONLY` with the reason that the event is marked private.
  - An internal, non-private, non-protected meeting with dependency score below 4 → `AUTO_COORDINATE`.
  - An external sender or dependency score >= 4 (below 7, organizer not VIP) → `HUMAN_APPROVAL`.
  - A VIP organizer or dependency score >= 7 → `PROTECTED_MEETING`.
  - Empty subject and body → `IGNORE`.
- Contracts and validation rules:
  - DTO serialization round-trips must produce identical field values (contract/schema test).
  - The dependency score is always non-negative; `Triage` always returns one of the five enum members for any valid input.

## Data & State

- Data transformations and invariants:
  - D1 maps the Graph-shaped `SchedulingMessageDto`/`SchedulingEventDto` into a flat, string-typed `NormalizedMeetingContext`, normalizing email addresses to trimmed lowercase, stripping HTML from body text, and partitioning attendees into required/optional/resource per master Section 9.2.
  - Dependency scoring and triage thresholds follow master Section 9.2 exactly (recurring +2, large meeting +2, resource attendees +1, online meeting +1, protected category +3, protected subject pattern +3, VIP organizer +3, external attendee +2; thresholds: VIP organizer or score >= 7 → `PROTECTED_MEETING`; external sender or score >= 4 → `HUMAN_APPROVAL`; otherwise `AUTO_COORDINATE`).
  - Priority, recurrence classification, and move policy follow master Sections 10.1–10.3. Slot proposal follows master Section 10.4 (30-minute steps inside working hours, excluding no-meeting blocks, free per free/busy, starting at least `MinNoticeMinutes` from now; ordered by day preference then start time).
- Caching or persistence details:
  - This feature introduces no new persistence. The existing `MessagePollingWorker` and `CalendarPollingWorker` continue to populate the local SQLite cache (OR-3). The `SchedulingWorker` reads through `ISchedulingService`.
- Migration or backfill requirements (if any): none.

## Constraints & Risks

- Limits (latency/throughput/memory) and acceptable trade-offs:
  - D1–D4 are pure, in-memory computations with no I/O; latency is bounded by input size (attendee counts, candidate-slot horizon). No external service calls occur inside the deterministic functions.
- Security/privacy considerations:
  - `PRIVATE_BUSY_ONLY` items must not have their semantics ingested; only availability is recorded.
  - Kill switches `SendEnabled` and `CalendarWriteEnabled` default to `false` so no outbound mail or calendar write occurs until an operator explicitly enables it.
- Operational/rollout risks and mitigations:
  - Upstream data dependencies are deferred to separate issues. The agent receives partial data until they land:
    - #71 — populate `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson` on `EventDto` from the COM Recipients collection (attendee JSON schema `[{"name":"...","email":"..."}]`, per OR-5).
    - #72 — add Graph-shaped `EventDto` fields (`categories`, `isOrganizer`, `isOnlineMeeting`, `allowNewTimeProposals`, `iCalUId`, `seriesMasterId`, `lastModifiedDateTime`, full body, `sensitivityLabel`).
    - #73 — add `MessageDto` fields (`senderEmailResolved`, `fromEmailAddress`, `toJson`, `ccJson`, `conversationId`, `meetingMessageType`).
    - #74 — add HostAdapter `mailboxSettings` and `calendar/getSchedule` endpoints.
    - #75 — add HostAdapter `sendMail` endpoint backed by COM send.
    - #76 — evolve the HostAdapter `/v1/*` surface to the Graph-shaped surface.
  - Mitigation: D1–D4 are fully unit- and property-testable against mock `ISchedulingService` implementations before any of #71–#76 are complete. The HostAdapter-backed adapter shim (OR-4) is a thin mapper in `OpenClaw.Core` that wraps `IHostAdapterClient` and is replaced when #76 lands.
  - Architecture-boundary risk: per `.claude/rules/architecture-boundaries.md` rule 6, `OpenClaw.Core` may depend only on `OpenClaw.HostAdapter.Contracts` and must not pull Outlook COM into its closure. The agent components are folded into the existing `OpenClaw.Core` project under the `OpenClaw.Core.Agent` namespace; no new project and no new `ProjectReference` edge are introduced, so rule 6 and the COM-confinement rule 3 are preserved. The contract-parity invariant (no MailBridge/HostAdapter/COM references in the D1–D4 deterministic types) is enforced by a namespace-scoped `NetArchTest.Rules` assertion rather than assembly isolation.
  - File-size risk: per `.claude/rules/general-code-change.md`, no production, test, or reusable script file may exceed 500 lines. The agent logic is split across the file set under `src/OpenClaw.Core/Agent/**`, one or a few small types per file, so each unit stays within the 500-line limit.

## Implementation Strategy

- Implementation scope (what changes, not sequencing):
  - Agent components are folded into the existing `OpenClaw.Core` project under `src/OpenClaw.Core/Agent/**` in the `OpenClaw.Core.Agent` namespace (no new project, no new `ProjectReference`). `OpenClaw.Core` is already classified T1 in `quality-tiers.yml`; the folded code inherits T1.
  - D1: `NormalizedMeetingContext` model and `MeetingContextNormalizer`.
  - D2: `TriageDecision` enum + `TriageResult`, `DependencyScorer`, `TriageEngine`, `TriagePolicy`.
  - D3: `OwnerPriority` enum, `OwnerPriorityClassifier`, `RecurringMeetingKind` enum, `RecurringMeetingClassifier`, `MovePolicy`, `OwnerSchedulingPolicy`.
  - D4: `SchedulingRequest`, `CandidateSlot`, `SlotProposer`, `WorkingHoursPolicy`.
  - D5: `AgentPolicyOptions` bound from `appsettings.json` (`OpenClaw:AgentPolicy`), including `SendEnabled` and `CalendarWriteEnabled` kill switches.
  - D6: `ISchedulingService` and Graph-shaped DTOs (`SchedulingMessageDto`, `SchedulingEventDto`, `MailboxSettingsDto`, `FreeBusyScheduleDto`, `SendMailRequest`); a HostAdapter-backed adapter shim (mapper in `OpenClaw.Core` wrapping `IHostAdapterClient`, per OR-4); and a new `SchedulingWorker` in `OpenClaw.Core` orchestrating poll → hydrate → triage → priority → propose.
- New classes/functions/commands to add or update: as listed in the deliverable file sets in the research (Section 2, D1–D6).
- Dependency changes (new/removed packages) and rationale: a property-based testing library (`CsCheck` or `FsCheck`) is used in the T1 agent test project to satisfy the property-test density gate; use the library already approved for the repository's T1 tests.
- Logging/telemetry additions and locations: the `SchedulingWorker` logs each decision class and its reasons at the appropriate level using the project's established logging pattern; D1–D4 do not log (they are pure).
- Rollout plan (feature flags, staged deploys, fallback path): `SendEnabled` and `CalendarWriteEnabled` default to `false`; the deterministic pipeline runs and is observable before any side-effecting action is enabled. The HostAdapter-backed `ISchedulingService` shim is the Stage 0 implementation; the Graph-backed implementation is introduced in Product Increment 1 with no change to D1–D4.

## Acceptance Criteria

- [x] AC-1 (D1): `MeetingContextNormalizer.Normalize(mailboxUpn, message, event)` is a pure function (no I/O, no side effects) that produces a `NormalizedMeetingContext` with email addresses normalized to trimmed lowercase, HTML stripped from body text, and attendees partitioned into required/optional/resource per master Section 9.2, including the no-event fallback to message subject and body.
- [x] AC-2 (D2): `TriageEngine.Triage(context, policy)` returns exactly one of `IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE` for every valid input, and `DependencyScorer.Score` applies the master Section 9.2 weights and thresholds (empty subject+body → `IGNORE`; `sensitivity=private` → `PRIVATE_BUSY_ONLY`; VIP organizer or score >= 7 → `PROTECTED_MEETING`; external sender or score >= 4 → `HUMAN_APPROVAL`; otherwise `AUTO_COORDINATE`).
- [x] AC-3 (D3): `OwnerPriorityClassifier.Classify` returns a valid `OwnerPriority` member (`P0`–`P4`, `ESCALATE_TO_OWNER`, `DIGEST_IGNORED`) per master Section 10.1–10.2; `RecurringMeetingClassifier.Classify` returns `NON_RECURRING`, `ONE_ON_ONE`, `RECURRING_FORUM`, or `RECURRING_OTHER` per master Section 10.3; and `MovePolicy.CanMove` returns `false` for a `RECURRING_FORUM` when the requester is neither the owner nor the meeting owner.
- [x] AC-4 (D3): The priority and move-policy layers run only after triage resolves to `AUTO_COORDINATE` or `HUMAN_APPROVAL`; a `PROTECTED_MEETING` remains protected regardless of requester priority and a `PRIVATE_BUSY_ONLY` item remains opaque regardless of priority.
- [x] AC-5 (D4): `SlotProposer.ProposeTimes` is deterministic given an injected `TimeProvider` and returns an ordered `CandidateSlot` list (day preference then start time) where every slot falls inside working hours, is not in a no-meeting block, is free per the supplied free/busy, and starts at least `MinNoticeMinutes` from the injected current time, normalized to the mailbox time zone per master Section 10.4.
- [x] AC-6 (D5): `AgentPolicyOptions` binds from `appsettings.json` section `OpenClaw:AgentPolicy` and exposes all triage, owner, and working-hours policy lists plus the `SendEnabled` and `CalendarWriteEnabled` kill switches; both kill switches default to `false`.
- [x] AC-7 (kill switches): When `SendEnabled` is `false`, the `SchedulingWorker` does not invoke `ISchedulingService.SendMailAsync`; when `CalendarWriteEnabled` is `false`, the worker invokes no calendar-write operation. The deterministic pipeline still computes and logs its result in both cases.
- [x] AC-8 (D6 seam): `ISchedulingService` and its DTOs (`SchedulingMessageDto`, `SchedulingEventDto`, `MailboxSettingsDto`, `FreeBusyScheduleDto`, `SendMailRequest`) exist as the Graph-shaped middleware contract; DTO serialization round-trips produce identical field values (contract/schema test).
- [x] AC-9 (D6 adapter + worker): A HostAdapter-backed `ISchedulingService` mapper in `OpenClaw.Core` wraps `IHostAdapterClient` (per OR-4), correctly mapping `MessageDto`/`EventDto` to the Graph-shaped DTOs (including null-field handling and attendee JSON parsing to the `[{"name":"...","email":"..."}]` schema), and a new `SchedulingWorker` orchestrates poll → hydrate → triage → priority → propose through `ISchedulingService`.
- [x] AC-10 (contract-parity invariant): D1–D4 contain no reference to `OpenClaw.MailBridge`, `OpenClaw.HostAdapter`, or any COM type; swapping the `ISchedulingService` implementation requires zero change to D1–D4. The project graph and namespace usage confirm the invariant.
- [x] AC-11 (namespace + tier): Agent components reside in the `OpenClaw.Core.Agent` namespace within the existing `OpenClaw.Core` project (already classified T1); the contract-parity invariant is enforced by a namespace-scoped architecture test.
  - Note: a separate `OpenClaw.Core.Agent` project was rejected to comply with architecture-boundaries rule 6 (`OpenClaw.Core` may depend only on `OpenClaw.HostAdapter.Contracts`), which forbids a new `OpenClaw.Core -> OpenClaw.Core.Agent` project reference.
- [x] AC-12 (test obligations): The T1 agent code has at least one property-based test per pure function (`Normalize`, `DependencyScorer.Score`, `TriageEngine.Triage`, `OwnerPriorityClassifier.Classify`, `RecurringMeetingClassifier.Classify`, `MovePolicy.CanMove`, `SlotProposer.ProposeTimes`) using `CsCheck` or `FsCheck`, with line coverage >= 85% and branch coverage >= 75% on the agent application code; async/time-dependent tests use `FakeTimeProvider`.
- [x] AC-13 (dependencies deferred): The spec and code reference issues #71–#76 as the deferred HostAdapter/MailBridge data-source work; the agent logic is complete and correct against partial upstream data and does not implement any of #71–#76.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)
