# `2026-04-07-finish-outlook-mail-bridge` — User Story

- Issue: #12
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-04-07T07-55

## Story Statement

- As the `openclaw-svc` integration path, I want `OpenClaw.MailBridge.Client.exe` to return deterministic, cache-backed Outlook message and calendar metadata over the existing named-pipe contract, so that the service can read local-only Outlook context without taking a direct dependency on Outlook COM.
- As the Windows operator responsible for the bridge host, I want installation, startup, and troubleshooting to stay inside the existing interactive-session topology with safe defaults and explicit failures, so that the bridge can be deployed and validated without exposing protected content or introducing a replacement transport.

## Problem / Why

The Outlook mail bridge is only partially implemented today: settings load, bridge status, Outlook acquisition, and scan timestamps exist, but message and calendar RPC methods still return placeholders and the scanner does not enumerate or normalize Inbox or Calendar data. The current branch also regressed the already-corrected target framework back to the older .NET 8 Windows target. Until the original fixed spec is completed end to end and all projects are corrected back to `net10.0-windows`, `openclaw-svc` cannot reliably read local-only, read-only Outlook metadata through `OpenClaw.MailBridge.Client.exe` in the required interactive-session-only topology.


## Personas & Scenarios

- Persona: Service-side integrator for `openclaw-svc`
  - Owns the local bridge/client integration point that reads Outlook metadata from a Windows machine.
  - Cares about deterministic RPC behavior, stable DTO shapes, JSON-only stdout, and predictable exit codes that can be automated safely.
  - Cannot rely on live Outlook COM access from the service process and cannot tolerate a transport swap to HTTP, WebSocket, Graph, EWS, or a Windows service.
  - Wants the bridge to return real cached messages, meeting requests, and calendar events instead of placeholder payloads.
  - Is frustrated by stubbed RPC methods, hard-coded pipe behavior, and documentation that promises behavior the current runtime does not yet deliver.
- Scenario: Service reads recent mail and calendar data through the completed bridge
  - The actor is the `openclaw-svc` integration path running on a Windows machine that already has classic Outlook and the bridge installed.
  - The trigger is a request for recent Inbox items or a calendar window that must stay local-only and read-only.
  - The operator has already registered the on-logon interactive task, so the bridge is running in the primary user session and keeping the SQLite cache fresh.
  - The service invokes `OpenClaw.MailBridge.Client.exe` with one of the supported commands; the client resolves the configured pipe name, sends a named-pipe request, and reads a JSON response.
  - If the cache is fresh and the request is valid, the bridge returns deterministic cached DTOs and the client exits successfully.
  - If the cache is stale, the response path still stays deterministic: status reports stale-cache state, request validation failures return stable bridge errors, and the service does not need to understand Outlook COM to interpret the result.
  - The expected outcome is that the service receives usable, local-only Outlook metadata through the already-defined contract without direct COM access or a new network-facing component.
- Persona: Windows desktop/operator administrator
  - Owns installation, scheduled-task registration, preflight validation, and first-run troubleshooting on the machine that hosts the bridge.
  - Cares about safe defaults, explicit ACL behavior, privacy boundaries, and a runbook that matches the actual shipped runtime.
  - Operates under the fixed topology: bridge host, client, scripts, and docs only; no new background service, no remote dependency, and no write-back to Outlook.
  - Wants predictable failures when Outlook, scheduled-task registration, or named-pipe ACL identities are wrong, because silent downgrade paths are expensive to diagnose.
  - Is motivated by getting a stable read-only bridge into production without exposing message bodies, event bodies, or attachment content.
- Scenario: Operator installs and validates the bridge in safe mode
  - The actor is the Windows operator preparing a machine for the completed Outlook bridge.
  - The trigger is the need to install or update the bridge so `openclaw-svc` can consume Outlook metadata through the fixed local topology.
  - The operator runs `scripts/install-mailbridge.ps1`, which creates the expected install/config layout, verifies Outlook/profile prerequisites, and registers the on-logon interactive task with `scripts/register-mailbridge-task.ps1`.
  - After sign-in or task start, the operator runs `scripts/test-mailbridge.ps1` and checks that `status`, message list/get, meeting-request list, and calendar list/get calls all succeed using JSON-only stdout.
  - If SID resolution, pipe ACL creation, or Outlook acquisition fails, the bridge fails explicitly and the operator uses `docs/mailbridge-runbook.md` to correct the environment rather than assuming a degraded partial install is acceptable.
  - The expected outcome is a safe-mode bridge that serves real cached data, documents how enhanced mode changes response shaping, and never logs message or event bodies.


## Acceptance Criteria

Each criterion below is intended to be proven either by deterministic coverage in `tests/OpenClaw.MailBridge.Tests` or by the scripted acceptance flows in `scripts/test-mailbridge.ps1`; none of these items are satisfied by placeholder success payloads or documentation-only claims.

- [x] All production and test projects target `net10.0-windows`.
- [x] Outlook automation runs only on one dedicated STA thread in the primary interactive user session, follows the required acquisition sequence, and performs disciplined COM cleanup on both success and failure paths.
- [x] Default Inbox scanning uses `Restrict` on `ReceivedTime`, a 30-second poll interval, a 5-minute overlap window, dedupes by `EntryID`, normalizes both `MailItem` and `MeetingItem`, and returns real cached message data from the message RPC methods.
- [x] Default Calendar scanning uses `Items.Sort`, `IncludeRecurrences = true`, a bounded start/end filter, avoids `Count` on recurring views, enforces a hard cap, and returns real cached event data from the calendar RPC methods.
- [x] Safe mode and enhanced mode are both implemented correctly, privacy redaction and preview sanitization/truncation match the fixed spec, and neither attachment content nor message/event body content is logged.
- [x] SQLite persistence and query behavior are complete for `messages`, `events`, and `scan_state`, including deterministic queries and the required stale-cache behavior.
- [x] The named-pipe server exposes only the allowed methods, validates requests deterministically, returns the specified error codes, applies explicit `PipeSecurity` grants and denies, and fails hard when SID or ACL resolution (including `openclaw-svc`) cannot be completed.
- [x] `OpenClaw.MailBridge.Client.exe` supports only the specified commands, writes JSON only to stdout, writes diagnostics only to stderr, does not rely on a hard-coded pipe name, and uses deterministic exit-code mapping.
- [x] `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1`, and `docs/mailbridge-runbook.md` implement and document the required preflight checks, on-logon interactive task registration, operational guidance, and automated acceptance suite A-F.
- [x] Tests in `tests.OpenClaw.MailBridge.Tests` prove actual behavior rather than stubs, covering DTO/ID helpers, strict config validation, redaction/sanitization, repository persistence/query behavior, RPC validation/error mapping, safe vs enhanced response shapes, stale-cache handling, COM boundary orchestration, and practical script assertions.


## Non-Goals

- Replacing the fixed named-pipe/local-cache topology with HTTP, WebSocket, Microsoft Graph, Exchange Web Services, or a Windows service.
- Adding Outlook write capabilities such as send, reply, accept, decline, create, update, reschedule, delete, or folder-management operations.
- Reading or exposing attachment payloads, full message bodies, or full event bodies through logs, scripts, or RPC responses.
- Expanding the scope beyond the default Inbox and default Calendar folders, including arbitrary folder browsing or mailbox-wide discovery.
- Introducing unrelated new projects, remote infrastructure, or secret-dependent configuration to complete the bridge.
