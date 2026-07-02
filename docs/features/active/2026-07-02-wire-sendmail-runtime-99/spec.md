# wire-sendmail-runtime — Spec

- **Issue:** #99
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02
- **Status:** Draft
- **Version:** 0.2

## Overview

`HostAdapterSchedulingService.SendMailAsync` (`src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`, lines 123-129) still throws `NotSupportedException` with a message citing "deferred to issues #74/#75" — but those issues are closed and both halves of the send path now exist: `IHostAdapterClient.SendMailAsync` is implemented (`HostAdapterHttpClient.SendMailAsync`, `src/OpenClaw.Core/HostAdapterHttpClient.cs`, lines 138-151, POSTing to `users/{id}/sendMail`) and the HostAdapter exposes the Graph-shaped `POST /users/{assistantMailbox}/sendMail` route backed by the MailBridge `send_mail` COM RPC. The stale wiring is the single largest concrete blocker to a functioning Stage 0 read-and-reply loop: the deterministic scheduling pipeline can triage and propose slots but cannot send the reply. Identified as gap F5 in `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` (Stage 0 gap table, "Agent runtime wiring — triage → slot proposal → send").

Verified facts this spec relies on (all confirmed by reading current source):

- Two distinct records named `SendMailRequest` exist. The agent seam uses the flat `OpenClaw.Core.Agent.SendMailRequest` (`src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs`: `Subject`, `BodyContent`, `BodyContentType`, `ToRecipients`/`CcRecipients` as `AttendeeDto` lists, `InReplyToMessageId`). The wire contract is the Graph-shaped `OpenClaw.HostAdapter.Contracts.SendMailRequest` (`src/OpenClaw.HostAdapter.Contracts/MailContracts.cs`: `SendMailMessageDto Message`, `bool SaveToSentItems = true`). In `HostAdapterSchedulingService` and `SchedulingWorker.Pipeline` the unqualified name resolves to the agent record (enclosing-namespace lookup precedes `using` directives), so the delegation requires an explicit agent-to-wire mapping, not a pass-through.
- Return-type mismatch: `ISchedulingService.SendMailAsync` returns `Task`; `IHostAdapterClient.SendMailAsync` returns `Task<ApiEnvelope<object?>>` (`ok: true`, `data: null` on a 202). The service must convert a failed envelope into a thrown exception because the seam has no return channel.
- `AgentPolicyOptions.SendEnabled` (`src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs`, line 78) is an uninitialized `bool` auto-property: **the default is `false` (send disabled)**. This is the safety boundary that keeps the newly wired send path unreachable until explicitly enabled.
- The worker already gates the send call on `options.SendEnabled` (`SchedulingWorker.Pipeline.cs`, lines 120-132) and already isolates per-message failures (`SchedulingWorker.ProcessMessageSafelyAsync`, which rethrows `OperationCanceledException` and logs everything else). No production change to `SchedulingWorker` is required.

## Behavior

- `HostAdapterSchedulingService.SendMailAsync` maps the agent request to the wire contract, awaits `IHostAdapterClient.SendMailAsync`, and forwards the caller's cancellation token. The stale `NotSupportedException` and its doc comment are removed, as is the stale "the runtime adapter throws until it is available" sentence in `SendMailRequest.cs` and the "read methods available today" phrasing in the service's class-level doc comment.
- Envelope handling (deliberate asymmetry with the read methods): a response with `Ok: false` (or a null `Data` is irrelevant here — success is `ok: true, data: null`) throws `InvalidOperationException` including the `ApiError.Code` and `ApiError.Message`. The read methods degrade gracefully because a missing message or empty free/busy grid has a safe neutral value; a failed send has none, and silently treating it as success would violate the fail-fast policy in `.claude/rules/general-code-change.md`.
- Exceptions thrown by the client (transport failures, `OperationCanceledException`) propagate unwrapped. The worker's existing `ProcessMessageSafelyAsync` boundary logs non-cancellation failures with the message id and continues the loop; cancellation propagates and stops the cycle.
- Kill-switch behavior is unchanged and now exercisable end-to-end: with `SendEnabled=false` (default) the pipeline computes and logs but never calls `SendMailAsync`; with `SendEnabled=true` it sends the proposal reply built by `BuildProposalReply`.
- No new HTTP surface, no MailBridge changes, no contract or schema changes — runtime wiring inside `OpenClaw.Core` only.

## Inputs / Outputs

- Inputs: the agent-side `SendMailRequest` built by `SchedulingWorker.BuildProposalReply` (subject `Re: {original subject}`, plain-text proposed-times body, single To recipient = normalized `MessageFrom`); the caller's `CancellationToken`.
- Outputs: one `POST users/{mailboxId}/sendMail` per allowed send, issued by the existing `HostAdapterHttpClient` (bearer token from the configured token file, `X-Request-Id` header). Logging: existing worker log statements ("SendEnabled is false; not sending mail...", per-message error log) are unchanged; no new log categories.
- Config keys and defaults: `OpenClaw:AgentPolicy:SendEnabled` — default `false` (send disabled); `OpenClaw:HostAdapter:{BaseUrl, MailboxId, TokenFile}` — consumed by the client, unchanged by this feature.
- Versioning / backward compatibility: `ISchedulingService`, `IHostAdapterClient`, and both `SendMailRequest` records are unchanged. Callers of `SendMailAsync` observe a behavior change only in that the method now performs the send instead of throwing.

## API / CLI Surface

No new commands, flags, routes, or contract types. The only public-surface addition is one mapping method on the existing injected mapper:

- `SchedulingDtoMapper.MapSendMailRequest(OpenClaw.Core.Agent.SendMailRequest request) : OpenClaw.HostAdapter.Contracts.SendMailRequest` — pure, deterministic, throws `ArgumentNullException` on null input. Use a using-alias (for example `using WireSendMailRequest = OpenClaw.HostAdapter.Contracts.SendMailRequest;`) to disambiguate the two record names inside the mapper file.

Mapping rules (validation is the wire's concern; the mapper is shape translation only):

| Agent field | Wire field |
|---|---|
| `Subject` | `Message.Subject` |
| `BodyContentType`, `BodyContent` | `Message.Body` = `SendMailBodyDto(ContentType, Content)` |
| `ToRecipients` (`AttendeeDto(Name, Email)`) | `Message.ToRecipients` = `SendMailRecipientDto(SendMailEmailAddressDto(Address: Email, Name: Name, empty name → null))` |
| `CcRecipients` | `Message.CcRecipients`; an empty list maps to `null` (the wire default) |
| — | `Message.BccRecipients` = `null` (agent seam has no BCC concept) |
| `InReplyToMessageId` | **Dropped** — the wire contract has no counterpart; see Constraints & Risks |
| — | `SaveToSentItems` = `true` (wire default; sent proposals remain auditable in Sent Items) |

## Data & State

- Data transformations: the single agent-to-wire mapping above. Invariant: the mapper never mutates its input and never performs I/O.
- Caching or persistence: none added. The bridge Outbox behavior on offline send (202 = accepted, not delivered) is pre-existing HostAdapter behavior and unchanged.
- Migration or backfill: none.
- State: `HostAdapterSchedulingService` remains stateless; `SchedulingWorker` state is unchanged.

## Constraints & Risks

- **Send becomes reachable in production once merged.** The `SendEnabled` kill switch is the safety boundary; its default is `false` (verified: uninitialized `bool` auto-property in `AgentPolicyOptions`, line 78). Deployments that have not opted in observe no behavior change.
- **Accepted interim risk — duplicate sends until F6.** Idempotency/dedupe keys are deliberately out of scope; they are the next feature in the program queue (F6). Until F6 lands, a retried cycle (for example, a candidate re-surfaced after a transient failure) may resend the same proposal reply. This risk is accepted for Stage 0 and is gated by `SendEnabled`. Sequence F6 immediately after this feature.
- **`InReplyToMessageId` is not transmitted.** The wire contract carries no reply-threading field; Stage 0 replies are new messages whose subject carries `Re:`. True reply threading is deferred with the bridge mail-sender seam work (see the send-on-behalf deferral note on `IHostAdapterClient.SendMailAsync`). Document the drop in the mapper's doc comment.
- **Stale worker catch block is out of scope.** `SchedulingWorker.Pipeline.cs` (lines 87-96) still catches `NotSupportedException` around the mailbox-settings/free-busy fetches with a stale #74/#75 comment. That path is graceful degradation at the `ISchedulingService` seam and its removal changes worker behavior; leave it unchanged in this feature.
- Standards: MSTest + FluentAssertions (+ Moq), matching the existing `OpenClaw.Core.Tests` suite; no temp files in tests; 500-line file cap (current sizes: service 131 lines, mapper 190 lines — both stay well under after the change); COM confined to MailBridge; `OpenClaw.Core` is tier **T1** per `quality-tiers.yml` (property-test and mutation-score obligations apply).

## Implementation Strategy

- Implementation scope (what changes):
  - `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` — replace the throwing `SendMailAsync` with the delegating implementation (null-guard the request, map via the injected `SchedulingDtoMapper`, await the client with `cancellationToken: ct` and `.ConfigureAwait(false)` per the file's existing pattern, throw `InvalidOperationException` with error code/message on `Ok: false`); refresh the class-level doc comment.
  - `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` — add `MapSendMailRequest` (pure, ~30 lines) with the mapping table above; refresh the class doc to mention the outbound direction.
  - `src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs` — remove the stale "deferred to issues #74/#75; the runtime adapter throws" sentence; keep the `SendEnabled` gating sentence.
  - No changes to `SchedulingWorker`/`SchedulingWorker.Pipeline` production code, `IHostAdapterClient`, `HostAdapterHttpClient`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`, HostAdapter routes, or MailBridge.
- New classes/functions: only `SchedulingDtoMapper.MapSendMailRequest`. No new types.
- Dependency changes: none. `OpenClaw.Core` already references both contract projects; the test project already references Moq, FluentAssertions, MSTest, and the property-testing package used by the existing `*PropertyTests` files.
- Logging/telemetry: no additions. Failure logging rides the existing `ProcessMessageSafelyAsync` error log; the disabled-switch log statement already exists.
- Rollout plan: merge with `SendEnabled` remaining default-off; enabling send is an explicit per-deployment configuration action (`OpenClaw:AgentPolicy:SendEnabled = true`). Fallback is setting the switch back to `false`; no schema or data rollback exists or is needed.

## Test Plan

Test changes (all in `tests/OpenClaw.Core.Tests`, MSTest + FluentAssertions + Moq per suite convention):

- `Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — **remove** `SendMailAsync_Throws_DeferredNotSupported` (line 269; it asserts the pre-#99 deferred behavior this feature deletes — record this rationale in the commit/PR) and **add**:
  - success: client returns `Ok: true, Data: null`; call completes; `Verify` the client received the mapped wire request and the caller's token exactly once;
  - envelope failure: `Ok: false` with `ApiError("BRIDGE_UNAVAILABLE", ...)` → `InvalidOperationException` whose message contains the code and message;
  - exception propagation: client throws `HttpRequestException` → same exception type surfaces (no wrapping);
  - cancellation: client throws `OperationCanceledException` for a canceled token → propagates as `OperationCanceledException`;
  - mapping assertions: subject, body content/type, To/Cc recipient translation, empty-CC → null, `SaveToSentItems == true`.
- `Agent/Runtime/SchedulingDtoMapperTests.cs` — example-based tests for `MapSendMailRequest` (field mapping, empty-name → null, null-argument throw), plus at least one property-based test (T1 obligation for a new pure function, same framework as the existing `*PropertyTests` files) — for example: for arbitrary valid agent requests, recipient count and address multiset are preserved and `SaveToSentItems` is always true.
- `Agent/Runtime/SchedulingWorkerTests.cs` — existing `RunCycle_SendDisabled_NeverInvokesSendMail` and `RunCycle_SendEnabled_InvokesSendMail` mock at the `ISchedulingService` seam and remain valid unchanged; **add** `RunCycle_SendFailure_LogsAndContinues` (service `SendMailAsync` throws `InvalidOperationException` for the first message; the second candidate is still processed; cycle does not throw).
- `HostAdapterHttpClientSendMailTests.cs` — unchanged (the client is not modified).

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [x] Tests updated/added (unit/integration as applicable)
- [x] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable — none planned)
- [x] Toolchain pass completed (format → lint → type-check → architecture → test)

## Acceptance Criteria

- [x] `HostAdapterSchedulingService.SendMailAsync` maps the agent-side `OpenClaw.Core.Agent.SendMailRequest` to the wire `OpenClaw.HostAdapter.Contracts.SendMailRequest` and awaits `IHostAdapterClient.SendMailAsync`, forwarding the caller's cancellation token; the `NotSupportedException` and the stale "deferred to issues #74/#75" doc comments (in `HostAdapterSchedulingService.cs` and `SendMailRequest.cs`) are removed.
- [x] A send envelope with `Ok: false` raises an exception that carries the API error code and message (no silent catch); exceptions thrown by the client, including `OperationCanceledException`, propagate unwrapped.
- [x] `SchedulingWorker` gating holds end-to-end in tests: send invoked exactly once per actionable message when `SendEnabled=true`; send never invoked when `SendEnabled=false` (the default); a send failure is logged via the worker's per-message isolation (`ProcessMessageSafelyAsync`) and the cycle continues with the next message; cancellation stops the cycle.
- [x] `SendMailAsync_Throws_DeferredNotSupported` in `HostAdapterSchedulingServiceTests` is replaced by delegation tests (success, envelope failure, exception propagation, cancellation, request mapping) using the suite's MSTest + FluentAssertions + Moq conventions; worker tests gain a send-failure isolation case; the pure request-mapping function has at least one property-based test (T1 requirement).
- [x] No changes to HostAdapter routes, MailBridge, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`, or schemas — the diff is confined to `OpenClaw.Core` runtime wiring and its tests.
- [x] Full C# toolchain passes; coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered.

## Seeded Test Conditions (from potential)

- [x] Unit: `HostAdapterSchedulingServiceTests` send delegation (success, envelope failure with error context, exception propagation, cancellation, request mapping).
- [x] Unit: `SchedulingDtoMapperTests` outbound mapping (field translation, empty-CC → null, empty-name → null, null guard) plus one property-based test.
- [x] Unit: `SchedulingWorkerTests` pipeline send gating (`SendEnabled` true/false — existing tests) and send-failure isolation (new).
- [x] Integration-style: worker cycle against a mocked `ISchedulingService` verifying the composed request (recipient = normalized `MessageFrom`, subject `Re: {subject}`, body from the slot-proposal formatter) — extend `RunCycle_SendEnabled_InvokesSendMail` with argument capture.
