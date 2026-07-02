# wire-sendmail-runtime (Issue #99)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-07-02-wire-sendmail-runtime-99/ (Issue #99)

- Issue: #99
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/99
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

`HostAdapterSchedulingService.SendMailAsync` (`src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`, lines ~123-129) still throws `NotSupportedException` with a message citing "deferred to issues #74/#75" — but those issues are closed and both halves of the send path now exist: `IHostAdapterClient.SendMailAsync` is implemented (`HostAdapterHttpClient`) and the HostAdapter exposes the Graph-shaped `POST /users/{assistantMailbox}/sendMail` route backed by the MailBridge `send_mail` COM RPC. The stale wiring is the single largest concrete blocker to a functioning Stage 0 read-and-reply loop: the deterministic scheduling pipeline can triage and propose slots but cannot send the reply. Identified as gap F5 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (Stage 0 gap table, "Agent runtime wiring — triage → slot proposal → send").

## Proposed Behavior

- `HostAdapterSchedulingService.SendMailAsync` delegates to `IHostAdapterClient.SendMailAsync`, removing the stale `NotSupportedException` and the stale doc comments.
- The existing `SchedulingWorker` pipeline gating is preserved: `AgentPolicyOptions.SendEnabled` (global send kill switch) continues to gate all outbound sends and is now exercisable end-to-end in tests.
- Errors from the underlying client propagate with context (no silent catch); the worker's existing error handling and logging patterns apply.
- No new HTTP surface, no MailBridge changes, no schema changes — this is runtime wiring only.

## Acceptance Criteria

- [x] `HostAdapterSchedulingService.SendMailAsync` maps the agent-side `OpenClaw.Core.Agent.SendMailRequest` to the wire `OpenClaw.HostAdapter.Contracts.SendMailRequest` and awaits `IHostAdapterClient.SendMailAsync`, forwarding the caller's cancellation token; the `NotSupportedException` and the stale "deferred to issues #74/#75" doc comments (in `HostAdapterSchedulingService.cs` and `SendMailRequest.cs`) are removed.
- [x] A send envelope with `Ok: false` raises an exception that carries the API error code and message (no silent catch); exceptions thrown by the client, including `OperationCanceledException`, propagate unwrapped.
- [x] `SchedulingWorker` gating holds end-to-end in tests: send invoked exactly once per actionable message when `SendEnabled=true`; send never invoked when `SendEnabled=false` (the default); a send failure is logged via the worker's per-message isolation (`ProcessMessageSafelyAsync`) and the cycle continues with the next message; cancellation stops the cycle.
- [x] `SendMailAsync_Throws_DeferredNotSupported` in `HostAdapterSchedulingServiceTests` is replaced by delegation tests (success, envelope failure, exception propagation, cancellation, request mapping) using the suite's MSTest + FluentAssertions + Moq conventions; worker tests gain a send-failure isolation case; the pure request-mapping function has at least one property-based test (T1 requirement).
- [x] No changes to HostAdapter routes, MailBridge, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`, or schemas — the diff is confined to `OpenClaw.Core` runtime wiring and its tests.
- [x] Full C# toolchain passes; coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered.

## Constraints & Risks

- The send action becomes reachable in production once merged; the `SendEnabled` kill switch (default off per `AgentPolicyOptions`) is the safety boundary — verify its default and document it in the spec.
- Idempotency/dedupe keys are deliberately out of scope (next feature in the program queue, F6); a retried cycle may resend until F6 lands. Sequence F6 immediately after.
- MSTest + FluentAssertions; no temp files; 500-line cap; COM confined to MailBridge.

## Test Conditions to Consider

- [x] Unit: `HostAdapterSchedulingServiceTests` send delegation (success, failure propagation, cancellation).
- [x] Unit: `SchedulingWorkerTests` pipeline send gating (`SendEnabled` true/false), audit of invocation arguments.
- [x] Integration-style: worker cycle against fake client verifying the composed request (recipient, subject, body from the slot proposal formatter).

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create `docs/features/active/2026-07-02-wire-sendmail-runtime-99/` folder from the template
