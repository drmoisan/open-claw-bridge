# message-to-event-linkage (Issue #146)

- Date captured: 2026-07-11
- Author: drmoisan
- Status: Promoted -> docs/features/active/message-to-event-linkage/ (Issue #146)
- Epic: openclaw-runtime-remediation (child A)

- Issue: #146
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/146
- Last Updated: 2026-07-11
- Work Mode: full-feature

## Problem / Why

The OpenClaw scheduling pipeline does not resolve the calendar event that a message
is linked to. `OpenClaw.Core.Agent.Runtime.HostAdapterSchedulingService.GetEventForMessageAsync`
is a stand-in: it forwards the supplied `messageId` to `GetEventAsync` (a plain event
lookup by id), which cannot succeed because a message id is not an event id. The inline
comment states the real linkage is "deferred bridge work (#71-#76)". As a result the
pipeline always misses the direct lookup and depends entirely on the heuristic
calendar-view fallback (`ChooseRelatedEventFromWindowAsync`), which is lossy and only
runs when `CalendarViewFallbackDays > 0`.

There is no MailBridge RPC and no HostAdapter route that answers the question "which
calendar event is this message linked to?" The wire contract for that operation does
not exist.

## Proposed Behavior

Introduce a real message-to-event linkage path across the bridge stack:

1. A new MailBridge RPC (in `OpenClaw.MailBridge`) that, given a message id, resolves the
   linked calendar event from the Outlook/host data source and returns the event payload
   or a clean "not linked" result.
2. A new MailBridge wire contract (in `OpenClaw.MailBridge.Contracts`) describing the
   request/response shape for that RPC.
3. A new HostAdapter route (in `OpenClaw.HostAdapter`) plus a client method on
   `IHostAdapterClient` (in `OpenClaw.HostAdapter.Contracts`) that a Core consumer calls
   to resolve the linked event for a message.
4. `HostAdapterSchedulingService.GetEventForMessageAsync` (in `OpenClaw.Core`) is rewired
   to call the new route instead of the messageId-as-eventId stand-in.

The graceful-degradation contract of `HostAdapterSchedulingService` is preserved: for a
genuinely unlinked message the path returns a clean `null` result (via a success envelope
carrying no data, not a 400 or any error), so the deterministic pipeline degrades to the
ordinary-mail / calendar-view fallback exactly as it does today.

## Acceptance Criteria (early draft)

- [ ] A MailBridge RPC resolves the linked calendar event for a given message id.
- [ ] A MailBridge.Contracts wire contract defines the RPC request/response shape.
- [ ] A HostAdapter route exposes the resolve-linked-event operation and an
      `IHostAdapterClient` method invokes it.
- [ ] `GetEventForMessageAsync` resolves the linked event through the new route and no
      longer treats the message id as an event id.
- [ ] A genuinely unlinked message yields a clean `null` result (no 400, no error
      envelope); the pipeline continues via its existing fallback.
- [ ] Coverage on changed C# code meets repo thresholds (line >= 85%, branch >= 75%) with
      no regression on changed lines.

## Constraints & Risks

- Spans ~3-4 C# projects (`OpenClaw.MailBridge`, `OpenClaw.MailBridge.Contracts`,
  `OpenClaw.HostAdapter`, `OpenClaw.Core`) and adds a new cross-module wire contract.
- Must follow the existing envelope/route patterns (`ApiEnvelope`, `HostAdapterResponses`,
  `IHostAdapterProcessRunner`, `HostAdapterCommandBuilder`) rather than inventing a new
  transport shape.
- `OpenClaw.Core` and `OpenClaw.HostAdapter` are T1 (critical) modules; the contract
  surfaces are T2. Cross-module contract change forces a higher complexity band.
- The unlinked-message null contract must not regress; the existing pipeline relies on it.
- Test framework selection must match the repository's actual C# test projects
  (MSTest/Moq per AGENTS.md vs xUnit/NSubstitute per `.claude/rules/csharp.md` — resolve
  against the real test projects during research).

## Test Conditions to Consider

- [ ] Unit: RPC handler returns event payload for a linked message.
- [ ] Unit: RPC handler returns clean not-linked result for an unlinked message.
- [ ] Unit: HostAdapter route maps success and not-linked to the correct envelope shapes.
- [ ] Unit: `GetEventForMessageAsync` returns a mapped event on success and `null` on
      not-linked without throwing.
- [ ] Contract: request/response wire shape round-trips.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/message-to-event-linkage/` folder from the template
