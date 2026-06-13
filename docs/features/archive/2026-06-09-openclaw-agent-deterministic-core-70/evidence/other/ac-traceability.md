# Acceptance Criteria Traceability (Issue #70)

Timestamp: 2026-06-09T12-31

Each acceptance criterion is mapped to the implementing plan task(s) and the test file(s) and/or evidence artifact(s) that verify it. Test paths are under `tests/OpenClaw.Core.Tests/Agent/`; source paths under `src/OpenClaw.Core/Agent/`.

## Spec acceptance criteria (AC-1..AC-13)

| AC | Implementing tasks | Source | Verifying tests / evidence |
|---|---|---|---|
| AC-1 (D1 normalizer) | P2-T1..P2-T5 | `Models/NormalizedMeetingContext.cs`, `MeetingContextNormalizer.cs`, `MeetingContextNormalizer.Helpers.cs` | `MeetingContextNormalizerTests.cs`, `MeetingContextNormalizerPropertyTests.cs`; `evidence/qa-gates/p2-test.md` |
| AC-2 (D2 triage) | P3-T1..P3-T6 | `TriagePolicy.cs`, `TriageDecision.cs`, `DependencyScorer.cs`, `TriageEngine.cs`, `TriageEmail.cs` | `TriageEngineTests.cs`, `DependencyScorerTests.cs`, `TriagePropertyTests.cs`; `evidence/qa-gates/p3-test.md` |
| AC-3 (D3 priority/recurrence/move) | P4-T1..P4-T7 | `OwnerSchedulingPolicy.cs`, `OwnerPriority.cs`, `RecurringMeetingKind.cs`, `OwnerPriorityClassifier.cs`, `RecurringMeetingClassifier.cs`, `MovePolicy.cs` | `OwnerPriorityClassifierTests.cs`, `RecurringMeetingClassifierTests.cs`, `MovePolicyTests.cs`, `PriorityPropertyTests.cs`; `evidence/qa-gates/p4-test.md` |
| AC-4 (layering gate) | P4-T8 | `SchedulingGate.cs` | `PriorityLayeringTests.cs`; `evidence/qa-gates/p4-test.md` |
| AC-5 (D4 slot proposer) | P5-T1..P5-T6 | `WorkingHoursPolicy.cs`, `SchedulingRequest.cs`, `CandidateSlot.cs`, `SlotProposer.cs`, `SlotProposer.Window.cs` | `SlotProposerTests.cs`, `SlotProposerPropertyTests.cs`, `WorkingHoursPolicyTests.cs`; `evidence/qa-gates/p5-test.md` |
| AC-6 (D5 config) | P1-T3, P6-T4, P6-T5 | `Contracts/AgentPolicyOptions.cs`, `Program.cs`, `appsettings.json` | `WorkingHoursPolicyTests.cs` (projection), `SchedulingWorkerTests.cs` (kill-switch defaults); `evidence/qa-gates/p6-build.md`, `final-build.md` |
| AC-7 (kill switches) | P6-T3, P6-T7 | `Runtime/SchedulingWorker.cs`, `Runtime/SchedulingWorker.Pipeline.cs` | `Runtime/SchedulingWorkerTests.cs`; `evidence/qa-gates/p6-test.md` |
| AC-8 (D6 seam) | P1-T4, P1-T5, P1-T8 | `Contracts/ISchedulingService.cs`, `Contracts/*Dto.cs`, `Contracts/SendMailRequest.cs` | `SchedulingDtoContractTests.cs`; `evidence/qa-gates/p1-test.md` |
| AC-9 (D6 adapter + worker) | P6-T1, P6-T2, P6-T3, P6-T6, P6-T7 | `Runtime/SchedulingDtoMapper.cs`, `Runtime/HostAdapterSchedulingService.cs`, `Runtime/SchedulingWorker*.cs` | `Runtime/SchedulingDtoMapperTests.cs`, `Runtime/HostAdapterSchedulingServiceTests.cs`, `Runtime/SchedulingWorkerTests.cs`; `evidence/qa-gates/p6-test.md` |
| AC-10 (contract-parity invariant) | P1-T7 | namespace partition `OpenClaw.Core.Agent` (excl. `.Runtime`) | `AgentArchitectureBoundaryTests.cs`; `evidence/qa-gates/final-arch-test.md` |
| AC-11 (namespace + tier) | P1-T6, P7-T6 | folded into `OpenClaw.Core` (`quality-tiers.yml: OpenClaw.Core: T1`) | `evidence/qa-gates/tier-classification.md`, `final-tier-classification.md` |
| AC-12 (test obligations) | P1-T1, all `*PropertyTests.cs` | property tests per pure function | `MeetingContextNormalizerPropertyTests.cs`, `TriagePropertyTests.cs`, `PriorityPropertyTests.cs`, `SlotProposerPropertyTests.cs`; `evidence/qa-gates/coverage-delta.md`, `final-test.md` |
| AC-13 (dependencies deferred) | P6-T1, P6-T2 | in-code `#71-#76` notes; `NotSupportedException` referencing `#74/#75` | `Runtime/SchedulingDtoMapperTests.cs` (null/empty deferred fields), `Runtime/HostAdapterSchedulingServiceTests.cs` (deferred-throws); this artifact |

## User-story acceptance criteria (AC-U1..AC-U4)

| AC | Implementing tasks | Verifying tests / evidence |
|---|---|---|
| AC-U1 (middleware-agnostic) | P1-T7 | `AgentArchitectureBoundaryTests.cs`; `evidence/qa-gates/final-arch-test.md` |
| AC-U2 (five-way classification, private busy-only, protected gating) | P3-T4, P4-T8 | `TriageEngineTests.cs`, `PriorityLayeringTests.cs`; `evidence/qa-gates/p3-test.md`, `p4-test.md` |
| AC-U3 (single config model, kill switches default off) | P6-T4, P6-T5 | `appsettings.json` (`SendEnabled: false`, `CalendarWriteEnabled: false`), `SchedulingWorkerTests.cs`; `evidence/qa-gates/p6-test.md` |
| AC-U4 (deterministic reproducible slots) | P5-T3, P5-T6 | `SlotProposerTests.cs`, `SlotProposerPropertyTests.cs`; `evidence/qa-gates/p5-test.md` |

## Deferred work (Non-Goals)

Issues #71-#76 are explicitly deferred and NOT implemented by this feature:
- The deferred bridge/HostAdapter data-source work (#71-#76) supplies Graph fields that are not yet available from the bridge cache. The OR-4 mapper maps those absent fields to null/empty with in-code comments referencing #71-#76.
- The mailbox-settings, free/busy, and send-mail endpoints are deferred to #74/#75; `HostAdapterSchedulingService.GetMailboxSettingsAsync`, `GetFreeBusyAsync`, and `SendMailAsync` throw a documented `NotSupportedException` referencing those issues. No `OpenClaw.HostAdapter` or `OpenClaw.MailBridge` production code was modified by this feature.
- The deterministic agent logic (D1-D4) is complete and correct against partial upstream data, validated by unit and property-based tests against mock/value-typed inputs, consistent with AC-13.

Every AC maps to at least one implementing task and at least one verifying test or evidence artifact.
