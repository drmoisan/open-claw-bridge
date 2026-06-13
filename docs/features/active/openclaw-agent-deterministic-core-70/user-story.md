# `openclaw-agent-deterministic-core` — User Story

- Issue: #70
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-06-09T12-02

## Story Statement

- As the engineering team building the OpenClaw scheduling assistant, I want the deterministic agent logic (normalization, triage, priority, slot proposal) to depend only on a Graph-shaped middleware seam, so that I can refine it once against a stable contract and carry it forward unchanged when the backend moves from the Local MVP HostAdapter to Microsoft Graph.
- As the operator deploying the assistant, I want to maintain policy lists and kill switches in one configuration model, so that I can control which meetings are auto-coordinated, escalated, or protected, and prevent any outbound mail or calendar write until I explicitly enable it.
- As the mailbox owner whose calendar the assistant manages, I want inbound meeting traffic triaged into clear, predictable decision classes, so that protected and private meetings are never auto-coordinated and routine internal requests are handled without my involvement.

## Problem / Why

The OpenClaw assistant must classify scheduling traffic deterministically and propose meeting times before any production rollout. The governing specification (`docs/open-claw-approach.master.md`) requires contract parity: the agent never calls Outlook COM or vendor APIs directly, and the same agent logic must run unchanged across the Local MVP, Product Increment 1, and Final Vision stages. Today the codebase has no policy configuration, no triage engine, no priority model, and no scheduling-service seam. Without a middleware-agnostic deterministic core, agent logic would couple to the Local MVP transport and have to be rewritten when the backend moves to Microsoft Graph. This feature delivers that core so the logic is built and proven once.

## Personas & Scenarios

- Persona: Engineering team member building the agent
  - who the user is: a developer implementing and testing the deterministic decision logic.
  - what they care about: building the logic once, testing it in isolation, and not having it break when the backend changes.
  - their constraints: must honor architecture boundaries (no MailBridge/HostAdapter/COM references in the deterministic core), the 500-line file limit, and the T1 test obligations.
  - their goals and frustrations: wants to unit- and property-test the logic against a mock seam before any HostAdapter data-source work lands; frustrated by transport coupling that forces rewrites.
  - their context and motivations: the Local MVP exists to refine the agent on real data before production; the logic must carry forward unchanged.
- Persona: Operator configuring the assistant
  - who the user is: the person who maintains policy lists and enables side-effecting actions.
  - what they care about: a single place to set VIP lists, protected categories, working hours, and kill switches.
  - their constraints: must be able to keep the assistant in a read-only, no-action posture until trust is established.
  - their goals and frustrations: wants `SendEnabled` and `CalendarWriteEnabled` to default off and to be the only switches that gate outbound effects.
- Scenario: triaging an inbound meeting request in the Local MVP
  - who is acting: the `SchedulingWorker` running the deterministic pipeline.
  - what triggered the action: a new meeting-related message becomes available through `ISchedulingService`.
  - what steps do they take: the worker hydrates the Graph-shaped DTOs, the normalizer produces a `NormalizedMeetingContext`, the triage engine assigns a decision class, the priority and move-policy layers run for `AUTO_COORDINATE`/`HUMAN_APPROVAL` items, and the slot proposer computes candidate times when scheduling is required.
  - what obstacles or decisions occur: a private meeting is classified `PRIVATE_BUSY_ONLY` and its semantics are not ingested; a VIP-organizer or high-dependency meeting is classified `PROTECTED_MEETING` and is not auto-coordinated; partial upstream data (issues #71–#76 not yet landed) yields a defensible decision on the available fields.
  - what outcome do they expect: a deterministic, reproducible decision and ordered candidate slots, with no outbound action taken while the kill switches are off.
- Scenario: swapping the backend from HostAdapter to Microsoft Graph
  - who is acting: the engineering team in Product Increment 1.
  - what triggered the action: the move from the Local MVP to the cloud backend.
  - what steps do they take: replace the HostAdapter-backed `ISchedulingService` implementation with a Graph-backed one.
  - what obstacles or decisions occur: the deterministic components D1–D4 must require no change.
  - what outcome do they expect: identical agent behavior on identical inputs, confirming the contract-parity invariant.

## Acceptance Criteria

- [x] AC-U1: The deterministic agent logic (normalization, triage, priority, move policy, slot proposal) consumes data only through the Graph-shaped `ISchedulingService` seam and its value-typed DTOs, with no reference to `OpenClaw.MailBridge`, `OpenClaw.HostAdapter`, or any COM type; swapping the `ISchedulingService` implementation requires zero change to that logic.
- [x] AC-U2: Inbound meeting traffic is classified into exactly one of five decision classes (`IGNORE`, `PRIVATE_BUSY_ONLY`, `PROTECTED_MEETING`, `HUMAN_APPROVAL`, `AUTO_COORDINATE`), with private meetings treated as busy-only (no semantic ingestion) and protected meetings never auto-coordinated regardless of requester priority.
- [x] AC-U3: The operator configures all policy lists and the `SendEnabled` and `CalendarWriteEnabled` kill switches in a single configuration model bound from `appsettings.json`; both kill switches default to off, and no outbound mail or calendar write occurs while they are off.
- [x] AC-U4: For `AUTO_COORDINATE` or `HUMAN_APPROVAL` items that require scheduling, the agent proposes deterministic, reproducible candidate time slots that fall inside working hours, avoid no-meeting blocks, are free, and respect minimum notice, ordered by day preference then start time.

## Non-Goals

- HostAdapter/MailBridge data-source work is out of scope and tracked separately: #71 (EventDto attendee JSON), #72 (Graph-shaped EventDto fields), #73 (MessageDto fields), #74 (mailboxSettings + getSchedule endpoints), #75 (sendMail/COM send), #76 (evolve `/v1/*` to the Graph-shaped surface). The agent receives partial data until those land; this feature does not implement any of them.
- No Microsoft Graph-backed `ISchedulingService` implementation, OAuth, RBAC, or cloud hosting; those belong to Product Increment 1.
- No calendar-write or rescheduling pathways (master Section 11); those remain behind feature flags in later stages.
- No new persistence; the existing polling workers continue to populate the local SQLite cache.
