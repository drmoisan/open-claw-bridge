# evolve-hostadapter-graph-surface - Plan

- **Issue:** #76
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-12T22-21
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature

## Required References

- Policy reading order: `.claude/skills/policy-compliance-order/SKILL.md`
- General code change policy: `.claude/rules/general-code-change.md`
- General unit test policy: `.claude/rules/general-unit-test.md`
- C# code standards: `.claude/rules/csharp.md`
- Quality tiers / gate matrix: `.claude/rules/quality-tiers.md`
- Architecture boundaries: `.claude/rules/architecture-boundaries.md`
- Evidence and timestamp conventions: `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`

**All work must comply with these policies; do not duplicate their content here.**

## Authoritative Inputs

- Spec: `docs/features/active/evolve-hostadapter-graph-surface-76/spec.md`
- User story / AC source of record: `docs/features/active/evolve-hostadapter-graph-surface-76/user-story.md`
- Research: `artifacts/research/issue-76-graph-surface.md`

## Locked Design Decisions (operator-confirmed; do not re-open)

- D1: KEEP `ListMeetingRequestsAsync`; only its wire route changes to the Graph-shaped messages-filtered form. Do not touch the bridge RPC chain, `MessagePollingWorker`, or Core UI.
- D2: Add `MailboxId` to `src/OpenClaw.HostAdapter/HostAdapterOptions.cs`, default `"me"`; the `{id}` path segment is sourced from it.
- D3: single event `GET /users/{id}/events/{eventId}`.
- D4: messages list `GET /users/{id}/messages?$filter=receivedDateTime ge {iso}&$top={limit}`; single message `/users/{id}/messages/{messageId}`; calendar `GET /users/{id}/calendarView?startDateTime={iso}&endDateTime={iso}&$top={limit}`. Status stays `GET /status`.
- D5: add `<Version>1.0.0</Version>` to `src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj`.
- D6: `OpenClawOptions.HostAdapter.BaseUrl` default -> `http://host.docker.internal:4319/` in `src/OpenClaw.Core/CoreOptions.cs`.
- Envelope/DTOs unchanged.

## Design Notes Captured During Planning (must be honored by the executor)

- N1 (route collision): the messages list and the meeting-requests list both resolve to the route template `/users/{id}/messages` and differ only by `$filter` content. ASP.NET Core minimal APIs cannot register two handlers with the same `{HTTP method, route template}`. Therefore `Program.cs` MUST register **one** `GET /users/{id}/messages` handler that inspects the `$filter` value: when the filter contains the `meetingMessageType ne null` predicate it dispatches `commandBuilder.BuildListMeetingRequests(...)`; otherwise it dispatches `commandBuilder.BuildListMessages(...)`. The receivedDateTime lower bound and `$top` are parsed the same way in both branches. The `ListMeetingRequestsAsync` interface method and the `BuildListMeetingRequests` command chain are unchanged per D1.
- N2 (Core-side MailboxId): `OpenClaw.Core` must render `/users/{id}/...` paths. Per architecture-boundaries Rule 6, Core must not reference `OpenClaw.HostAdapter` (the web project that owns `HostAdapterOptions.MailboxId`). The minimal approach is to add a `MailboxId` property (default `"me"`) to the Core-side `HostAdapterOptions` in `src/OpenClaw.Core/CoreOptions.cs` and read it in `HostAdapterHttpClient`. This keeps the constant configurable and consistent with the adapter default without crossing a project boundary. (A hardcoded `"me"` constant is the fallback if a config property is judged out of scope; the config property is the recommended choice and is what this plan specifies.)
- N3 (signatures unchanged): `IHostAdapterClient` method signatures keep the same C# shape (`sinceUtc`, `limit`, `bridgeId`, `startUtc`/`endUtc`, `requestId`, `cancellationToken`). Only XML-doc text and the implementation's emitted wire strings change. `HostAdapterSchedulingService` calls only `GetMessageAsync`, `GetEventAsync`, and `ListCalendarWindowAsync`; with signatures unchanged this file is review-only (no functional edit expected).
- N4 (validation parameter names): `HostAdapterRequestValidation` currently reads `since`/`start`/`end`/`limit` by name and emits diagnostics that quote those names. After the change it reads the receivedDateTime lower bound parsed from `$filter`, `startDateTime`, `endDateTime`, and `$top`, and the diagnostic message strings must quote the new parameter names. All existing validation semantics (UTC round-trip parse with zero offset, limit `>0` and `<= MaxLimit` bounds, window `end > start`, required-parameter errors) are preserved.
- N5 (file size): `src/OpenClaw.HostAdapter/Program.cs` is 465 lines today. Consolidating the two `/messages` handlers into one (N1) reduces duplicated handler bodies, but the `/users/{id}/...` rewrites add an `id` route parameter and `$filter` parsing. The executor MUST verify `Program.cs` stays `< 500` lines after the edit; if it would exceed 500, extract the messages-handler body into a small internal helper in a new file rather than letting the route file grow past the limit.
- N6 (status route): `/v1/status` becomes `/status` only. It is the sole non-Graph route and keeps its handler body unchanged apart from the template string.

## Evidence Locations (canonical; non-overridable)

All evidence artifacts for this feature are written under:
`docs/features/active/evolve-hostadapter-graph-surface-76/evidence/<kind>/`

- Baseline: `.../evidence/baseline/`
- QA gates: `.../evidence/qa-gates/`
- Regression testing: `.../evidence/regression-testing/`
- Other: `.../evidence/other/`

Writing evidence to `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, or any other non-canonical path is a policy violation. If any caller instruction supplies such a path, reject it, substitute the canonical path, and record `EVIDENCE_LOCATION_OVERRIDE_REJECTED: <supplied> replaced with <canonical>`.

Timestamp format for all artifacts: `yyyy-MM-ddTHH-mm` (ISO-8601).

## Toolchain Command Reference (C#)

- Format: `dotnet csharpier .`
- Build / lint (analyzers + code style): `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
- Type check (nullable as errors): `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
- Test + coverage: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
- Architecture: verify no new `ProjectReference` edges in `*.csproj`; Core depends only on `OpenClaw.HostAdapter.Contracts`; HostAdapter depends only on `OpenClaw.HostAdapter.Contracts` + `OpenClaw.MailBridge.Contracts`.

---

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture and Policy Reading

- [x] [P0-T1] Read the repository policy files in required order and record an evidence artifact.
  - Files to read: `.claude/skills/policy-compliance-order/SKILL.md`, `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/architecture-boundaries.md`.
  - Acceptance: artifact `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/other/phase0-instructions-read.md` exists and contains `Timestamp:`, `Policy Order:`, and the explicit list of files read.

- [x] [P0-T2] Capture baseline formatting state.
  - Command: `dotnet csharpier . --check`
  - Acceptance: artifact `.../evidence/baseline/baseline-format.<timestamp>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (pass/fail and any files needing formatting).

- [x] [P0-T3] Capture baseline build/analyzer state.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
  - Acceptance: artifact `.../evidence/baseline/baseline-build.<timestamp>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (warning/error counts).

- [x] [P0-T4] Capture baseline nullable type-check state.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
  - Acceptance: artifact `.../evidence/baseline/baseline-typecheck.<timestamp>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.

- [x] [P0-T5] Capture baseline test + coverage state for the full solution.
  - Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - Acceptance: artifact `.../evidence/baseline/baseline-test.<timestamp>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric headline line-coverage % and branch-coverage % and total passed/failed counts. These numbers are the no-regression reference for Phase 3.

### Phase 1 — HostAdapter Side: Graph-Shaped Routes, Validation, Options, Version (HostAdapter.Tests green)

- [x] [P1-T1] Add the `MailboxId` option to `src/OpenClaw.HostAdapter/HostAdapterOptions.cs`.
  - Change: add a public `string MailboxId { get; set; } = "me";` property (default `"me"`); follow the existing default-fallback pattern if normalized in `Program.cs` post-configure.
  - Acceptance: `HostAdapterOptions` exposes `MailboxId` with default `"me"`; file compiles; file remains `< 500` lines.

- [x] [P1-T2] Normalize `MailboxId` in the `Program.cs` options post-configure block.
  - File: `src/OpenClaw.HostAdapter/Program.cs` (the `.PostConfigure` block).
  - Change: set `options.MailboxId = string.IsNullOrWhiteSpace(options.MailboxId) ? "me" : options.MailboxId;`.
  - Acceptance: an empty or whitespace `MailboxId` resolves to `"me"`; no other post-configure behavior changed.

- [x] [P1-T3] Update `src/OpenClaw.HostAdapter/HostAdapterRequestValidation.cs` to read and quote the Graph-shaped parameter names.
  - Change: parameter-name strings and diagnostic messages move from `since`/`start`/`end`/`limit` to the receivedDateTime lower bound (parsed from `$filter`), `startDateTime`, `endDateTime`, and `$top`. Preserve all parse/bounds/window semantics unchanged (UTC round-trip, zero offset, `>0`, `<= MaxLimit`, `end > start`, required-parameter error). The window error message updates from quoting `start`/`end` to `startDateTime`/`endDateTime`.
  - Acceptance: validation helpers accept the new parameter names; error messages quote the new names; bounds/window semantics unchanged; file remains `< 500` lines.

- [x] [P1-T4] Rewrite the status route in `src/OpenClaw.HostAdapter/Program.cs` from `/v1/status` to `/status`.
  - Acceptance: `app.MapGet("/status", ...)` registered; handler body unchanged; no `/v1/status` template remains.

- [x] [P1-T5] Rewrite the messages list and meeting-requests routes into a single `GET /users/{id}/messages` handler per design note N1.
  - File: `src/OpenClaw.HostAdapter/Program.cs`.
  - Change: register one `GET /users/{id}/messages` handler that parses the receivedDateTime lower bound from `$filter` and `$top`, then branches: if `$filter` contains the `meetingMessageType ne null` predicate, invoke `commandBuilder.BuildListMeetingRequests(sinceUtc, limit)`; otherwise invoke `commandBuilder.BuildListMessages(sinceUtc, limit)`. Remove the former `/v1/messages` and `/v1/meeting-requests` registrations.
  - Acceptance: exactly one `/users/{id}/messages` route is registered; meeting-requests dispatch is selected by the `meetingMessageType ne null` predicate; `BuildListMessages` and `BuildListMeetingRequests` command chains are both reachable and unchanged; no `/v1/messages` or `/v1/meeting-requests` template remains.

- [x] [P1-T6] Rewrite the single-message route to `GET /users/{id}/messages/{messageId}` in `src/OpenClaw.HostAdapter/Program.cs`.
  - Change: template becomes `/users/{id}/messages/{messageId}`; the route value passed to `TryGetBridgeId`/`BuildGetMessage` is `messageId`. The `{id}` segment is not bridge-id validated.
  - Acceptance: route registered at `/users/{id}/messages/{messageId}`; single-message lookup uses the `messageId` segment; no `/v1/messages/{bridgeId}` template remains.

- [x] [P1-T7] Rewrite the calendar route to `GET /users/{id}/calendarView` with `startDateTime`/`endDateTime`/`$top` in `src/OpenClaw.HostAdapter/Program.cs`.
  - Change: template becomes `/users/{id}/calendarView`; read `startDateTime`, `endDateTime`, `$top` from the query; preserve window validation and `BuildListCalendar(startUtc, endUtc, limit)` dispatch.
  - Acceptance: route registered at `/users/{id}/calendarView`; query params are `startDateTime`/`endDateTime`/`$top`; no `/v1/calendar` template remains.

- [x] [P1-T8] Rewrite the single-event route to `GET /users/{id}/events/{eventId}` in `src/OpenClaw.HostAdapter/Program.cs`.
  - Change: template becomes `/users/{id}/events/{eventId}`; route value passed to `TryGetBridgeId`/`BuildGetEvent` is `eventId`.
  - Acceptance: route registered at `/users/{id}/events/{eventId}`; no `/v1/events/{bridgeId}` template remains.

- [x] [P1-T9] Verify `src/OpenClaw.HostAdapter/Program.cs` stays under 500 lines after the route rewrites (design note N5).
  - Acceptance: `Program.cs` line count `< 500`. If `>= 500`, extract the messages-handler body into a small internal helper in a new file under `src/OpenClaw.HostAdapter/` and re-verify; record the count.

- [x] [P1-T10] Add `<Version>1.0.0</Version>` to `src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj` (D5).
  - Change: add `<Version>1.0.0</Version>` inside the existing `<PropertyGroup>`.
  - Acceptance: the csproj declares `<Version>1.0.0</Version>`; `HostAdapterOptions.DefaultAdapterVersion` resolves to `1.0.0` from the assembly version.

- [x] [P1-T11] Update XML-doc comments in `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` to describe the Graph-shaped routes (signatures unchanged per N3).
  - Change: revise `<summary>`/`<param>` text on `ListMessagesAsync`, `GetMessageAsync`, `ListMeetingRequestsAsync`, `ListCalendarWindowAsync`, `GetEventAsync`, `GetStatusAsync` to reference the Graph-shaped routes. Do not remove, rename, or re-order any method or parameter.
  - Acceptance: all six methods retain their current signatures; doc text references the Graph-shaped routes; `ListMeetingRequestsAsync` is retained.

- [x] [P1-T12] Update `tests/OpenClaw.HostAdapter.Tests/HostAdapterEndpointTests.cs` route strings to the Graph-shaped paths.
  - Change: `/v1/messages/{...}` -> `/users/me/messages/{...}`; `/v1/events/{...}` -> `/users/me/events/{...}`. Do not weaken assertions.
  - Acceptance: tests reference Graph-shaped paths; assertions unchanged in strength.

- [x] [P1-T13] Update `tests/OpenClaw.HostAdapter.Tests/HostAdapterAuthTests.cs` status route strings.
  - Change: `/v1/status` -> `/status` at both call sites.
  - Acceptance: tests reference `/status`; auth assertions unchanged.

- [x] [P1-T14] Update `tests/OpenClaw.HostAdapter.Tests/HostAdapterMappingTests.cs` route and query-param strings.
  - Change: `/v1/messages?since=...&limit=...` -> `/users/me/messages?$filter=receivedDateTime ge ...&$top=...`.
  - Acceptance: tests reference the Graph-shaped messages route and parameters; mapping assertions unchanged.

- [x] [P1-T15] Update `tests/OpenClaw.HostAdapter.Tests/HostAdapterValidationTests.cs` route and query-param strings, including the over-limit and window cases.
  - Change: messages route/params to `$filter=receivedDateTime ge ...&$top=...`; calendar route to `/users/me/calendarView?startDateTime=...&endDateTime=...`; over-limit `$top` case preserved; expected error messages updated to quote the new parameter names.
  - Acceptance: validation tests assert the same status codes and error semantics against the Graph-shaped parameter names; no assertion weakened.

- [x] [P1-T16] Update `tests/OpenClaw.HostAdapter.Tests/HostAdapterEnvelopeTests.cs` status route string.
  - Change: `/v1/status` -> `/status`.
  - Acceptance: envelope test references `/status`; assertions unchanged.

- [x] [P1-T17] Add a HostAdapter test asserting the meeting-requests dispatch on `/users/{id}/messages` with the `meetingMessageType ne null` filter predicate.
  - File: `tests/OpenClaw.HostAdapter.Tests/HostAdapterMappingTests.cs` (or the most appropriate existing HostAdapter.Tests file that already exercises the messages route).
  - Change: add a test issuing `GET /users/me/messages?$filter=meetingMessageType ne null and receivedDateTime ge {iso}&$top={n}` and asserting the meeting-requests command path is taken (distinct from the plain messages path), preserving the existing envelope shape.
  - Acceptance: a test exists that verifies the meeting-requests branch is selected by the `meetingMessageType ne null` predicate and the plain-messages branch is selected without it.

- [x] [P1-T18] Add or update a HostAdapter test asserting `meta.adapterVersion` reports `1.0.0` from the assembly version, OR document that the test factory overrides `AdapterVersion` to `"test-version"` and verify the version through `HostAdapterOptions.DefaultAdapterVersion` instead.
  - File: `tests/OpenClaw.HostAdapter.Tests/` (the most appropriate existing file; `HostAdapterTestWebApplicationFactory.cs:49` sets `AdapterVersion = "test-version"`).
  - Acceptance: a test confirms `HostAdapterOptions.DefaultAdapterVersion == "1.0.0"` (assembly-version-derived). If the in-process test version is overridden by the factory, the test asserts the `DefaultAdapterVersion` value directly, not the overridden envelope value.

- [x] [P1-T19] Run the C# toolchain for the HostAdapter changes and record the QA-gate artifacts.
  - Commands (in order; restart from format if any step changes files or fails): `dotnet csharpier .` ; `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` ; `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` ; `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.
  - Acceptance: all four commands exit `0` in a single pass; artifacts `.../evidence/qa-gates/phase1-format.<timestamp>.md`, `phase1-build.<timestamp>.md`, `phase1-typecheck.<timestamp>.md`, `phase1-test.<timestamp>.md` each contain `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; the test artifact records numeric line/branch coverage headline values. HostAdapter.Tests pass.

### Phase 2 — Core Side: HTTP Client Paths, BaseUrl Default, MailboxId Mirror (Core.Tests green)

- [x] [P2-T1] Change the `BaseUrl` default in `src/OpenClaw.Core/CoreOptions.cs` to drop `/v1/` (D6).
  - Change: `BaseUrl` default from `http://host.docker.internal:4319/v1/` to `http://host.docker.internal:4319/`.
  - Acceptance: the Core `HostAdapterOptions.BaseUrl` default no longer contains `/v1/`.

- [x] [P2-T2] Add a `MailboxId` property (default `"me"`) to the Core-side `HostAdapterOptions` in `src/OpenClaw.Core/CoreOptions.cs` (design note N2).
  - Change: add `public string MailboxId { get; set; } = "me";` to the Core `HostAdapterOptions` class.
  - Acceptance: the Core-side `HostAdapterOptions` exposes `MailboxId` with default `"me"`; no new project reference is introduced.

- [x] [P2-T3] Rewrite the six relative paths in `src/OpenClaw.Core/HostAdapterHttpClient.cs` to the Graph-shaped forms, sourcing the `{id}` segment from `options.HostAdapter.MailboxId`.
  - Changes:
    - `GetStatusAsync`: `"status"`.
    - `ListMessagesAsync`: `$"users/{id}/messages?$filter=receivedDateTime ge {iso}&$top={limit}"` (URL-encode as needed).
    - `GetMessageAsync`: `$"users/{id}/messages/{escapedMessageId}"`.
    - `ListMeetingRequestsAsync`: `$"users/{id}/messages?$filter=meetingMessageType ne null and receivedDateTime ge {iso}&$top={limit}"`.
    - `ListCalendarWindowAsync`: `$"users/{id}/calendarView?startDateTime={iso}&endDateTime={iso}&$top={limit}"`.
    - `GetEventAsync`: `$"users/{id}/events/{escapedEventId}"`.
  - The `{id}` value is `options.HostAdapter.MailboxId`. Preserve `Uri.EscapeDataString` on id/timestamp values consistent with current escaping.
  - Acceptance: all six relative paths emit Graph-shaped forms with the `{id}` segment from `MailboxId`; method signatures unchanged; the meeting-requests path carries the `meetingMessageType ne null` predicate.

- [x] [P2-T4] Review `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` call sites against the revised `IHostAdapterClient` (design note N3).
  - Acceptance: confirm the file calls only `GetMessageAsync`, `GetEventAsync`, `ListCalendarWindowAsync` with unchanged signatures; record that no functional edit is required (or apply the minimal edit if a call shape changed, with justification).

- [x] [P2-T5] Update `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` base URL, path, and query-param assertions.
  - Changes: base URL `http://localhost:4319/v1/` -> `http://localhost:4319/`; status path assertion `/v1/status` -> `/status`; messages path assertion to contain `users/me/messages` and `$filter=receivedDateTime` and `$top=`; meeting-requests assertion to contain `users/me/messages` and `meetingMessageType ne null`; calendar assertion to contain `calendarView`, `startDateTime=`, `endDateTime=`, `$top=`; single message/event paths to `users/me/messages/{...}` and `users/me/events/{...}`. Do not weaken assertions.
  - Acceptance: client tests assert the Graph-shaped paths/parameters and the new base URL; `since=`/`start=`/`end=`/`limit=`/`meeting-requests`/`/v1/` substrings no longer asserted; assertion strength preserved.

- [x] [P2-T6] Review and update `tests/OpenClaw.Core.Tests/MessagePollingWorkerTests.cs` and `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` mock setups.
  - Acceptance: because `ListMeetingRequestsAsync` and all other signatures are retained, confirm the existing Moq setups remain valid; update any literal route/base-url strings if present; record that no mock signature change is required (or apply the minimal change with justification). Do not weaken assertions.

- [x] [P2-T7] Scan `tests/OpenClaw.Core.Tests/` (including `tests/OpenClaw.Core.Tests/CoreTestWebApplicationFactory.cs`) for any remaining references to old route strings or the `/v1/` base URL and update them.
  - Search targets: `/v1/`, `since=`, `start=`, `end=`, `limit=`, `meeting-requests`, `4319/v1/`.
  - Acceptance: no Core test asserts a removed route string or the `/v1/` base URL except where intentionally testing rejection; any remaining occurrence is justified in the task record.

- [x] [P2-T8] Update `tests/OpenClaw.Core.Tests/CoreTestWebApplicationFactory.cs` base URL from "http://127.0.0.1:4319/v1/" to "http://127.0.0.1:4319/" (D6 consistency).
  - Acceptance: the Core test factory's HostAdapter BaseUrl configuration no longer contains /v1/; no other factory behavior changed.

- [x] [P2-T9] Run the full C# toolchain for the Core changes and record the QA-gate artifacts.
  - Commands (in order; restart from format if any step changes files or fails): `dotnet csharpier .` ; `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` ; `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` ; `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.
  - Acceptance: all four commands exit `0` in a single pass; artifacts `.../evidence/qa-gates/phase2-format.<timestamp>.md`, `phase2-build.<timestamp>.md`, `phase2-typecheck.<timestamp>.md`, `phase2-test.<timestamp>.md` each contain `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; the test artifact records numeric line/branch coverage headline values. Core.Tests pass.

### Phase 3 — Full-Solution QA, Coverage Verification, Contract/Schema Check, AC Mapping

- [x] [P3-T1] Run the full final-QA formatting gate.
  - Command: `dotnet csharpier .`
  - Acceptance: exit `0` with no files reformatted; artifact `.../evidence/qa-gates/final-format.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. If files are reformatted, restart the loop from this task.

- [x] [P3-T2] Run the full final-QA build/analyzer gate.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
  - Acceptance: exit `0` with 0 analyzer errors; artifact `.../evidence/qa-gates/final-build.<timestamp>.md` with the required fields and warning/error counts in `Output Summary:`.

- [x] [P3-T3] Run the full final-QA nullable type-check gate.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
  - Acceptance: exit `0`; artifact `.../evidence/qa-gates/final-typecheck.<timestamp>.md` with the required fields.

- [x] [P3-T4] Run the full final-QA test + coverage gate for the whole solution.
  - Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - Acceptance: exit `0`, all tests pass; artifact `.../evidence/qa-gates/final-test.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric post-change line-coverage % and branch-coverage % and total passed/failed counts.

- [x] [P3-T5] Verify coverage thresholds and no-regression on changed code.
  - Inputs: baseline coverage from `.../evidence/baseline/baseline-test.<timestamp>.md` (P0-T5) and post-change coverage from `.../evidence/qa-gates/final-test.<timestamp>.md` (P3-T4).
  - Acceptance: artifact `.../evidence/qa-gates/coverage-delta.<timestamp>.md` records baseline line/branch %, post-change line/branch %, and changed-code coverage; confirms line coverage `>= 85%`, branch coverage `>= 75%`, and no regression on changed lines. If any threshold is unmet, the outcome is remediation-required, not PASS.

- [x] [P3-T6] Verify architecture boundaries are unchanged.
  - Check: no new `ProjectReference` edges in any `*.csproj`; `OpenClaw.Core` references only `OpenClaw.HostAdapter.Contracts`; `OpenClaw.HostAdapter` references only `OpenClaw.HostAdapter.Contracts` + `OpenClaw.MailBridge.Contracts`; `OpenClaw.HostAdapter.Contracts` references only `OpenClaw.MailBridge.Contracts`; no COM boundary crossed.
  - Acceptance: artifact `.../evidence/qa-gates/architecture-check.<timestamp>.md` records the inspected `ProjectReference` sets and confirms no new edges.

- [x] [P3-T7] Perform the contract/schema compatibility check for the T2 `IHostAdapterClient` breaking change.
  - Check: confirm `IHostAdapterClient` retains all six members with unchanged C# signatures (including `ListMeetingRequestsAsync` per D1); confirm `HostAdapterHttpClient` implements every member and emits the Graph-shaped wire routes; confirm the envelope/DTO contracts (`ApiEnvelope<T>`, `ItemsResponse<T>`, `MessageDto`, `EventDto`, `ApiMeta`, `ApiError`, `BridgeStatusDto`) are byte-for-byte unchanged; confirm the adapter version is `1.0.0` (major bump signaling the breaking HTTP surface change).
  - Acceptance: artifact `.../evidence/qa-gates/contract-compat.<timestamp>.md` records the member-by-member implementation confirmation, the unchanged-DTO confirmation, and the `1.0.0` version, and states that all in-repo callers (`HostAdapterHttpClient`, `HostAdapterSchedulingService`, polling workers) compile and pass.

- [x] [P3-T8] Verify no `/v1/*` route (status excepted) is served by the HostAdapter.
  - Check: grep `src/OpenClaw.HostAdapter/Program.cs` for `"/v1/`; confirm zero matches. Confirm `/status` is the only non-`/users/{id}/...` route.
  - Acceptance: artifact `.../evidence/qa-gates/route-surface-check.<timestamp>.md` records the route inventory: `/status`, `/users/{id}/messages` (single handler, plain + meeting-requests branches), `/users/{id}/messages/{messageId}`, `/users/{id}/calendarView`, `/users/{id}/events/{eventId}`; and confirms no `/v1/` template remains.

- [x] [P3-T9] Map each of the 7 acceptance criteria in `user-story.md` to the satisfying test(s)/evidence and check them off per the acceptance-criteria-tracking convention.
  - Mapping (verify each against passing tests/evidence before checking the AC box in `user-story.md`):
    - AC1 (Graph-shaped routes, no `/v1/*` except `/status`): P3-T8 route-surface-check + `HostAdapterEndpointTests.cs`, `HostAdapterAuthTests.cs`, `HostAdapterValidationTests.cs`, `HostAdapterMappingTests.cs`, `HostAdapterEnvelopeTests.cs`.
    - AC2 (`IHostAdapterClient`/`HostAdapterHttpClient` call Graph endpoints, envelope-wrapped, `ListMeetingRequestsAsync` retained): P3-T7 contract-compat + `HostAdapterHttpClientTests.cs`.
    - AC3 (`OpenClawOptions.HostAdapter.BaseUrl` default has no `/v1/`): P2-T1 + `HostAdapterHttpClientTests.cs` base-url assertion.
    - AC4 (adapter version reports `1.0.0` via `meta.adapterVersion`): P1-T10, P1-T18 + contract-compat artifact.
    - AC5 (`MailboxId` default `"me"` configurable on `HostAdapterOptions` and renders `{id}`): P1-T1/P1-T2 (adapter) and P2-T2/P2-T3 (Core client path) + the `/users/me/...` assertions in `HostAdapterHttpClientTests.cs`.
    - AC6 (existing contract/endpoint tests pass against new routes, not weakened): P1-T19, P2-T9, P3-T4 final-test artifact (all tests pass).
    - AC7 (line >= 85%, branch >= 75% on changed code, no regression): P3-T5 coverage-delta artifact.
  - Acceptance: artifact `.../evidence/qa-gates/ac-mapping.<timestamp>.md` records the AC-to-evidence map with the verifying artifact path for each AC; all 7 boxes in `user-story.md` are checked only after their evidence is confirmed PASS.

## Test Plan

- Unit (MSTest + Moq + FluentAssertions): HostAdapter.Tests assert the new route templates, `$filter`/`$top`/`startDateTime`/`endDateTime` parameter handling, the meeting-requests branch selection, validation error messages, and `DefaultAdapterVersion == "1.0.0"`. Core.Tests assert the new base URL, Graph-shaped relative paths, and `MailboxId`-sourced `{id}` segment.
- Core test-file scope: `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` (P2-T5), `tests/OpenClaw.Core.Tests/MessagePollingWorkerTests.cs` and `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` (P2-T6), and `tests/OpenClaw.Core.Tests/CoreTestWebApplicationFactory.cs` (P2-T8 base-URL update; also scanned in P2-T7).
- Contract/schema: P3-T7 confirms `IHostAdapterClient` member parity, `HostAdapterHttpClient` implementation, unchanged DTO/envelope contracts, and the `1.0.0` major bump.
- Integration: covered by the existing `WebApplicationFactory`-based HostAdapter endpoint tests exercised through the full `dotnet test` run in P3-T4.
- Coverage evidence:
  - Baseline: `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/baseline/baseline-test.<timestamp>.md`
  - Post-change: `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/qa-gates/final-test.<timestamp>.md`
  - Comparison: `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/qa-gates/coverage-delta.<timestamp>.md`

## Open Questions / Notes

- N1 route collision (single `/users/{id}/messages` handler branching on `$filter`) is the one non-obvious implementation decision; it is resolved in this plan and must be honored.
- N2 Core-side `MailboxId`: a Core-side config mirror is specified to avoid crossing the architecture boundary into `OpenClaw.HostAdapter`. A hardcoded `"me"` constant is the only acceptable fallback if the config property is later judged out of scope.
- File-size watch: `Program.cs` (currently 465 lines) is the only file near the 500-line limit; P1-T9 enforces the check.
