# hostadapter-sendmail-com-send — Spec

- **Issue:** #75
- **Parent (optional):** Deferred from #70 (OpenClaw agent deterministic core)
- **Owner:** drmoisan
- **Last Updated:** 2026-06-16T10-50
- **Status:** Approved
- **Version:** 1.0

## Overview

Add an outbound write path so the OpenClaw agent can send mail through the host. Today `IHostAdapterClient` is read-only and the Graph-shaped action `POST /users/{assistantMailbox}/sendMail` (master Section 3.3) is unimplemented. This feature adds the client contract method, the HostAdapter endpoint, a new `send_mail` MailBridge RPC, and the Outlook COM send implementation on the STA thread.

- Target users/personas and primary use cases: the OpenClaw deterministic agent core issuing outbound mail (e.g., meeting replies, notifications) through the HostAdapter.
- Success metric: a `SendMailRequest` with a valid recipient and body produces a Sent Items entry, validated by an integration test against the real COM path.

This is the cross-cutting integration issue (Track M + Track H) and runs last; all five predecessors (#71/#72/#73, #74/#76) are merged.

## Behavior

End-to-end happy path:
1. Caller issues `POST /users/{assistantMailbox}/sendMail` with a Graph-shaped `SendMailRequest` JSON body and a bearer token.
2. `BearerTokenMiddleware` authenticates; the handler calls `RequireReadyBridgeAsync` (409 `BRIDGE_NOT_READY` if not ready).
3. The body is validated (≥1 recipient across To/CC/BCC; `contentType` ∈ {Text, HTML} case-insensitive).
4. `HostAdapterCommandBuilder` builds a `send-mail` CLI invocation; recipients are passed as JSON-serialized arrays.
5. The MailBridge client sends the `send_mail` RPC; `PipeRpcWorker` dispatches to `HandleSendMailAsync`, which calls `IOutlookMailSender.SendMailAsync` on the STA thread.
6. COM creates a `MailItem`, sets `Subject`, body (`HTMLBody` when `contentType=HTML`, else `Body`), To/CC/BCC recipients, and `DeleteAfterSubmit = !saveToSentItems`, then calls `Send()` and releases all COM objects.
7. On success the endpoint returns **202 Accepted** with `ApiEnvelope<object?>` `{ ok: true, data: null }`.

Alternate/edge flows: empty subject permitted (Graph-permissive); `saveToSentItems` defaults to `true` when absent; `{assistantMailbox}` is treated as a Graph placeholder and not validated against the local profile (single-profile MVP).

Error handling: validation failures → 400 `INVALID_REQUEST`; bridge not ready → 409 `BRIDGE_NOT_READY`; bad/missing token → 401; body over the 64KB pipe cap → 502 (`PAYLOAD_TOO_LARGE` bridge code); COM send failure → bridge `InternalError` → 502. Send failures are fail-fast (reported to the caller), not fail-soft. COM resources are released in a `finally` block on success or failure.

## Inputs / Outputs

- Inputs: HTTP POST JSON body `SendMailRequest`; bearer token (existing mechanism).
- Outputs: 202 + envelope on success; error envelope otherwise; existing request logging applies.
- Config keys/defaults: `HostAdapterOptions.MailboxId` (default `"me"`), unchanged. No new config.
- Backward-compatibility: all changes additive (new contract method, new route, new RPC verb). No existing field, route, or RPC is altered or removed. No major version bump.

## API / CLI Surface

HTTP: `POST /users/{assistantMailbox}/sendMail`

Request body (Graph-aligned):
```json
{
  "message": {
    "subject": "string",
    "body": { "contentType": "Text|HTML", "content": "string" },
    "toRecipients": [{ "emailAddress": { "address": "a@b.c", "name": "optional" } }],
    "ccRecipients": [ ... ],
    "bccRecipients": [ ... ]
  },
  "saveToSentItems": true
}
```
Success: `202 Accepted`, `{ "ok": true, "data": null, "meta": { ... } }`.

Client contract: `Task<ApiEnvelope<object?>> SendMailAsync(SendMailRequest request, string? requestId = null, CancellationToken cancellationToken = default)`.

MailBridge RPC: method `send_mail`; flat string params `--subject`, `--body-content-type`, `--body-content`, `--to-recipients` (JSON array), `--cc-recipients` (JSON), `--bcc-recipients` (JSON), `--save-to-sent-items` (`true|false`).

Validation rules: ≥1 recipient across To/CC/BCC; `contentType` ∈ {Text, HTML} (case-insensitive); subject may be empty; `saveToSentItems` defaults true.

## Data & State

- No persistence or schema change. The COM send mutates host state (creates and submits a `MailItem`; optionally writes to Sent Items).
- Invariant: every COM object obtained during a send is released via `ReleaseAll` in `finally`.
- `IOutlookApplicationProvider` holds the shared `Application` reference, set by `OutlookScanner` on connect and cleared on disconnect; `OutlookComMailSender` reads it.

## Constraints & Risks

- **64KB pipe cap (R-1):** a large HTML body may exceed the named-pipe message limit and return `PAYLOAD_TOO_LARGE`. Documented in the endpoint XML doc; no mitigation in MVP.
- **Offline/Outbox (R-5):** `MailItem.Send()` may queue to the Outbox when offline and return without error; a 202 indicates accepted-for-send, not guaranteed delivery. Documented.
- **STA contention (R-3):** send and scan share the single STA queue; a long send serializes ahead of scans. Acceptable for MVP; documented.
- **COM confinement:** COM stays within `OpenClaw.MailBridge`; the send seam (`IOutlookMailSender`) is internal and must not leak COM types upward.
- **Security:** existing bearer-token auth applies unchanged; no new secret handling.

## Implementation Strategy

Scope (additive across both domains):
- `OpenClaw.HostAdapter.Contracts`: new `MailContracts.cs` (`SendMailRequest`, `SendMailMessageDto`, `SendMailBodyDto`, `SendMailRecipientDto`, `SendMailEmailAddressDto`, all `sealed record`); add `SendMailAsync` to `IHostAdapterClient`.
- `OpenClaw.Core`: implement `SendMailAsync` in `HostAdapterHttpClient` via a new `PostAsync<TBody,TResponse>` helper (POST + JSON body; reuse `TokenReader`).
- `OpenClaw.HostAdapter`: new `MailRoutes.cs` (`MapMailRoutes` + `HandleSendMailAsync`), one-line `app.MapMailRoutes()` in `Program.cs`; `BuildSendMail` in `HostAdapterCommandBuilder`; 202 success factory in `HostAdapterResponses`.
- `OpenClaw.MailBridge.Contracts`: add `BridgeMethods.SendMail = "send_mail"` to `All`.
- `OpenClaw.MailBridge.Client`: add `"send-mail"` arm to the `Build` switch.
- `OpenClaw.MailBridge`: new `IOutlookApplicationProvider` + `OutlookApplicationProvider`; new `IOutlookMailSender` + `OutlookComMailSender`; `HandleSendMailAsync` arm in `PipeRpcWorker`; DI registration in `BridgeApplication`; `OutlookScanner` sets/clears the provider.
- Logging: log send attempt and outcome at the bridge handler at `info`/`error` per the project pattern.
- Send-on-behalf deferred to PI-1: the `IOutlookMailSender` seam is designed to accept a future `fromEmailAddress` (optional param/overload) without breaking callers.

## Acceptance Criteria

- **AC-01:** `IHostAdapterClient` declares `SendMailAsync(SendMailRequest, string? requestId = null, CancellationToken = default)` returning `Task<ApiEnvelope<object?>>`; the `SendMail*` DTOs are Graph-aligned `sealed record`s in `OpenClaw.HostAdapter.Contracts`.
- **AC-02:** The HostAdapter registers `POST /users/{assistantMailbox}/sendMail` in a new `MailRoutes.cs` via `app.MapMailRoutes()`; `Program.cs` remains under 500 lines; the `BearerTokenMiddleware` → `RequireReadyBridgeAsync` → validate → dispatch order is preserved.
- **AC-03:** A successful send returns **202 Accepted** with `ApiEnvelope<object?>` (`ok: true`, `data: null`) (D-A).
- **AC-04:** `HostAdapterHttpClient.SendMailAsync` issues an HTTP `POST` to `users/{MailboxId}/sendMail` with the JSON body via a new `PostAsync` helper, obtaining the token through the existing `TokenReader` seam.
- **AC-05:** `BridgeMethods.SendMail = "send_mail"` is added to `BridgeMethods.All`; the MailBridge client `Build` switch gains a `"send-mail"` arm; recipients are passed as JSON-serialized arrays per recipient type (D-C).
- **AC-06:** COM send runs on the STA thread in `OutlookComMailSender : IOutlookMailSender`, obtaining the `Application` via `IOutlookApplicationProvider` set by `OutlookScanner` (D-E); it sets `Subject`, body (`HTMLBody` for `HTML`, else `Body`), To/CC/BCC recipients including BCC via `Recipients.Add(...).Type=olBCC` (D-I), `DeleteAfterSubmit = !saveToSentItems`, calls `Send()`, and releases all COM objects in `finally`. COM remains confined to `OpenClaw.MailBridge`.
- **AC-07:** Validation: ≥1 recipient across To/CC/BCC combined is required (D-G); `contentType` must be `Text` or `HTML` case-insensitive; empty subject is permitted (D-F); `{assistantMailbox}` is not validated against the local profile (D-D); validation failures return 400 `INVALID_REQUEST`; COM send failure maps to `BridgeErrorCodes.InternalError` → HTTP 502 (D-H).
- **AC-08:** `saveToSentItems` defaults to `true` when absent; `true` saves the message to Sent Items (`DeleteAfterSubmit = false`), `false` does not.
- **AC-09:** Send-on-behalf is deferred to PI-1; the `IOutlookMailSender` seam accepts a future `fromEmailAddress` without breaking existing callers, and this deferral is documented.
- **AC-10:** Tests cover: (a) an integration test [real COM] where a valid `SendMailRequest` produces a Sent Items entry; (b) an integration test validating the COM send path; (c) a unit test validating the endpoint with a mocked runner; plus Core client unit tests (POST shape/body/token-missing), MailBridge RPC-dispatch unit tests using a `FakeOutlookMailSender` (success + failure + invalid-request), and a contract-coverage test asserting `BridgeMethods.All` contains `send_mail`.
- **AC-11:** The full seven-stage toolchain passes; line coverage ≥ 85% and branch coverage ≥ 75%; no regression on changed lines; no new analyzer/nullable suppressions except documented `[ExcludeFromCodeCoverage]` on live-COM-only members (each covered by the integration test); no file exceeds 500 lines; all contract changes are additive (no breaking changes).

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)
