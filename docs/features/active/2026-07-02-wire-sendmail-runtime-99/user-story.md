# `wire-sendmail-runtime` — User Story

- Issue: #99
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-02

## Story Statement

- As the mailbox owner running the OpenClaw scheduling assistant against my local Outlook profile, I want the assistant to actually send its proposed-times reply to a meeting requester, so that the triage → slot-proposal work it already does results in the requester receiving concrete times instead of the pipeline stopping one step short.
- As the mailbox owner, I want outbound send to stay disabled unless I explicitly turn on the `SendEnabled` kill switch, so that wiring the send path into the runtime cannot cause a single unexpected email from my mailbox.
- As the operator of the assistant, I want a failed send to be logged with the message identifier and the cycle to continue, so that one unreachable adapter or one bad message never stalls scheduling for everything else in the queue.

## Problem / Why

`HostAdapterSchedulingService.SendMailAsync` (`src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`, lines 123-129) still throws `NotSupportedException` with a message citing "deferred to issues #74/#75" — but those issues are closed and both halves of the send path now exist: `IHostAdapterClient.SendMailAsync` is implemented (`HostAdapterHttpClient`) and the HostAdapter exposes the Graph-shaped `POST /users/{assistantMailbox}/sendMail` route backed by the MailBridge `send_mail` COM RPC. The stale wiring is the single largest concrete blocker to a functioning Stage 0 read-and-reply loop: the deterministic scheduling pipeline can triage and propose slots but cannot send the reply. Identified as gap F5 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (Stage 0 gap table, "Agent runtime wiring — triage → slot proposal → send").

From the owner's perspective: today the assistant reads incoming meeting requests, triages them, and computes candidate slots — and then does nothing visible. Every proposal dies in a log line. The value of the whole Stage 0 loop is only realized when the requester receives the reply.

## Personas & Scenarios

- Persona: **Dan, the mailbox owner.**
  - Who: a busy manager whose assistant mailbox runs the OpenClaw bridge against his local Outlook profile.
  - Cares about: meeting requesters getting timely, accurate proposed times without his manual involvement; absolute control over when the system is allowed to send on his behalf.
  - Constraints: everything runs through the local Outlook profile via the HostAdapter/MailBridge chain; no cloud send path exists in Stage 0.
  - Goals and frustrations: the pipeline already computes correct proposals (he sees them in the logs), but he still has to paste times into replies by hand. He will not enable an auto-send feature that could surprise him — the kill switch must default off and be the only gate he needs to reason about.
  - Context and motivations: this is the last wiring gap (F5) before the Stage 0 read-and-reply loop is real; he plans to enable `SendEnabled` on a test mailbox first.

- Persona: **The operator** (Dan wearing his admin hat).
  - Who: the person who deploys the worker, sets `OpenClaw:AgentPolicy` configuration, and watches the logs.
  - Cares about: failures being visible and contained; a send outage not halting triage of other messages; being able to shut off sending instantly by flipping one config value.

- Scenario: **Proposed-times reply is sent (happy path).**
  - Who is acting: the `SchedulingWorker` background cycle, on behalf of Dan.
  - Trigger: a colleague's meeting request lands in the mailbox and surfaces as a scheduling candidate.
  - Steps: the worker hydrates the message, triages it to `AUTO_COORDINATE`, classifies priority, fetches mailbox settings and free/busy, and proposes candidate slots. Dan has set `SendEnabled=true`. The worker builds the reply (`Re: {subject}`, plain-text list of proposed times, addressed to the requester) and calls `SendMailAsync`; the service maps the request to the wire contract and the HostAdapter accepts it (202), handing it to Outlook via the MailBridge.
  - Obstacles/decisions: none on this path; the message appears in Sent Items (`SaveToSentItems` is true), so Dan can audit exactly what was sent.
  - Expected outcome: the requester receives the proposed-times reply within one worker cycle, with no manual action by Dan.

- Scenario: **Kill switch off (default) — nothing is sent.**
  - Who is acting: the same worker cycle on a fresh deployment where Dan has not touched `SendEnabled`.
  - Trigger: the same meeting request arrives.
  - Steps: the pipeline computes and logs the full decision including proposed slots, then logs "SendEnabled is false; not sending mail" and stops.
  - Expected outcome: zero outbound mail. Dan can review the would-have-sent decisions in the logs before ever enabling send.

- Scenario: **Send fails — the loop survives.**
  - Who is acting: the operator, reviewing logs after the HostAdapter was briefly unreachable.
  - Trigger: the client's send call fails (transport error or an `Ok: false` envelope such as `BRIDGE_UNAVAILABLE`).
  - Steps: the service raises an exception carrying the error code and message; the worker's per-message isolation logs "Scheduling pipeline failed for message {MessageId}" and continues with the next candidate.
  - Obstacles/decisions: until the F6 idempotency feature lands, a later cycle that re-surfaces the same candidate may send the proposal again — an accepted, documented interim risk that the operator manages with `SendEnabled`.
  - Expected outcome: the failure is visible in the logs with the message id; other messages in the same cycle are unaffected.

## Acceptance Criteria

- [x] `HostAdapterSchedulingService.SendMailAsync` maps the agent-side `OpenClaw.Core.Agent.SendMailRequest` to the wire `OpenClaw.HostAdapter.Contracts.SendMailRequest` and awaits `IHostAdapterClient.SendMailAsync`, forwarding the caller's cancellation token; the `NotSupportedException` and the stale "deferred to issues #74/#75" doc comments (in `HostAdapterSchedulingService.cs` and `SendMailRequest.cs`) are removed.
- [x] A send envelope with `Ok: false` raises an exception that carries the API error code and message (no silent catch); exceptions thrown by the client, including `OperationCanceledException`, propagate unwrapped.
- [x] `SchedulingWorker` gating holds end-to-end in tests: send invoked exactly once per actionable message when `SendEnabled=true`; send never invoked when `SendEnabled=false` (the default); a send failure is logged via the worker's per-message isolation (`ProcessMessageSafelyAsync`) and the cycle continues with the next message; cancellation stops the cycle.
- [x] `SendMailAsync_Throws_DeferredNotSupported` in `HostAdapterSchedulingServiceTests` is replaced by delegation tests (success, envelope failure, exception propagation, cancellation, request mapping) using the suite's MSTest + FluentAssertions + Moq conventions; worker tests gain a send-failure isolation case; the pure request-mapping function has at least one property-based test (T1 requirement).
- [x] No changes to HostAdapter routes, MailBridge, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`, or schemas — the diff is confined to `OpenClaw.Core` runtime wiring and its tests.
- [x] Full C# toolchain passes; coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered.

## Non-Goals

- **Idempotency / dedupe keys** — the next feature in the program queue (F6). Until it lands, a retried cycle may resend the same proposal; this is an accepted interim risk gated by `SendEnabled`.
- **Reply threading** — the wire contract has no `InReplyToMessageId` counterpart; the agent field is dropped at the mapping seam and Stage 0 replies are new messages with a `Re:` subject.
- **Send-on-behalf** — deferred to PI-1 (AC-09) at the bridge mail-sender seam, per the `IHostAdapterClient.SendMailAsync` contract notes.
- **Calendar writes** — the `CalendarWriteEnabled` switch and its action path are untouched.
- **Any change to HostAdapter routes, MailBridge, contract projects, or schemas** — this feature is runtime wiring inside `OpenClaw.Core` only.
