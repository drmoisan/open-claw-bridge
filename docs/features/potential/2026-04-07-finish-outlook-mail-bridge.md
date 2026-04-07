---
title: "finish-outlook-mail-bridge - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-07T07-52"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# finish-outlook-mail-bridge (Potential)

- Date captured: 2026-04-07
- Author: drmoisan
- Status: Draft

## Problem / Why

The Outlook mail bridge is only partially implemented today: settings load, bridge status, Outlook acquisition, and scan timestamps exist, but message and calendar RPC methods still return placeholders and the scanner does not enumerate or normalize Inbox or Calendar data. Until the original fixed spec is completed end to end, `openclaw-svc` cannot reliably read local-only, read-only Outlook metadata through `OpenClaw.MailBridge.Client.exe` in the required interactive-session-only topology.

## Proposed Behavior

Finish the existing bridge architecture in `src/OpenClaw.MailBridge`, `src/OpenClaw.MailBridge.Client`, `src/OpenClaw.MailBridge.Contracts`, `tests/OpenClaw.MailBridge.Tests`, the existing install/test scripts, and `docs/mailbridge-runbook.md` without adding unrelated projects or replacement transports. The completed behavior must retarget all projects to `net8.0-windows`, acquire Outlook on one dedicated STA thread, scan and normalize default Inbox and Calendar metadata into SQLite, expose that cached data through the exact named-pipe/client contract, and prove the required preflight plus acceptance flows with automated tests and scripts.

## Acceptance Criteria (early draft)

- [ ] All production and test projects target `net8.0-windows`; no project remains on `net10.0-windows`.
- [ ] Outlook automation runs only on one dedicated STA thread in the primary interactive user session, follows the required acquisition sequence, and performs disciplined COM cleanup on both success and failure paths.
- [ ] Default Inbox scanning uses `Restrict` on `ReceivedTime`, a 30-second poll interval, a 5-minute overlap window, dedupes by `EntryID`, normalizes both `MailItem` and `MeetingItem`, and returns real cached message data from the message RPC methods.
- [ ] Default Calendar scanning uses `Items.Sort`, `IncludeRecurrences = true`, a bounded start/end filter, avoids `Count` on recurring views, enforces a hard cap, and returns real cached event data from the calendar RPC methods.
- [ ] Safe mode and enhanced mode are both implemented correctly, privacy redaction and preview sanitization/truncation match the fixed spec, and neither attachment content nor message/event body content is logged.
- [ ] SQLite persistence and query behavior are complete for `messages`, `events`, and `scan_state`, including deterministic queries and the required stale-cache behavior.
- [ ] The named-pipe server exposes only the allowed methods, validates requests deterministically, returns the specified error codes, applies explicit `PipeSecurity` grants and denies, and fails hard when SID or ACL resolution (including `openclaw-svc`) cannot be completed.
- [ ] `OpenClaw.MailBridge.Client.exe` supports only the specified commands, writes JSON only to stdout, writes diagnostics only to stderr, does not rely on a hard-coded pipe name, and uses deterministic exit-code mapping.
- [ ] `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1`, and `docs/mailbridge-runbook.md` implement and document the required preflight checks, on-logon interactive task registration, operational guidance, and automated acceptance suite A-F.
- [ ] Tests in `tests/OpenClaw.MailBridge.Tests` prove actual behavior rather than stubs, covering DTO/ID helpers, strict config validation, redaction/sanitization, repository persistence/query behavior, RPC validation/error mapping, safe vs enhanced response shapes, stale-cache handling, COM boundary orchestration, and practical script assertions.

## Constraints & Risks

- The topology is fixed to `src/OpenClaw.MailBridge`, `src/OpenClaw.MailBridge.Client`, `src/OpenClaw.MailBridge.Contracts`, `tests/OpenClaw.MailBridge.Tests`, `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1`, and `docs/mailbridge-runbook.md`; do not add unrelated projects.
- Do not replace the architecture with HTTP, WebSocket, Microsoft Graph, EWS, or a Windows service.
- Scope remains local-only, read-only Windows Outlook metadata access; do not add send, write, reply, accept, decline, create, reschedule, folder-browse, or attachment-read capabilities.
- Key implementation risks are Outlook COM/threading correctness, recurring calendar enumeration edge cases, named-pipe ACL determinism, SQLite cache consistency, and falsely claiming completion from partial tests instead of spec-complete end-to-end behavior.

## Test Conditions to Consider

- [ ] Unit coverage for DTO/ID helpers, strict configuration validation, redaction, sanitization/truncation, safe vs enhanced shaping, repository persistence/query logic, stale-cache rules, and RPC validation/error mapping
- [ ] Integration scenarios for primary interactive-session Outlook acquisition, dedicated STA orchestration, Inbox overlap/dedupe behavior, Calendar recurrence windows and hard caps, named-pipe ACL failure handling, and client JSON/exit-code behavior
- [ ] CLI/API examples for each supported client command, JSON-only stdout, stderr diagnostics, successful cached reads, stale-cache responses, invalid request handling, and automated acceptance suite A-F entry points

## Next Step

- [ ] Promote to a GitHub feature issue using the original fixed spec and this mission brief as the non-negotiable contract
- [ ] Create the active feature folder and derive `issue.md`, `user-story.md`, and `spec.md` without changing the required topology or scope

