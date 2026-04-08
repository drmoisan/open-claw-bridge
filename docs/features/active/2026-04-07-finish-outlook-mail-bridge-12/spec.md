# 2026-04-07-finish-outlook-mail-bridge — Spec

- **Issue:** #12
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-07T07-55
- **Status:** Draft
- **Version:** 0.1

## Overview

The Outlook mail bridge is only partially implemented today: settings load, bridge status, Outlook acquisition, and scan timestamps exist, but message and calendar RPC methods still return placeholders and the scanner does not enumerate or normalize Inbox or Calendar data. Until the original fixed spec is completed end to end, `openclaw-svc` cannot reliably read local-only, read-only Outlook metadata through `OpenClaw.MailBridge.Client.exe` in the required interactive-session-only topology.

The branch must be corrected back to the existing .NET 10 environment before publish and acceptance evidence are rerun.

This feature completes the already-chosen cache-first design rather than replacing it. The implementation stays inside the existing bridge host, client, contracts library, scripts, tests, and runbook so the bridge can scan Outlook metadata on one dedicated STA thread, persist normalized rows in SQLite, and serve deterministic named-pipe responses without introducing a new transport, service model, or write capability.


## Behavior

Finish the existing bridge architecture in `src/OpenClaw.MailBridge`, `src/OpenClaw.MailBridge.Client`, `src/OpenClaw.MailBridge.Contracts`, `tests/OpenClaw.MailBridge.Tests`, the existing install/test scripts, and `docs/mailbridge-runbook.md` without adding unrelated projects or replacement transports. The completed behavior must retarget all projects to `net10.0-windows`, acquire Outlook on one dedicated STA thread, scan and normalize default Inbox and Calendar metadata into SQLite, expose that cached data through the exact named-pipe/client contract, and prove the required preflight plus acceptance flows with automated tests and scripts.

Expected end-to-end runtime flow:

- `Program.Main` continues to delegate to `BridgeApplication.RunAsync`, which resolves the config path, loads `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`, validates it with the existing settings validator, and composes the host with `BridgeStateStore`, `CacheRepository`, `OutlookStaExecutor`, `OutlookScanner`, `ScanWorker`, and `PipeRpcWorker`.
- `OutlookStaExecutor` remains the only place where Outlook COM work runs. Every Outlook acquisition, folder lookup, enumeration, and COM release path must execute through that one dedicated STA thread, with `Thread.SetApartmentState(ApartmentState.STA)` set before the thread starts.
- Startup first initializes the SQLite cache and the named-pipe server. Pipe creation must resolve the required identities up front (`SYSTEM`, Administrators, the primary interactive user, and `openclaw-svc`) and deny `NETWORK`; if SID lookup or ACL creation fails, startup fails hard instead of silently downgrading access.
- `ScanWorker` must honor both polling cadences independently: Inbox on the configured inbox cadence and Calendar on the configured calendar cadence. The worker may share the same background loop, but it must not ignore `CalendarPollSeconds` or perform COM work outside the STA executor.
- Outlook acquisition follows the existing architecture and the research-backed rules: attempt to attach to a running Outlook instance first, create/log on only when `autostartOutlook` allows it, confirm the default Inbox and Calendar folders exist, update bridge lifecycle state accordingly, and release COM references on both success and failure paths.
- Inbox scanning is cache-first and default-folder-only. The scanner builds an Outlook-compatible `ReceivedTime` filter, subtracts the configured overlap window from the last successful scan, uses `Items.Restrict`, iterates matching items, normalizes `MailItem` and `MeetingItem` metadata, dedupes by `EntryID`, upserts message rows, and updates inbox scan state.
- Calendar scanning is also default-folder-only. The scanner sorts the Calendar items by `[Start]`, sets `IncludeRecurrences = true`, applies a bounded start/end filter using the configured past/future window, never calls `.Count` on the recurring view, enforces the hard item cap, normalizes appointment metadata into event rows, and updates calendar scan state.
- RPC methods remain limited to the existing contract names in `OpenClaw.MailBridge.Contracts`. `get_status` returns bridge state and stale-cache metadata; message and calendar methods return repository-backed results only and do not trigger live Outlook traversal on the request path.
- Safe and enhanced mode are response-shaping concerns layered over the normalized cache. Safe mode must suppress protected fields such as `body_preview`, `sender_name`, and `sender_email`; enhanced mode may return sanitized and truncated previews plus protected metadata already defined in the contracts, but neither mode may log message bodies, event bodies, or attachment content.
- When Outlook is temporarily unavailable after prior successful scans, the bridge reports stale cache state through `BridgeStatusDto` rather than fabricating fresh data. When a request is invalid, unsupported, unauthorized, or missing a cached record, the bridge returns the deterministic error mapping defined by the contracts and client exit-code rules.


## Inputs / Outputs

- Inputs (CLI flags, files, env vars)
	- Bridge host CLI: `--config <path>` remains the explicit host override for the settings file.
	- Client commands remain `status`, `list-messages --since <ISO8601> --limit <n>`, `get-message --id <bridge_id>`, `list-meeting-requests --since <ISO8601> --limit <n>`, `list-calendar --start <ISO8601> --end <ISO8601> --limit <n>`, and `get-event --id <bridge_id>`.
	- The client must stop relying on a hard-coded pipe name; the implementation should resolve the pipe from the shared settings file and may accept an explicit `--pipe-name <name>` override so operators and tests can target non-default pipe names deterministically.
	- Runtime configuration source of truth is `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json`; `src/OpenClaw.MailBridge/appsettings.json` is not currently authoritative for the bridge settings model and should not become a second conflicting configuration source.
	- No new environment variables are required by this feature.
- Outputs (artifacts, logs, telemetry)
	- SQLite cache at `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db`, containing normalized `messages`, `events`, and `scan_state` rows.
	- Named-pipe endpoint `\\.\pipe\<pipeName>` serving JSON RPC-style request/response payloads for the existing client.
	- Client stdout remains JSON only; stderr remains diagnostics only.
	- Runtime logs record bridge state transitions, Outlook acquisition results, scan start/end summaries, stale-cache reasons, pipe startup failures, and request-validation failures, while excluding message bodies, event bodies, and attachment content.
- Config keys and defaults:
	- `pipeName`: default `openclaw_mailbridge_v1`; used by both bridge host and client.
	- `mode`: default `safe`; allowed values remain `safe` and `enhanced`.
	- `autostartOutlook`: default `true`; controls whether the scanner may create/log on to Outlook when no active instance exists.
	- `inboxPollSeconds`: default `30`; lower bound enforced by the validator.
	- `calendarPollSeconds`: default `300`; must be honored separately from inbox polling.
	- `inboxOverlapMinutes`: default `5`; overlap applied to Inbox rescans before dedupe.
	- `calendarPastDays`: default `14`; lower bound of the bounded Calendar query window.
	- `calendarFutureDays`: default `60`; upper bound of the bounded Calendar query window.
	- `maxItemsPerScan`: default `500`; hard cap applied to scan enumeration and request limits.
	- `bodyPreviewMaxChars`: default `500`; maximum preview length after sanitization/truncation.
	- `logLevel`: default `Information`; forwarded into host logging.
- Versioning or backward-compatibility constraints:
	- All production and test projects must target `net10.0-windows`; the repo remains pinned to SDK `10.0.201`, which matches the intended .NET 10 runtime environment for this repository.
	- Keep the existing projects, method names, DTO fields, and named-pipe transport contract in `src/OpenClaw.MailBridge.Contracts`; do not introduce replacement RPC transports or unrelated projects.
	- Preserve local-only, read-only behavior and the existing settings/cache file locations so install scripts, client usage, and operator runbooks stay aligned.

## API / CLI Surface

List commands, flags, request/response shapes, and examples.
- Example invocations with expected outputs (concise):
	- `OpenClaw.MailBridge.Client.exe status` -> success payload shape `{"ok":true,"result":{"state":"ready","mode":"safe","outlookConnected":true,"cacheStale":false,...}}`.
	- `OpenClaw.MailBridge.Client.exe list-messages --since 2026-04-06T00:00:00Z --limit 25` -> `{"ok":true,"result":{"items":[MessageDto,...]}}` ordered deterministically by newest-first message timestamp with a stable tie-breaker.
	- `OpenClaw.MailBridge.Client.exe get-message --id <bridge_id>` -> `{"ok":true,"result":MessageDto}` when present, or `{"ok":false,"error":{"code":"NOT_FOUND",...}}` when the cache has no matching row.
	- `OpenClaw.MailBridge.Client.exe list-meeting-requests --since 2026-04-06T00:00:00Z --limit 25` -> the same list envelope as messages, constrained to meeting-request item kinds/classes already normalized into the message cache.
	- `OpenClaw.MailBridge.Client.exe list-calendar --start 2026-04-07T00:00:00Z --end 2026-05-07T00:00:00Z --limit 100` -> `{"ok":true,"result":{"items":[EventDto,...]}}` ordered deterministically by ascending event start time with a stable tie-breaker.
	- `OpenClaw.MailBridge.Client.exe get-event --id <bridge_id>` -> `{"ok":true,"result":EventDto}` or `NOT_FOUND` when no cached event matches.
- Contracts and validation rules:
	- Allowed RPC methods remain exactly `get_status`, `list_recent_messages`, `get_message`, `list_recent_meeting_requests`, `list_calendar_window`, and `get_event`.
	- `since`, `start`, and `end` inputs must parse as valid ISO-8601 timestamps; `start` must be strictly earlier than `end`.
	- `limit` must be a positive integer and must not exceed the configured hard cap used by the bridge for request/result shaping.
	- `id` must decode successfully through the existing bridge ID scheme used by `BridgeIdCodec`; malformed IDs return `INVALID_REQUEST`, not `NOT_FOUND`.
	- Unsupported method names return the existing method-not-found error, invalid parameter shapes or ranges return `INVALID_REQUEST`, cache misses return `NOT_FOUND`, Outlook-unavailable/startup failures surface through the existing bridge error codes, and the client continues to map those bridge-side outcomes to deterministic process exit codes.
	- The client must keep stdout machine-readable and reserve stderr for diagnostics so install/test scripts can reliably parse the bridge response.

## Data & State

Data flow, storage, or state changes introduced by this feature.
- Data transformations and invariants:
	- Outlook item metadata is normalized into the DTO-backed schema already present in `OpenClaw.MailBridge.Contracts` and `CacheRepository`.
	- Message identity is derived from stable Outlook identifiers (`store_id` + `entry_id`) and encoded as the existing bridge ID; duplicate observations of the same Outlook item must update the same cache row instead of creating multiple rows.
	- Inbox enumeration must normalize both `MailItem` and `MeetingItem` metadata because meeting requests are retrieved from the message-side cache rather than a separate transport.
	- Calendar enumeration must normalize appointment metadata, preserve recurrence-derived instances within the bounded window, and avoid infinite/undefined iteration behavior by never using `.Count` on recurring views.
	- `BodySanitizer` remains the preview-sanitization boundary: strip HTML, collapse whitespace, replace file paths with `[path]`, and truncate to `bodyPreviewMaxChars` before any preview value is returned or stored.
	- Privacy invariants are non-negotiable: safe mode never exposes protected fields, enhanced mode only exposes sanitized/truncated preview content and optional protected metadata already defined by the contracts, and logs never contain message/event bodies or attachment content.
- Caching or persistence details:
	- `messages` is the authoritative cache for recent mail items and meeting requests returned by the message-related RPC methods.
	- `events` is the authoritative cache for calendar-window and single-event RPC methods.
	- `scan_state` persists last successful scan timestamps and related state needed to derive overlap windows and stale-cache status.
	- Repository queries must be deterministic and cache-backed; RPC handlers query SQLite and shape responses, but they do not perform live Outlook reads.
	- Scan writes should upsert current observations, refresh `last_seen_utc`, and allow the implementation to prune rows that no longer appear in the bounded scan window without destabilizing IDs for still-valid cached items.
	- `BridgeStateStore` continues to expose lifecycle state (`starting`, `waiting_for_outlook`, `ready`, `degraded`, `error`) plus stale-cache flags and reasons that explain whether cached data is current or degraded.
- Migration or backfill requirements (if any):
	- No new storage technology or sidecar project is allowed; the existing SQLite database remains the only cache store.
	- Because the schema already contains `messages`, `events`, and `scan_state`, implementation should prefer additive or compatible schema updates rather than destructive rebuilds.
	- No manual backfill step is required for operators beyond letting the first successful Inbox and Calendar scans repopulate the cache after deployment.

## Constraints & Risks

- The topology is fixed to `src/OpenClaw.MailBridge`, `src/OpenClaw.MailBridge.Client`, `src/OpenClaw.MailBridge.Contracts`, `tests/OpenClaw.MailBridge.Tests`, `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1`, and `docs/mailbridge-runbook.md`; do not add unrelated projects.
- Do not replace the architecture with HTTP, WebSocket, Microsoft Graph, EWS, or a Windows service.
- Scope remains local-only, read-only Windows Outlook metadata access; do not add send, write, reply, accept, decline, create, reschedule, folder-browse, or attachment-read capabilities.
- Key implementation risks are Outlook COM/threading correctness, recurring calendar enumeration edge cases, named-pipe ACL determinism, SQLite cache consistency, and falsely claiming completion from partial tests instead of spec-complete end-to-end behavior.
- Real Outlook COM verification requires Windows, classic Outlook, a configured profile, and an interactive session; the repo can and should automate pure logic and orchestration coverage, but operator acceptance remains necessary for the COM boundary and scheduled-task topology.
- Explicit named-pipe ACL support and `openclaw-svc` access must remain compatible with `PipeSecurity`; do not use pipe options that nullify the requested security descriptor.
- Existing docs already advertise capabilities that the current runtime does not yet implement, so rollout must avoid claiming success until cache-backed RPC methods, scripts, and runbook guidance all match the actual behavior.


## Implementation Strategy

- Implementation scope (what changes, not sequencing):
	- Retarget the four existing project files to `net10.0-windows`.
	- Complete `CacheRepository.cs` so it persists and queries `messages`, `events`, and `scan_state` rather than scan-state only.
	- Complete `OutlookScanner.cs` so it enumerates the default Inbox and Calendar, applies the research-backed filtering rules, normalizes metadata, and updates the cache through the repository.
	- Update `ScanWorker.cs` so Inbox and Calendar polling cadence are both honored while still running every Outlook operation through `OutlookStaExecutor`.
	- Update `PipeRpcWorker.cs` so each supported method validates parameters deterministically, serves repository-backed payloads, and hard-fails pipe ACL setup when required identities cannot be resolved.
	- Update `src/OpenClaw.MailBridge.Client/Program.cs`, existing scripts, and `docs/mailbridge-runbook.md` so the client, install/test flows, and operator docs reflect the completed runtime rather than the current placeholders.
- New classes/functions/commands to add or update:
	- Extend the existing repository/query surface in `CacheRepository.cs` for list/get message and event operations plus scan-state helpers.
	- Add or expand internal helper methods in `OutlookScanner.cs` (or tightly scoped internal helper files under `src/OpenClaw.MailBridge`) for Inbox filter building, Calendar bounded filtering, DTO projection, redaction shaping, and disciplined COM release.
	- Keep the client command set unchanged; if a pipe-name override is added, it must be an optional flag on the existing client rather than a new executable or transport surface.
- Dependency changes (new/removed packages) and rationale:
	- No transport or project-topology dependency changes are allowed.
	- Prefer the existing dependency profile and late-bound COM seam already present in `ComActiveObject.cs`.
	- If typed Outlook interop is introduced to reduce risk around recurring appointments and COM property access, it must be limited to `src/OpenClaw.MailBridge`, justified in the project file, and must not change the external topology or contract surface.
- Logging/telemetry additions and locations:
	- Add structured logs in the bridge host for state transitions, Outlook acquisition attempts, Inbox/Calendar scan completion counts, stale-cache transitions, and named-pipe startup/validation failures.
	- Log request-validation failures and bridge-side error mapping in `PipeRpcWorker.cs` without echoing message bodies, event bodies, previews beyond sanitized/truncated output, or attachment content.
	- Keep client diagnostics on stderr only and do not add telemetry that requires a new service, network dependency, or operator secret.
- Rollout plan (feature flags, staged deploys, fallback path):
	- No feature flag or alternate topology is introduced.
	- Roll out by completing the existing bridge, verifying deterministic MSTest/script coverage, then running the Windows operator acceptance suite against an installed bridge in the interactive-session topology.
	- Safe mode remains the default installation mode; enhanced mode is opt-in and should only be enabled after operator validation.
	- If preflight checks, ACL setup, or Outlook acquisition fail, the bridge should fail explicitly and the runbook/scripts should direct operators to fix the environment rather than silently running in a partially functional state.

## Definition of Done

- [x] Acceptance criteria documented and mapped to named coverage in `tests.OpenClaw.MailBridge.Tests` and the acceptance-suite entry points in `scripts/test-mailbridge.ps1`
- [ ] Behavior matches acceptance criteria in the documented Windows interactive-session environment and the cache-backed RPC surface no longer returns placeholder payloads for supported non-status methods
- [x] Tests updated/added for helper logic, repository persistence/query semantics, RPC validation/error mapping, privacy shaping, stale-cache handling, client exit-code behavior, and script assertions
- [x] Edge cases and error handling covered by tests, including Outlook-unavailable states, ACL resolution failures, recurring-calendar caps, malformed request inputs, and cache-miss behavior
- [x] Docs updated in `README.md`, `docs/mailbridge-runbook.md`, and the active feature docs so published behavior matches the shipped implementation
- [x] Telemetry/logging added or updated where runtime state changes or validation failures require operator-visible diagnostics, without logging protected message or event content
- [x] Toolchain pass completed (format → lint → type-check → test) for the touched runtime, client, contracts, tests, and script surfaces

## Seeded Test Conditions (from potential)
- [ ] Unit coverage for DTO/ID helpers, strict configuration validation, redaction, sanitization/truncation, safe vs enhanced shaping, repository persistence/query logic, stale-cache rules, and RPC validation/error mapping
	- Include malformed bridge IDs, invalid ISO timestamps, out-of-range limits, null/missing required arguments, and deterministic error-code assertions.
- [ ] Integration scenarios for primary interactive-session Outlook acquisition, dedicated STA orchestration, Inbox overlap/dedupe behavior, Calendar recurrence windows and hard caps, named-pipe ACL failure handling, and client JSON/exit-code behavior
	- Include explicit proof that all Outlook access stays on the STA executor and that pipe startup fails when required SID or ACL resolution cannot be completed.
- [ ] CLI/API examples for each supported client command, JSON-only stdout, stderr diagnostics, successful cached reads, stale-cache responses, invalid request handling, and automated acceptance suite A-F entry points
	- Keep examples aligned with the real client syntax and with the installed-script acceptance flow described in `docs/mailbridge-runbook.md`.
