# finish-outlook-mail-bridge (Issue #12)
title: "finish-outlook-mail-bridge - Plan"
issue: "12"
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
- Status: Promoted -> docs/features/active/finish-outlook-mail-bridge/ (Issue #12)

- Issue: #12
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/12
- Last Updated: 2026-04-07
- Work Mode: full-feature

## Problem / Why

The Outlook mail bridge is only partially implemented today: settings load, bridge status, Outlook acquisition, and scan timestamps exist, but message and calendar RPC methods still return placeholders and the scanner does not enumerate or normalize Inbox or Calendar data. The active branch also regressed the already-corrected target environment back to `net8.0-windows` even though the repository and local machine are aligned to .NET 10. Until the original fixed spec is completed end to end and the branch is corrected back to `net10.0-windows`, `openclaw-svc` cannot reliably read local-only, read-only Outlook metadata through `OpenClaw.MailBridge.Client.exe` in the required interactive-session-only topology.

## Proposed Behavior

Finish the existing bridge architecture in `src/OpenClaw.MailBridge`, `src/OpenClaw.MailBridge.Client`, `src/OpenClaw.MailBridge.Contracts`, `tests/OpenClaw.MailBridge.Tests`, the existing install/test scripts, and `docs/mailbridge-runbook.md` without adding unrelated projects or replacement transports. The completed behavior must retarget all projects to `net10.0-windows`, acquire Outlook on one dedicated STA thread, scan and normalize default Inbox and Calendar metadata into SQLite, expose that cached data through the exact named-pipe/client contract, and prove the required preflight plus acceptance flows with automated tests and scripts.

- Complete the existing repository-first flow rather than replacing it: `ScanWorker` schedules work, `OutlookScanner` performs Outlook enumeration on `OutlookStaExecutor`, `CacheRepository` persists normalized rows, `PipeRpcWorker` serves cache-backed responses, and `OpenClaw.MailBridge.Client.exe` remains the supported CLI surface.
- Keep the fixed topology and scope prohibitions intact while closing the documented gaps: no stubbed non-status methods, no hard-coded pipe-only client behavior, no silent ACL downgrade, and no partial success claims that rely on placeholder data.
- The provided research artifact is sufficient to complete `spec.md` and `user-story.md` without additional discovery; remaining work is implementation planning and delivery inside the existing codebase seams.

## Acceptance Criteria (early draft)

- [x] All production and test projects target `net10.0-windows`.
- [x] Outlook automation runs only on one dedicated STA thread in the primary interactive user session, follows the required acquisition sequence, and performs disciplined COM cleanup on both success and failure paths.
- [x] Default Inbox scanning uses `Restrict` on `ReceivedTime`, a 30-second poll interval, a 5-minute overlap window, dedupes by `EntryID`, normalizes both `MailItem` and `MeetingItem`, and returns real cached message data from the message RPC methods.
- [x] Default Calendar scanning uses `Items.Sort`, `IncludeRecurrences = true`, a bounded start/end filter, avoids `Count` on recurring views, enforces a hard cap, and returns real cached event data from the calendar RPC methods.
- [x] Safe mode and enhanced mode are both implemented correctly, privacy redaction and preview sanitization/truncation match the fixed spec, and neither attachment content nor message/event body content is logged.
- [x] SQLite persistence and query behavior are complete for `messages`, `events`, and `scan_state`, including deterministic queries and the required stale-cache behavior.
- [x] The named-pipe server exposes only the allowed methods, validates requests deterministically, returns the specified error codes, applies explicit `PipeSecurity` grants and denies, and fails hard when SID or ACL resolution (including `openclaw-svc`) cannot be completed.
- [x] `OpenClaw.MailBridge.Client.exe` supports only the specified commands, writes JSON only to stdout, writes diagnostics only to stderr, does not rely on a hard-coded pipe name, and uses deterministic exit-code mapping.
- [x] `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1`, and `docs/mailbridge-runbook.md` implement and document the required preflight checks, on-logon interactive task registration, operational guidance, and automated acceptance suite A-F.
- [x] Tests in `tests/OpenClaw.MailBridge.Tests` prove actual behavior rather than stubs, covering DTO/ID helpers, strict config validation, redaction/sanitization, repository persistence/query behavior, RPC validation/error mapping, safe vs enhanced response shapes, stale-cache handling, COM boundary orchestration, and practical script assertions.

## Constraints & Risks

- The topology is fixed to `src/OpenClaw.MailBridge`, `src/OpenClaw.MailBridge.Client`, `src/OpenClaw.MailBridge.Contracts`, `tests/OpenClaw.MailBridge.Tests`, `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1`, and `docs/mailbridge-runbook.md`; do not add unrelated projects.
- Do not replace the architecture with HTTP, WebSocket, Microsoft Graph, EWS, or a Windows service.
- Scope remains local-only, read-only Windows Outlook metadata access; do not add send, write, reply, accept, decline, create, reschedule, folder-browse, or attachment-read capabilities.
- Key implementation risks are Outlook COM/threading correctness, recurring calendar enumeration edge cases, named-pipe ACL determinism, SQLite cache consistency, and falsely claiming completion from partial tests instead of spec-complete end-to-end behavior.

## Test Conditions to Consider

- [ ] Unit coverage for DTO/ID helpers, strict configuration validation, redaction, sanitization/truncation, safe vs enhanced shaping, repository persistence/query logic, stale-cache rules, and RPC validation/error mapping
	- Include malformed IDs, invalid timestamp/range parsing, request-limit bounds, and deterministic bridge error assertions.
- [ ] Integration scenarios for primary interactive-session Outlook acquisition, dedicated STA orchestration, Inbox overlap/dedupe behavior, Calendar recurrence windows and hard caps, named-pipe ACL failure handling, and client JSON/exit-code behavior
	- Include explicit proof that required SID resolution failures stop pipe startup rather than silently removing ACL entries.
- [ ] CLI/API examples for each supported client command, JSON-only stdout, stderr diagnostics, successful cached reads, stale-cache responses, invalid request handling, and automated acceptance suite A-F entry points
	- Keep examples aligned with the installed-script flows and the runbook so documentation and acceptance evidence describe the same runtime behavior.

## Next Step

- [x] Promote to a GitHub feature issue using the original fixed spec and this mission brief as the non-negotiable contract
- [x] Create the active feature folder and derive `issue.md`, `user-story.md`, and `spec.md` without changing the required topology or scope
- [ ] Execute the implementation plan against the completed `spec.md` and `user-story.md`, keeping the fixed topology, read-only scope, and acceptance criteria unchanged