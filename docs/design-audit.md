# AUTHORITATIVE BASELINE

Use the provided bridge vision/spec as the controlling baseline, with exactly one version update:

- Replace every reference to .NET 8 with .NET 10
- Replace every reference to `net8.0-windows` with `net10.0-windows`
- Replace every reference to `net8.0` with `net10.0` where applicable

Do not make any other substantive changes to the original spec.

Everything else in the original vision remains authoritative.

---

# AUDIT OBJECTIVE

Prove whether the delivered implementation fully satisfies the fixed-spec Windows mail bridge architecture:

- local-only
- read-only
- Outlook COM bridge
- bridge runs in primary user’s interactive session
- service user only accesses the bridge via named-pipe client CLI
- safe mode is default
- enhanced mode is optional only
- no write/send capabilities
- no alternate transport layers
- no Outlook automation from service/SYSTEM/unattended accounts

You must audit the implementation, not restate the design.

---

# REQUIRED AUDIT METHOD

You must inspect all relevant implementation artifacts, including at minimum:

- solution / workspace structure
- all project files
- all csproj files
- source code for bridge server
- source code for client CLI
- contracts / DTOs / protocol types
- tests
- PowerShell scripts
- configuration files
- documentation / runbook

Where available, inspect:
- named pipe creation code
- pipe ACL creation code
- Outlook acquisition code
- STA thread boundary code
- COM object lifetime management
- scan logic for Inbox and Calendar
- SQLite schema creation / migrations
- redaction logic
- logging code
- CLI argument parsing
- scheduled task registration scripts
- acceptance test script implementation

If executable testing is possible in the environment, use it.
If not possible, distinguish clearly between:
- code-proven
- test-proven
- environment-blocked
- not evidenced

Do not fake runtime validation if Outlook / Windows / task scheduler / service accounts are unavailable.

---

# STRICT EVALUATION RULES

1. A requirement passes only if directly evidenced.
2. “Looks intended” is not a pass.
3. Comments are not implementation.
4. Documentation alone is not implementation unless the requirement is documentation-only.
5. Tests alone are not proof if the implementation is missing.
6. Implementation alone is not proof for runtime behavior when the requirement explicitly calls for a test/script proving it.
7. If the implementation deviates from the spec but could still work, mark FAIL.
8. If the implementation broadens scope beyond spec, record as deviation.
9. If a forbidden capability exists, that is a FAIL even if the required capabilities also exist.
10. If the delivered solution targets the wrong framework version, that is a FAIL.

---

# SPEC TO AUDIT AGAINST

Audit against the following contract.

## 1. Mission and hard constraints

The solution must build a local-only, read-only Outlook bridge for Windows that lets `openclaw-svc` read a narrow, normalized view of the primary user’s Inbox and Calendar without giving the service account:

- direct Outlook access
- mailbox credentials
- filesystem access to the primary user’s Outlook profile

Topology must be:

- `OpenClaw.MailBridge.exe` runs in primary user’s interactive Windows session
- `OpenClaw.MailBridge.Client.exe` runs under `openclaw-svc`
- OpenClaw calls the client CLI, not Outlook directly
- Bridge is read-only
- No send / reply / accept / decline / create / reschedule / folder browse APIs

Must not automate Outlook from:
- Windows service
- SYSTEM
- `openclaw-svc`
- unattended non-interactive account

Classic Outlook for Windows is the required target.

## 2. Scope boundary

Exactly these artifacts must exist:

- `src/OpenClaw.MailBridge/`
- `src/OpenClaw.MailBridge.Client/`
- `src/OpenClaw.MailBridge.Contracts/`
- `tests/OpenClaw.MailBridge.Tests/`
- `scripts/install-mailbridge.ps1`
- `scripts/uninstall-mailbridge.ps1`
- `scripts/register-mailbridge-task.ps1`
- `scripts/test-mailbridge.ps1`
- `docs/mailbridge-runbook.md`

Must not add:
- Graph
- EWS
- Outlook REST
- IMAP
- MAPI profile scraping
- PST/OST parsing
- HTTP loopback APIs
- browser auth
- write-capable Outlook methods

## 3. Technology choices

Must use:

- C#
- .NET 10
- `net10.0-windows`
- `System.IO.Pipes`
- `System.Text.Json`
- `Microsoft.Data.Sqlite`
- Outlook COM interop through installed Outlook PIA / Object Library

Must not rely on `PipeOptions.CurrentUserOnly`.

## 4. Threading model

All Outlook COM access must run on one dedicated STA thread.

Must show:
- dedicated worker thread
- `SetApartmentState(ApartmentState.STA)` before start
- request queue / marshaling of Outlook work onto STA thread
- no Outlook COM access from thread pool threads

## 5. Outlook session acquisition

Must implement exact sequence on STA thread:

1. check whether Outlook is already running
2. if yes, use `Marshal.GetActiveObject("Outlook.Application")`
3. if no, create new Outlook application, get MAPI namespace, call logon to default profile

Must expose config:
- `autostartOutlook`

Behavior:
- true => launch allowed
- false => state remains `waiting_for_outlook`

## 6. Outlook access rules

Must use default Inbox and Calendar only.

### Mail scan
Must implement:
- default Inbox
- `folder.Items`
- `Items.Restrict`
- filter on `ReceivedTime`
- 30 second poll cadence
- 5 minute overlap window
- dedupe by `EntryID`

Must not filter Restrict on unsupported properties like:
- Body
- EntryID
- HTMLBody

### Calendar scan
Must implement:
1. default Calendar
2. `items.Sort("[Start]")`
3. `items.IncludeRecurrences = true`
4. overlap filter `[Start] <= end AND [End] >= start`

Must not use `.Count` on recurring collection.
Must enumerate with hard cap.

### Meeting requests
Must normalize `MeetingItem` as `meeting_message`.
If linking appointment, must call `GetAssociatedAppointment(false)`.
Must never pass true.

## 7. COM lifetime rules

Must use fresh COM references every scan.
Must not cache:
- RCWs
- Outlook COM objects
- COM objects in DB/cache

Must show:
- no inline chained COM calls
- local variables per COM object
- reverse-order release
- `Marshal.FinalReleaseComObject` or equivalent release loop
- nulling references after release

## 8. Data model

SQLite path must be under:
- `%LOCALAPPDATA%\OpenClaw\MailBridge\cache.db`

Must create exact tables:
- `messages`
- `events`
- `scan_state`

Audit the schema fields exactly.

### messages required columns
- `bridge_id TEXT PRIMARY KEY`
- `entry_id TEXT NOT NULL`
- `store_id TEXT NULL`
- `item_kind TEXT NOT NULL`
- `subject TEXT NULL`
- `received_utc TEXT NULL`
- `sent_utc TEXT NULL`
- `importance INTEGER NULL`
- `sensitivity INTEGER NULL`
- `unread INTEGER NOT NULL`
- `has_attachments INTEGER NOT NULL`
- `message_class TEXT NULL`
- `sender_name TEXT NULL`
- `sender_email TEXT NULL`
- `to_json TEXT NULL`
- `cc_json TEXT NULL`
- `body_preview TEXT NULL`
- `protected_fields_available INTEGER NOT NULL`
- `is_redacted INTEGER NOT NULL`
- `last_seen_utc TEXT NOT NULL`

### events required columns
- `bridge_id TEXT PRIMARY KEY`
- `entry_id TEXT NULL`
- `store_id TEXT NULL`
- `global_appointment_id TEXT NULL`
- `item_kind TEXT NOT NULL`
- `subject TEXT NULL`
- `start_utc TEXT NOT NULL`
- `end_utc TEXT NOT NULL`
- `location TEXT NULL`
- `busy_status INTEGER NULL`
- `meeting_status INTEGER NULL`
- `is_recurring INTEGER NOT NULL`
- `sensitivity INTEGER NULL`
- `organizer TEXT NULL`
- `required_attendees_json TEXT NULL`
- `optional_attendees_json TEXT NULL`
- `resources_json TEXT NULL`
- `body_preview TEXT NULL`
- `protected_fields_available INTEGER NOT NULL`
- `is_redacted INTEGER NOT NULL`
- `last_modified_utc TEXT NULL`
- `last_seen_utc TEXT NOT NULL`

### scan_state required keys
- `last_inbox_scan_utc`
- `last_calendar_scan_utc`
- `schema_version`

## 9. ID strategy

Must implement exact ID forms:

Mail:
- `msg:<base64url(entry_id)>`

Meeting-message:
- `mtg:<base64url(entry_id)>`

Calendar:
- `evt:<base64url(global_appointment_id)>:<start_utc>`
- fallback `evt:<base64url(entry_id)>:<start_utc>`

Must prefer `GlobalAppointmentID` where available.

## 10. Redaction and privacy policy

Must use Outlook `Sensitivity`.

If private/confidential:
- `is_redacted=true`

Mail redaction:
- subject becomes `"Private message"`
- sender fields null
- recipient fields null
- body preview null

Calendar redaction:
- subject becomes `"Private appointment"`
- location null
- organizer null
- attendees/resources null
- body preview null
- preserve only `start_utc`, `end_utc`, `busy_status`, `is_recurring`

If not private/confidential:
- strip HTML
- normalize plain text
- truncate to 500 chars
- never expose attachment contents
- never expose filesystem paths discovered in bodies

Must never log message content or body preview.

## 11. Safe vs enhanced field sets

Default installed mode must be `safe`.

### Safe mail fields
Only:
- `bridge_id`
- `item_kind`
- `subject`
- `received_utc`
- `sent_utc`
- `importance`
- `sensitivity`
- `unread`
- `has_attachments`
- `message_class`
- `protected_fields_available=false`
- `is_redacted`

### Safe calendar fields
Only:
- `bridge_id`
- `global_appointment_id`
- `subject`
- `start_utc`
- `end_utc`
- `location`
- `busy_status`
- `meeting_status`
- `is_recurring`
- `sensitivity`
- `protected_fields_available=false`
- `is_redacted`

### Enhanced optional fields
Mail:
- `sender_name`
- `sender_email`
- `to_json`
- `cc_json`
- `body_preview`

Calendar:
- `organizer`
- attendee/resource fields
- `body_preview`

Enhanced must not be default.

## 12. Named-pipe design

Pipe name:
- `\\.\pipe\openclaw_mailbridge_v1`

Protocol:
- UTF-8 JSON
- one request per message
- one response per message
- message mode
- max request 64 KB
- max response 1 MB
- max concurrent server instances 4

Must use explicit `PipeSecurity` with:
- SYSTEM full control
- Administrators full control
- primary user SID read/write
- `openclaw-svc` SID read/write
- deny `NT AUTHORITY\NETWORK` all access

Must not use:
- default descriptor
- `CurrentUserOnly`

If ACL creation fails, must exit hard.

## 13. Pipe protocol

Must support only:
- `get_status`
- `list_recent_messages`
- `get_message`
- `list_recent_meeting_requests`
- `list_calendar_window`
- `get_event`

Must use request envelope with:
- `id`
- `method`
- `params`

Must use response envelope with:
- `id`
- `ok`
- `result` or `error`

Must implement deterministic validation/errors.

## 14. CLI surface

Client CLI must implement exactly:
- `status`
- `list-messages --since <ISO8601> --limit <n>`
- `get-message --id <bridge_id>`
- `list-meeting-requests --since <ISO8601> --limit <n>`
- `list-calendar --start <ISO8601> --end <ISO8601> --limit <n>`
- `get-event --id <bridge_id>`

Must:
- print JSON only to stdout
- print human-readable diagnostics only to stderr

Exit codes must be:
- 0 success
- 2 pipe transport failure
- 3 unauthorized
- 4 Outlook unavailable
- 5 invalid arguments
- 6 bridge internal error

## 15. File and config locations

Install root:
- `C:\Program Files\OpenClaw\MailBridge\`

Primary-user config root:
- `%LOCALAPPDATA%\OpenClaw\MailBridge\`

Required files:
- `bridge.settings.json`
- `cache.db`
- `logs\bridge.log`

Required default config values:
- `pipeName = openclaw_mailbridge_v1`
- `mode = safe`
- `autostartOutlook = true`
- `inboxPollSeconds = 30`
- `calendarPollSeconds = 300`
- `inboxOverlapMinutes = 5`
- `calendarPastDays = 14`
- `calendarFutureDays = 60`
- `maxItemsPerScan = 500`
- `bodyPreviewMaxChars = 500`
- `logLevel = Information`

Must validate config strictly.

## 16. Scheduled task installation

Must register Task Scheduler task:
- name `OpenClaw MailBridge`
- `onlogon`
- `/ru <PRIMARY_USER>`
- `/it`

If primary user is logged on, must immediately run task.

Must not install as service.

## 17. OpenClaw integration pattern

Must not make OpenClaw talk pipes directly in v1.
Must support shell-out model via client CLI.

## 18. Preflight checks

Install script must fail fast unless it proves:
1. Outlook COM available and classic Outlook installed/configured
2. primary user can open Outlook manually and default profile is usable
3. primary user Inbox and Calendar resolve
4. bridge starts in safe mode and responds to status
5. `openclaw-svc` can connect to pipe through client CLI
6. no bridge log file contains message bodies

Optional enhanced preflight:
- warn only
- keep disabled unless explicitly enabled

## 19. Acceptance tests

`scripts/test-mailbridge.ps1` must check:

### A. Bridge lifecycle
- scheduled task starts
- poll status until ready or timeout
- verify mode / Outlook state / last scan timestamps

### B. Mail read path
- list messages last 24h
- verify schema
- verify safe-mode omissions
- get one message by ID

### C. Calendar read path
- list calendar window
- verify schema
- verify recurring appointments if present
- get one event by ID

### D. Privacy enforcement
- verify redaction for private/confidential items if present
- verify no body preview in safe mode
- verify sender/attendee fields absent in safe mode

### E. Pipe isolation
- verify `openclaw-svc` succeeds
- verify unapproved account denied or ACL inspection documented
- verify NETWORK denied

### F. COM hygiene
- run 100 repeated status/list calls
- verify no multiplying orphan Outlook processes
- verify no steadily growing handle leak

## 20. Failure behavior

Must implement bridge states:
- `starting`
- `waiting_for_outlook`
- `ready`
- `degraded`
- `error`

Must:
- return `waiting_for_outlook` if Outlook unavailable and autostart disabled
- return degraded on startup/logon fail with redacted HRESULT-level message
- retain prior cache when scan fails and serve stale marker:
  - `cache_stale=true`
  - `stale_reason`
- exit hard if pipe ACL creation fails

## 21. Forbidden behaviors

Solution must not:
- automate Outlook from `openclaw-svc`
- automate Outlook from SYSTEM
- create Windows service for bridge
- expose HTTP or WebSocket
- enable write methods
- browse arbitrary folders
- read attachments
- parse PST/OST directly
- use default named-pipe security
- use `PipeOptions.CurrentUserOnly`
- cache raw COM objects
- log message bodies
- enable enhanced by default

---

# REQUIRED OUTPUT FORMAT

Produce exactly these sections.

## 1. Executive verdict
Give:
- overall verdict: PASS / FAIL / PARTIAL
- short explanation
- count of PASS / FAIL / PARTIAL / NOT EVIDENCED items

## 2. Delivery inventory
List all delivered artifacts found and note missing required artifacts.

## 3. Framework/version compliance
Verify that the solution targets .NET 10 / `net10.0-windows` as required.
Call out every project target framework.

## 4. Requirement-by-requirement audit
Use a numbered checklist matching the sections above.
For each requirement:
- Status: PASS / FAIL / PARTIAL / NOT EVIDENCED
- Evidence: file paths, classes, methods, scripts, tests
- Gap: precise deficiency, if any
- Risk: why the gap matters

## 5. Forbidden capability audit
Explicitly inspect whether any forbidden behaviors exist.
For each forbidden item:
- Present: YES / NO / NOT EVIDENCED
- Evidence

## 6. Runtime-proof audit
Separate what is:
- code-proven
- test-proven
- runtime-proven
- environment-blocked

Do not blur these categories.

## 7. Acceptance-test audit
Inspect `scripts/test-mailbridge.ps1` and determine whether it actually proves each required acceptance test.
Call out missing assertions, weak assertions, or untestable assumptions.

## 8. Security audit
Specifically assess:
- named pipe ACL correctness
- local-only posture
- service-user isolation
- no direct Outlook access from service account
- safe mode default
- redaction correctness
- logging hygiene

## 9. Deviations from spec
List every deviation, even if arguably harmless.

## 10. Remediation list
Produce a precise punch list of what must change to reach full acceptance.
Order by severity:
- Critical
- High
- Medium
- Low

## 11. Final exit-criteria matrix
For each final exit criterion, provide:
- PASS / FAIL / PARTIAL / NOT EVIDENCED
- evidence
- blocking issue if not passed

---

# REVIEW STYLE

- Be exact.
- Be skeptical.
- Be implementation-centric.
- Cite concrete file paths and symbols.
- Distinguish code existence from behavioral proof.
- Do not praise.
- Do not soften findings.
- Do not invent evidence.

Proceed with the acceptance audit now.

Claude: I'll begin the acceptance audit by systematically inspecting the repository structure and all implementation artifacts. Let me start with parallel exploration of the key areas.

Ran terminal command: ls -la /c/Users/DanMoisan/repos/open-claw-bridge/

Searched for files matching `**/*.csproj`

Searched for files matching `**/*.sln`

Searched for files matching `scripts/*.ps1`

Searched for files matching `docs/*.md`

Good, the basic structure exists. Now let me read all csproj files and key source files in parallel.

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/global.json)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/OpenClaw.MailBridge.sln)

Now let me explore all source files in each project directory.

Searched for files matching `src/OpenClaw.MailBridge/**/*.cs`

Searched for files matching `src/OpenClaw.MailBridge.Client/**/*.cs`

Searched for files matching `src/OpenClaw.MailBridge.Contracts/**/*.cs`

Searched for files matching `tests/OpenClaw.MailBridge.Tests/**/*.cs`

Now let me read all the core source files in parallel.

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/Program.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/BridgeApplication.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/BridgeStateStore.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/CacheRepository.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/ComActiveObject.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/OutlookComHelpers.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/OutlookScanner.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/OutlookStaExecutor.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/PipeRpcWorker.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/ScanWorker.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge/ResponseShaper.cs)

Now let me read the contracts and remaining source files, plus check for classes referenced but not yet found.

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/src/OpenClaw.MailBridge.Client/Program.cs)

Searched for regex `BridgeIdCodec`

Searched for regex `BodySanitizer`

Now let me read all the scripts and documentation in parallel.

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/scripts/install-mailbridge.ps1)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/scripts/uninstall-mailbridge.ps1)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/scripts/register-mailbridge-task.ps1)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/scripts/test-mailbridge.ps1)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/docs/mailbridge-runbook.md)

Now let me read all the test files.

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Pipe.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Calendar.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Phase5.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/MailBridgeClientTests.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/BridgeContractsCoverageTests.cs)

Read [](file:///c%3A/Users/DanMoisan/repos/open-claw-bridge/tests/OpenClaw.MailBridge.Tests/ResponseShaperTests.cs)

Now let me verify a few specific items I need to confirm.

Searched for regex `GetAssociatedAppointment`

Searched for regex `AddWindowsService|WindowsService`

Searched for regex `schema_version`

Searched for regex `to_json|cc_json|RequiredAttendees|OptionalAttendees|Resources`

Searched for regex `Private message|Private appointment|Sensitivity.*[23]`

Searched for regex `Microsoft\.Office\.Interop|COMReference|Interop\.Outlook`

Searched for regex `Interop|PIA|Object Library`

Searched for regex `FileLog|AddFile|Serilog|NLog|bridge\.log`

Searched for regex `FullControl`

Searched for regex `Environment\.Exit|FailFast`

Searched for regex `BridgeState\.error`

Let me verify the build status and run the tests.

Ran terminal command: dotnet build --verbosity quiet 2>&1 | tail -5

Ran terminal command: dotnet test --verbosity quiet 2>&1 | tail -10

Good - builds clean, 86 tests pass, 1 skipped. Now I have all the evidence I need to produce the full audit report.

---

# ACCEPTANCE AUDIT REPORT: OpenClaw MailBridge

---

## 1. Executive Verdict

**Overall Verdict: FAIL**

The delivered solution provides a structurally sound foundation with correct project layout, framework targeting, pipe transport mechanics, STA threading, and CLI surface. However, it fails on multiple critical specification requirements. Sensitivity-based redaction is entirely absent. Safe-mode field suppression is incomplete. The calendar overlap filter deviates from spec. Meeting-request normalization via `GetAssociatedAppointment(false)` is not implemented. Several enhanced-mode fields are never populated from Outlook. No file logging exists. The pipe ACL does not grant SYSTEM/Administrators FullControl as required. `AddWindowsService()` is present despite the spec prohibiting Windows service creation.

| Status | Count |
|---|---|
| PASS | 42 |
| FAIL | 27 |
| PARTIAL | 8 |
| NOT EVIDENCED | 3 |

---

## 2. Delivery Inventory

### Required artifacts

| Artifact | Status |
|---|---|
| `src/OpenClaw.MailBridge/` | Present |
| `src/OpenClaw.MailBridge.Client/` | Present |
| `src/OpenClaw.MailBridge.Contracts/` | Present |
| `tests/OpenClaw.MailBridge.Tests/` | Present |
| `scripts/install-mailbridge.ps1` | Present |
| `scripts/uninstall-mailbridge.ps1` | Present |
| `scripts/register-mailbridge-task.ps1` | Present |
| `scripts/test-mailbridge.ps1` | Present |
| `docs/mailbridge-runbook.md` | Present |

### Additional artifacts (not in spec)

| Artifact | Note |
|---|---|
| `scripts/Build.ps1` | Extra helper |
| `scripts/Test.ps1` | Extra helper |
| `scripts/Run-Client.ps1` | Extra helper |
| `scripts/Run-Bridge.ps1` | Extra helper |
| `docs/setup.md` | Extra documentation |
| `docs/api-reference.md` | Extra documentation |
| `docs/architecture-diagrams.md` | Extra documentation |
| `AGENTS.md` (87KB) | Devex configuration |
| `mailbridge.runsettings` | Test settings |

**Verdict**: All required artifacts present. Extra artifacts are not forbidden.

---

## 3. Framework / Version Compliance

| Project | Target Framework | Status |
|---|---|---|
| `OpenClaw.MailBridge` | `net10.0-windows` | PASS |
| `OpenClaw.MailBridge.Client` | `net10.0-windows` | PASS |
| `OpenClaw.MailBridge.Contracts` | `net10.0-windows` | PASS |
| `OpenClaw.MailBridge.Tests` | `net10.0-windows` | PASS |
| `global.json` | SDK 10.0.201 | PASS |

**Build result**: Compiles clean, 0 warnings, 0 errors. 86 tests pass, 1 skipped (non-Windows behavior test).

**Note**: [OpenClaw.MailBridge.csproj:22](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L22) references `Microsoft.Data.Sqlite` at version `8.0.11`, not a .NET 10 version. Functional but incongruent.

**Note**: [OpenClaw.MailBridge.csproj:28](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L28) references `System.IO.Pipes.AccessControl` at version `6.0.0-preview.5.21301.5` — a preview package.

---

## 4. Requirement-by-Requirement Audit

### 4.1 Mission and Hard Constraints

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Local-only bridge | PASS | No HTTP/WebSocket/REST/IMAP/Graph code found | — |
| Read-only bridge | PASS | No write/send/reply/create Outlook methods | — |
| Bridge runs in user session | PASS | [register-mailbridge-task.ps1:90](scripts/register-mailbridge-task.ps1#L90): `/sc onlogon /ru $PrimaryUser /it` | — |
| Client runs under openclaw-svc | PASS | Client is standalone CLI, no session dependency | — |
| No send/reply/accept/decline/create | PASS | Grep for Outlook write verbs: zero hits | — |
| Classic Outlook target | PASS | `Type.GetTypeFromProgID("Outlook.Application")` at [ComActiveObject.cs:67](src/OpenClaw.MailBridge/ComActiveObject.cs#L67) | — |
| Must not automate Outlook from service/SYSTEM | PASS | Bridge runs as interactive task, not service | — |

### 4.2 Scope Boundary

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Exact required artifact set | PASS | All 9 artifacts present per Section 2 | — |
| No Graph/EWS/REST/IMAP/MAPI | PASS | No evidence of forbidden transport code | — |
| No PST/OST parsing | PASS | No file-based mailstore code | — |
| No HTTP loopback APIs | PASS | No HTTP listener code | — |
| No browser auth | PASS | No OAuth/browser code | — |
| No write-capable Outlook methods | PASS | No write methods found | — |

### 4.3 Technology Choices

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| C# | PASS | All source is C# | — |
| .NET 10 / net10.0-windows | PASS | All csproj files target `net10.0-windows` | — |
| System.IO.Pipes | PASS | Used in [PipeRpcWorker.cs](src/OpenClaw.MailBridge/PipeRpcWorker.cs) and [Client/Program.cs](src/OpenClaw.MailBridge.Client/Program.cs) | — |
| System.Text.Json | PASS | Used throughout | — |
| Microsoft.Data.Sqlite | PASS | Used in [CacheRepository.cs](src/OpenClaw.MailBridge/CacheRepository.cs) | — |
| Outlook COM via installed PIA/Object Library | **FAIL** | Implementation uses late-bound reflection (`GetType().InvokeMember`) at [OutlookComHelpers.cs](src/OpenClaw.MailBridge/OutlookComHelpers.cs). No PIA reference in any csproj. [docs/setup.md](docs/setup.md) documents PIA setup instructions but the project does not reference it. | Spec requires "Outlook COM interop through installed Outlook PIA / Object Library." Late-binding is a valid COM approach but does not satisfy this requirement. |
| Must not rely on PipeOptions.CurrentUserOnly | PASS | Not used. Explicit `PipeSecurity` at [PipeRpcWorker.cs:74](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L74) | — |

### 4.4 Threading Model

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Dedicated STA thread | PASS | [OutlookStaExecutor.cs:27-29](src/OpenClaw.MailBridge/OutlookStaExecutor.cs#L27-L29): `new Thread(Run)`, `SetApartmentState(ApartmentState.STA)`, `Start()` | — |
| Request queue/marshaling | PASS | `BlockingCollection` at [OutlookStaExecutor.cs:16](src/OpenClaw.MailBridge/OutlookStaExecutor.cs#L16) | — |
| No Outlook COM from thread pool | PASS | [ScanWorker.cs:28-30](src/OpenClaw.MailBridge/ScanWorker.cs#L28-L30): scan work is dispatched via `sta.InvokeAsync()` | — |

### 4.5 Outlook Session Acquisition

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Check if Outlook running | PASS | [OutlookScanner.cs:75](src/OpenClaw.MailBridge/OutlookScanner.cs#L75): `_com.TryGet("Outlook.Application")` | — |
| If running, use GetActiveObject | PASS | [ComActiveObject.cs:41-44](src/OpenClaw.MailBridge/ComActiveObject.cs#L41-L44): P/Invoke `GetActiveObject` (Marshal.GetActiveObject unavailable in .NET 5+) | — |
| If not running, create + MAPI logon | PASS | [ComActiveObject.cs:66-89](src/OpenClaw.MailBridge/ComActiveObject.cs#L66-L89) | — |
| Expose autostartOutlook config | PASS | [BridgeContracts.cs:115](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L115): `AutostartOutlook` property | — |
| false => waiting_for_outlook | PASS | [OutlookScanner.cs:91-92](src/OpenClaw.MailBridge/OutlookScanner.cs#L91-L92) | — |
| true => launch allowed | PASS | [OutlookScanner.cs:98](src/OpenClaw.MailBridge/OutlookScanner.cs#L98): `_com.CreateAndLogonOutlook()` | — |

### 4.6 Outlook Access Rules

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Default Inbox (folder type 6) | PASS | [OutlookScanner.cs:132](src/OpenClaw.MailBridge/OutlookScanner.cs#L132): `ResolveDefaultFolder(outlookNamespace, 6, ...)` | — |
| Default Calendar (folder type 9) | PASS | [OutlookScanner.cs:144](src/OpenClaw.MailBridge/OutlookScanner.cs#L144): `ResolveDefaultFolder(outlookNamespace, 9, ...)` | — |
| folder.Items | PASS | [OutlookScanner.cs:203](src/OpenClaw.MailBridge/OutlookScanner.cs#L203): `GetMemberValue(inboxFolder, "Items")` | — |
| Items.Restrict on ReceivedTime | PASS | [OutlookScanner.cs:210](src/OpenClaw.MailBridge/OutlookScanner.cs#L210) + [OutlookScanner.cs:335](src/OpenClaw.MailBridge/OutlookScanner.cs#L335): `[ReceivedTime] >= ...` | — |
| 30-second poll cadence (default) | PASS | [BridgeContracts.cs:129](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L129): `InboxPollSeconds = 30` | — |
| 5-minute overlap window (default) | PASS | [BridgeContracts.cs:129](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L129): `InboxOverlapMinutes = 5` | — |
| Dedupe by EntryID | PASS | `bridge_id` is PRIMARY KEY with upsert ON CONFLICT | — |
| No Restrict on Body/EntryID/HTMLBody | PASS | Filter only uses `[ReceivedTime]` and `[Start]`/`[End]` | — |
| Calendar: items.Sort("[Start]") | PASS | [OutlookScanner.cs:259](src/OpenClaw.MailBridge/OutlookScanner.cs#L259) | — |
| Calendar: IncludeRecurrences = true | PASS | [OutlookScanner.cs:260](src/OpenClaw.MailBridge/OutlookScanner.cs#L260) | — |
| Calendar overlap filter `[Start] <= end AND [End] >= start` | **FAIL** | [OutlookScanner.cs:338-339](src/OpenClaw.MailBridge/OutlookScanner.cs#L338-L339): uses `[Start] >= start AND [Start] < end` | Wrong filter shape. Misses events that start before the window but extend into it. |
| Must not use .Count on recurring collection | PASS | Uses `EnumerateItems` with hard cap, no `.Count` | — |
| Enumerate with hard cap | PASS | [OutlookScanner.cs:341-363](src/OpenClaw.MailBridge/OutlookScanner.cs#L341-L363): `maxItems` parameter | — |
| Normalize MeetingItem as meeting_message | **FAIL** | [OutlookScanner.cs:387](src/OpenClaw.MailBridge/OutlookScanner.cs#L387): `isMeeting ? "meeting" : "mail"` | Uses `"meeting"` not `"meeting_message"` |
| Call GetAssociatedAppointment(false) | **FAIL** | Grep: zero hits for `GetAssociatedAppointment` in entire repo | Not implemented |
| Never pass true to GetAssociatedAppointment | NOT EVIDENCED | Method never called at all | — |

### 4.7 COM Lifetime Rules

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Fresh COM refs every scan | PASS | [OutlookScanner.cs:186-188](src/OpenClaw.MailBridge/OutlookScanner.cs#L186-L188): finally block releases all + `_outlookApp = null` | — |
| No inline chained COM calls | PASS | All COM objects assigned to local variables | — |
| Local variables per COM object | PARTIAL | Namespace/folders/items use locals. But individual mail/appointment items from `EnumerateItems` are never released. | Items yielded by `foreach` in [OutlookScanner.cs:218-229](src/OpenClaw.MailBridge/OutlookScanner.cs#L218-L229) are COM objects that are never passed to `_com.Release()`. |
| Reverse-order release | PASS | [ComActiveObject.cs:118-124](src/OpenClaw.MailBridge/ComActiveObject.cs#L118-L124): iterates length-1 to 0 | — |
| Marshal.FinalReleaseComObject | PASS | [ComActiveObject.cs:106](src/OpenClaw.MailBridge/ComActiveObject.cs#L106) | — |
| Null references after release | PASS | [OutlookScanner.cs:188](src/OpenClaw.MailBridge/OutlookScanner.cs#L188): `_outlookApp = null` | — |

### 4.8 Data Model

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| SQLite path: %LOCALAPPDATA%\OpenClaw\MailBridge\cache.db | PASS | [CacheRepository.cs:53-57](src/OpenClaw.MailBridge/CacheRepository.cs#L53-L57) | — |
| messages table — all columns match | PASS | [CacheRepository.cs:92](src/OpenClaw.MailBridge/CacheRepository.cs#L92): 20 columns, all match spec | — |
| events table — all columns match | PASS | [CacheRepository.cs:93](src/OpenClaw.MailBridge/CacheRepository.cs#L93): 22 columns, all match spec | — |
| scan_state table exists | PASS | [CacheRepository.cs:94](src/OpenClaw.MailBridge/CacheRepository.cs#L94) | — |
| scan_state: last_inbox_scan_utc | PASS | Written at [OutlookScanner.cs:161](src/OpenClaw.MailBridge/OutlookScanner.cs#L161) | — |
| scan_state: last_calendar_scan_utc | PASS | Written at [OutlookScanner.cs:167](src/OpenClaw.MailBridge/OutlookScanner.cs#L167) | — |
| scan_state: schema_version | **FAIL** | Grep: zero hits for `schema_version` in repo | Never written or checked |
| last_modified_utc populated for events | **FAIL** | [CacheRepository.cs:421](src/OpenClaw.MailBridge/CacheRepository.cs#L421): hardcoded `DBNull.Value` | Never read from Outlook |

### 4.9 ID Strategy

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Mail: `msg:<base64url(entry_id)>` | PASS | [Helpers.cs:8-9](src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs#L8-L9) | — |
| Meeting-message: `mtg:<base64url(entry_id)>` | PASS | Same method with `isMeeting=true` | — |
| Calendar: `evt:<base64url(global_appointment_id)>:<start_utc>` | PASS | [Helpers.cs:11-16](src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs#L11-L16) | — |
| Fallback `evt:<base64url(entry_id)>:<start_utc>` | PASS | Uses `entryId` when `globalAppointmentId` is null/empty | — |
| Prefer GlobalAppointmentID | PASS | Ternary checks non-blank first | — |

### 4.10 Redaction and Privacy Policy

| Requirement | Status | Evidence | Gap | Risk |
|---|---|---|---|---|
| Use Outlook Sensitivity to trigger redaction | **FAIL** | `Sensitivity` is READ from Outlook items but never CHECKED against private/confidential values (2/3). Grep for "Private message" and "Private appointment": zero hits. | No sensitivity-based redaction exists. | Private/confidential items are exposed with full content. Critical privacy violation. |
| Mail redaction: subject → "Private message" | **FAIL** | Not implemented | — | — |
| Mail redaction: sender/recipient/body null | **FAIL** | Not implemented | — | — |
| Calendar redaction: subject → "Private appointment" | **FAIL** | Not implemented | — | — |
| Calendar redaction: location/organizer/attendees/body null | **FAIL** | Not implemented | — | — |
| Calendar redaction: preserve start/end/busy/recurring | NOT EVIDENCED | Redaction not implemented, so preservation is moot | — | — |
| Strip HTML from non-private preview | PASS | [Helpers.cs:107](src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs#L107): `Tags.Replace(input, " ")` | — | — |
| Truncate to 500 chars (default) | PASS | [Helpers.cs:117](src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs#L117) | — | — |
| Never expose attachment contents | PASS | No attachment content reading | — | — |
| Never expose filesystem paths in bodies | PASS | [Helpers.cs:108](src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs#L108): `FilePath.Replace(noHtml, "[path]")` | — | — |
| Never log message content or body preview | PASS | All logger calls log only counts and error messages, never content | — | — |

### 4.11 Safe vs Enhanced Field Sets

| Requirement | Status | Evidence | Gap | Risk |
|---|---|---|---|---|
| Default mode = safe | PASS | [BridgeContracts.cs:129](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L129): `"safe"` | — | — |
| Safe mail: suppress sender_name, sender_email | PASS | [ResponseShaper.cs:27-28](src/OpenClaw.MailBridge/ResponseShaper.cs#L27-L28) | — | — |
| Safe mail: suppress body_preview | PASS | [ResponseShaper.cs:26](src/OpenClaw.MailBridge/ResponseShaper.cs#L26) | — | — |
| Safe mail: suppress to_json, cc_json | **FAIL** | [ResponseShaper.cs:25-30](src/OpenClaw.MailBridge/ResponseShaper.cs#L25-L30): `ToJson` and `CcJson` are NOT nulled in safe mode | Leaked to caller even in safe mode |
| Safe mail: protected_fields_available=false | **FAIL** | Not set. `ProtectedFieldsAvailable` retains scanner's value. | Spec explicitly requires this to be false in safe mode |
| Safe calendar: suppress organizer | **FAIL** | [ResponseShaper.cs:49-53](src/OpenClaw.MailBridge/ResponseShaper.cs#L49-L53): `Organizer` not nulled | Leaked in safe mode |
| Safe calendar: suppress attendee/resource fields | **FAIL** | `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson` not nulled | Leaked in safe mode |
| Safe calendar: suppress body_preview | PASS | [ResponseShaper.cs:51](src/OpenClaw.MailBridge/ResponseShaper.cs#L51) | — | — |
| Safe calendar: protected_fields_available=false | **FAIL** | Not set | — |
| Enhanced mode not default | PASS | Default mode is `"safe"` | — | — |
| Enhanced mail: to_json, cc_json populated | **FAIL** | [OutlookScanner.cs:399-400](src/OpenClaw.MailBridge/OutlookScanner.cs#L399-L400): both hardcoded to `null` in scanner. Never read from Outlook. | Enhanced mode returns null for these fields |
| Enhanced calendar: attendee/resource fields populated | **FAIL** | [OutlookScanner.cs:439-441](src/OpenClaw.MailBridge/OutlookScanner.cs#L439-L441): all three hardcoded to `null` | Enhanced mode returns null for these fields |

### 4.12 Named-Pipe Design

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Pipe name: `openclaw_mailbridge_v1` | PASS | [BridgeContracts.cs:129](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L129) | — |
| UTF-8 JSON | PASS | `Encoding.UTF8.GetString` / `SerializeToUtf8Bytes` | — |
| Message mode | PASS | [PipeRpcWorker.cs:65](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L65): `PipeTransmissionMode.Message` | — |
| Max request 64KB | PASS | [PipeRpcWorker.cs:126](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L126): `ms.Length > 65536` | — |
| Max response 1MB | PASS | [PipeRpcWorker.cs:232](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L232): `payload.Length > 1024 * 1024` | — |
| Max concurrent instances 4 | PASS | [PipeRpcWorker.cs:63](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L63): parameter `4` | — |
| SYSTEM full control | **FAIL** | [PipeRpcWorker.cs:84](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L84): `AddAllowRule` grants `ReadWrite \| CreateNewInstance` via [PipeRpcWorker.cs:349-355](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L349-L355) | Spec requires FullControl for SYSTEM |
| Administrators full control | **FAIL** | Same `AddAllowRule` method — grants ReadWrite+CreateNewInstance, not FullControl | Spec requires FullControl for Administrators |
| Primary user SID read/write | PASS | [PipeRpcWorker.cs:95](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L95): `AddAllowRule(security, currentUser)` | — |
| openclaw-svc SID read/write | PASS | [PipeRpcWorker.cs:96](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L96): `AccountSidResolver("openclaw-svc")` | — |
| Deny NETWORK all access | PASS | [PipeRpcWorker.cs:98-104](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L98-L104): `PipeAccessRights.FullControl`, `AccessControlType.Deny` | — |
| No default descriptor | PASS | Explicit `PipeSecurity` built | — |
| No CurrentUserOnly | PASS | Not used | — |
| Hard exit if ACL fails | PARTIAL | Exception propagates through BackgroundService but no explicit `Environment.Exit()` / `FailFast()` | Depends on host behavior; not a guaranteed hard exit |

### 4.13 Pipe Protocol

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| get_status | PASS | [PipeRpcWorker.cs:207](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L207) | — |
| list_recent_messages | PASS | [PipeRpcWorker.cs:208](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L208) | — |
| get_message | PASS | [PipeRpcWorker.cs:211](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L211) | — |
| list_recent_meeting_requests | PASS | [PipeRpcWorker.cs:209](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L209) | — |
| list_calendar_window | PASS | [PipeRpcWorker.cs:212](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L212) | — |
| get_event | PASS | [PipeRpcWorker.cs:213](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L213) | — |
| Request envelope: id, method, params | PASS | [BridgeContracts.cs:40-44](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L40-L44) | — |
| Response envelope: id, ok, result/error | PASS | [BridgeContracts.cs:51-56](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L51-L56) | — |
| Deterministic validation/errors | PASS | `RequireParameter`, `RequireIso8601`, `RequireLimit` with `InvalidRequestException` | — |

### 4.14 CLI Surface

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| status | PASS | [Client/Program.cs:132](src/OpenClaw.MailBridge.Client/Program.cs#L132) | — |
| list-messages --since --limit | PASS | [Client/Program.cs:133](src/OpenClaw.MailBridge.Client/Program.cs#L133) | — |
| get-message --id | PASS | [Client/Program.cs:134](src/OpenClaw.MailBridge.Client/Program.cs#L134) | — |
| list-meeting-requests --since --limit | PASS | [Client/Program.cs:135](src/OpenClaw.MailBridge.Client/Program.cs#L135) | — |
| list-calendar --start --end --limit | PASS | [Client/Program.cs:142](src/OpenClaw.MailBridge.Client/Program.cs#L142) | — |
| get-event --id | PASS | [Client/Program.cs:150](src/OpenClaw.MailBridge.Client/Program.cs#L150) | — |
| JSON to stdout only | PASS | [Client/Program.cs:32](src/OpenClaw.MailBridge.Client/Program.cs#L32): writes to `stdout` parameter | — |
| Diagnostics to stderr only | PASS | Parse errors go to `Console.Error`; exceptions to `stderr` | — |
| Exit code 0 (success) | PASS | [Client/Program.cs:34](src/OpenClaw.MailBridge.Client/Program.cs#L34) | — |
| Exit code 2 (pipe transport) | PASS | [Client/Program.cs:49-58](src/OpenClaw.MailBridge.Client/Program.cs#L49-L58): IOException/TimeoutException → 2 | — |
| Exit code 3 (unauthorized) | PASS | [Client/Program.cs:37](src/OpenClaw.MailBridge.Client/Program.cs#L37) + [Client/Program.cs:44-46](src/OpenClaw.MailBridge.Client/Program.cs#L44-L46) | — |
| Exit code 4 (Outlook unavailable) | PASS | [Client/Program.cs:38](src/OpenClaw.MailBridge.Client/Program.cs#L38) | — |
| Exit code 5 (invalid arguments) | PASS | [Client/Program.cs:29](src/OpenClaw.MailBridge.Client/Program.cs#L29) + [Client/Program.cs:39](src/OpenClaw.MailBridge.Client/Program.cs#L39) | — |
| Exit code 6 (bridge internal error) | PASS | [Client/Program.cs:41](src/OpenClaw.MailBridge.Client/Program.cs#L41): default case | — |

### 4.15 File and Config Locations

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Install root: C:\Program Files\OpenClaw\MailBridge\ | PASS | [install-mailbridge.ps1:6](scripts/install-mailbridge.ps1#L6) | — |
| Config root: %LOCALAPPDATA%\OpenClaw\MailBridge\ | PASS | [install-mailbridge.ps1:170](scripts/install-mailbridge.ps1#L170) | — |
| bridge.settings.json | PASS | [install-mailbridge.ps1:181-183](scripts/install-mailbridge.ps1#L181-L183) | — |
| cache.db | PASS | [CacheRepository.cs:53-57](src/OpenClaw.MailBridge/CacheRepository.cs#L53-L57) | — |
| logs\bridge.log | **FAIL** | [BridgeApplication.cs:56-57](src/OpenClaw.MailBridge/BridgeApplication.cs#L56-L57): `ClearProviders()` + `AddSimpleConsole()`. No file logging provider. No `Serilog`, `NLog`, or `AddFile`. | Bridge writes to console only; no `bridge.log` file is produced |
| Default config values (all 11) | PASS | [BridgeContracts.cs:128-129](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L128-L129): all 11 defaults match spec exactly | — |
| Strict config validation | PARTIAL | [Helpers.cs:121-143](src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs#L121-L143): validates mode, poll seconds, maxItems, bodyPreviewMaxChars, pipeName. Does NOT validate InboxOverlapMinutes, CalendarPastDays, CalendarFutureDays, LogLevel. | 4 config fields unvalidated |

### 4.16 Scheduled Task Installation

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Task name "OpenClaw MailBridge" | PASS | [register-mailbridge-task.ps1:7](scripts/register-mailbridge-task.ps1#L7) | — |
| onlogon trigger | PASS | [register-mailbridge-task.ps1:90](scripts/register-mailbridge-task.ps1#L90): `/sc onlogon` | — |
| /ru PRIMARY_USER | PASS | Same line: `/ru $PrimaryUser` | — |
| /it (interactive only) | PASS | Same line: `/it` | — |
| Immediately run if user logged on | PASS | [register-mailbridge-task.ps1:92-96](scripts/register-mailbridge-task.ps1#L92-L96): `Test-PrimaryUserLoggedOn` → `schtasks /run` | — |
| Must not install as service | **FAIL** | [BridgeApplication.cs:52-55](src/OpenClaw.MailBridge/BridgeApplication.cs#L52-L55): `AddWindowsService(options => options.ServiceName = "OpenClaw.MailBridge")`. While no service is registered, the host is explicitly configured to support running as one. [OpenClaw.MailBridge.csproj:26](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L26) also packages `Microsoft.Extensions.Hosting.WindowsServices`. | Spec says "Must not create Windows service for bridge." AddWindowsService() configures service capability. |

### 4.17 OpenClaw Integration Pattern

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Shell-out model via client CLI | PASS | Client is standalone exe, no library API | — |
| Must not make OpenClaw talk pipes directly | PASS | No pipe client library exposed | — |

### 4.18 Preflight Checks

| # | Requirement | Status | Evidence | Gap |
|---|---|---|---|---|
| 1 | Outlook COM available & classic installed | PASS | [install-mailbridge.ps1:77-80](scripts/install-mailbridge.ps1#L77-L80) | — |
| 2 | Primary user can open Outlook, default profile | PASS | [install-mailbridge.ps1:85-89](scripts/install-mailbridge.ps1#L85-L89) | — |
| 3 | Inbox and Calendar resolve | PASS | [install-mailbridge.ps1:88-89](scripts/install-mailbridge.ps1#L88-L89): `GetDefaultFolder(6)`, `GetDefaultFolder(9)` | — |
| 4 | Bridge starts in safe mode, responds to status | PASS | [install-mailbridge.ps1:202](scripts/install-mailbridge.ps1#L202): `Wait-BridgeStatusPreflight` | — |
| 5 | openclaw-svc can connect via client CLI | **FAIL** | No test from openclaw-svc account context. `Wait-BridgeStatusPreflight` runs as current user. | Missing preflight |
| 6 | No log file contains message bodies | PASS | [install-mailbridge.ps1:206-209](scripts/install-mailbridge.ps1#L206-L209): regex check | — |
| 7 | .NET 10 runtime validation | PASS | [install-mailbridge.ps1:193-194](scripts/install-mailbridge.ps1#L193-L194): `Assert-DotNet10RuntimeConfig` | — |

### 4.19 Acceptance Tests — see Section 7 below

### 4.20 Failure Behavior

| Requirement | Status | Evidence | Gap |
|---|---|---|---|
| Bridge states: starting, waiting_for_outlook, ready, degraded, error | PARTIAL | [BridgeContracts.cs:6-12](src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs#L6-L12): all five defined. But `error` state is NEVER SET in any code path. | `error` state is dead code |
| waiting_for_outlook when Outlook unavailable + autostart disabled | PASS | [OutlookScanner.cs:91-92](src/OpenClaw.MailBridge/OutlookScanner.cs#L91-L92) | — |
| degraded on startup/logon fail with redacted HRESULT | PARTIAL | [OutlookScanner.cs:182](src/OpenClaw.MailBridge/OutlookScanner.cs#L182): sets degraded with `"scan_failure"`. Logs `ex.Message` not HRESULT. | Not HRESULT-level specifically |
| Retain cache when scan fails + cache_stale marker | PASS | [BridgeStateStore.cs:82-85](src/OpenClaw.MailBridge/BridgeStateStore.cs#L82-L85): `CacheStale=true`, `StaleReason` set, state→degraded. Cache DB not cleared. | — |
| Exit hard if pipe ACL creation fails | PARTIAL | Exception propagates through BackgroundService framework. No explicit hard exit call. | Relies on host infrastructure, not guaranteed |

### 4.21 Forbidden Behaviors — see Section 5

---

## 5. Forbidden Capability Audit

| Forbidden Behavior | Present? | Evidence |
|---|---|---|
| Automate Outlook from openclaw-svc | NO | Bridge runs as primary user task only |
| Automate Outlook from SYSTEM | NO | No SYSTEM-context code paths |
| Create Windows service for bridge | **YES** | [BridgeApplication.cs:52-55](src/OpenClaw.MailBridge/BridgeApplication.cs#L52-L55): `AddWindowsService(...)` and [OpenClaw.MailBridge.csproj:26](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L26): `Microsoft.Extensions.Hosting.WindowsServices` package. No actual service registration occurs via scripts, but the host is configured for it. |
| Expose HTTP or WebSocket | NO | No HTTP/WebSocket listener code |
| Enable write methods | NO | No write/send Outlook COM methods |
| Browse arbitrary folders | NO | Only `GetDefaultFolder(6)` and `GetDefaultFolder(9)` |
| Read attachments | NO | No attachment content code |
| Parse PST/OST directly | NO | No file-based mailstore code |
| Use default named-pipe security | NO | Explicit `PipeSecurity` constructed |
| Use PipeOptions.CurrentUserOnly | NO | Not referenced |
| Cache raw COM objects | BORDERLINE | `_outlookApp` is a field that holds a COM ref within a scan cycle, but is released in finally block and nulled. Not cached across scans. |
| Log message bodies | NO | Logger calls log only counts and error messages |
| Enable enhanced by default | NO | Default mode is `"safe"` |

---

## 6. Runtime-Proof Audit

### Code-proven (verified by source inspection)
- Framework targeting net10.0-windows
- STA thread creation with SetApartmentState before Start
- BlockingCollection queue for COM marshaling
- PipeSecurity construction with explicit ACE entries
- SQLite schema creation with exact column definitions
- BridgeIdCodec prefix/encoding logic
- BodySanitizer HTML stripping and path redaction
- Client CLI argument parsing and exit code mapping
- Config defaults and validation logic
- Pipe message mode and size limits

### Test-proven (86 pass, 1 skip)
- BridgeIdCodec prefix fidelity
- BridgeSettingsValidator rejects invalid mode
- BodySanitizer HTML/path removal
- Safe-mode message shaping (suppresses body_preview, sender_name, sender_email)
- Enhanced-mode preview sanitization and truncation
- STA executor runs work and propagates exceptions
- OutlookScanner waiting_for_outlook state
- OutlookScanner ready state with repo touches
- OutlookScanner degraded on scan failure
- ScanWorker initialization and scanner invocation
- CacheRepository scan state CRUD
- CacheRepository message/event upsert and query
- PipeRpcWorker method dispatch, payload limit, error codes
- Client CLI pipe-name resolution, exit code mapping, pipe communication
- Response oversize downgrade

### Environment-blocked (cannot verify in this environment)
- Actual Outlook COM interop with real Outlook instance
- Named-pipe ACL enforcement with real SID resolution for openclaw-svc
- Task Scheduler registration and on-logon execution
- Bridge process lifecycle in interactive session
- End-to-end acceptance script execution
- NETWORK SID deny enforcement at transport level
- COM handle/process leak detection under sustained load

### NOT EVIDENCED
- Sensitivity-based redaction (no code exists)
- GetAssociatedAppointment(false) for meeting items (no code exists)
- schema_version in scan_state (never written)

---

## 7. Acceptance-Test Audit

Inspecting [scripts/test-mailbridge.ps1](scripts/test-mailbridge.ps1):

### Suite A — Bridge Lifecycle

| Check | Status | Evidence |
|---|---|---|
| Scheduled task starts | PASS | [test-mailbridge.ps1:70](scripts/test-mailbridge.ps1#L70): `schtasks /run /tn $ScheduledTaskName` |
| Poll status until ready or timeout | PASS | [test-mailbridge.ps1:72-79](scripts/test-mailbridge.ps1#L72-L79) |
| Verify mode | PASS | [test-mailbridge.ps1:186-188](scripts/test-mailbridge.ps1#L186-L188) |
| Verify Outlook state / last scan timestamps | PARTIAL | Checks for `mode` presence but does not explicitly assert `outlookConnected`, `lastInboxScanUtc`, or `lastCalendarScanUtc` |

### Suite B — Mail Read Path

| Check | Status | Evidence |
|---|---|---|
| List messages last 24h | PASS | [test-mailbridge.ps1:191](scripts/test-mailbridge.ps1#L191) |
| Verify schema | **FAIL** | No explicit field-by-field schema validation of returned message objects |
| Verify safe-mode omissions | Deferred to Suite D | — |
| Get one message by ID | PASS | [test-mailbridge.ps1:202-207](scripts/test-mailbridge.ps1#L202-L207) |

### Suite C — Calendar Read Path

| Check | Status | Evidence |
|---|---|---|
| List calendar window | PASS | [test-mailbridge.ps1:216](scripts/test-mailbridge.ps1#L216) |
| Verify schema | **FAIL** | No field-by-field schema validation |
| Verify recurring appointments | **FAIL** | No explicit recurrence check |
| Get one event by ID | PASS | [test-mailbridge.ps1:228-232](scripts/test-mailbridge.ps1#L228-L232) |

### Suite D — Privacy Enforcement

| Check | Status | Evidence | Gap |
|---|---|---|---|
| Verify redaction for private/confidential items | **FAIL** | Not tested. `Assert-SafeModePrivacy` only checks body_preview, sender_name, sender_email. | No sensitivity-based redaction test |
| Verify no body_preview in safe mode | PASS | [test-mailbridge.ps1:90-91](scripts/test-mailbridge.ps1#L90-L91) |
| Verify sender fields absent in safe mode | PASS | [test-mailbridge.ps1:93-99](scripts/test-mailbridge.ps1#L93-L99) |
| Verify to_json, cc_json absent in safe mode | **FAIL** | Not checked by `Assert-SafeModePrivacy` |
| Verify organizer/attendees absent in safe mode (calendar) | **FAIL** | Not checked |

### Suite E — Pipe Isolation

| Check | Status | Evidence | Gap |
|---|---|---|---|
| openclaw-svc succeeds | **FAIL** | [test-mailbridge.ps1:10](scripts/test-mailbridge.ps1#L10): `$OpenClawSvcPipeConnect` is a PARAMETER defaulting to `$false`. Test defers to operator evidence. | Not automated |
| Unapproved account denied | **FAIL** | Not tested | Deferred to operator |
| NETWORK denied | **FAIL** | [test-mailbridge.ps1:11](scripts/test-mailbridge.ps1#L11): `$NetworkDenyVerified` is a PARAMETER defaulting to `$false` | Not automated |

### Suite F — COM Hygiene

| Check | Status | Evidence | Gap |
|---|---|---|---|
| 100 repeated status/list calls | **FAIL** | [test-mailbridge.ps1:241](scripts/test-mailbridge.ps1#L241): loop runs 25 iterations (`$i -lt 25`), not 100 | Spec requires 100 |
| No orphan Outlook processes | **FAIL** | No process count check before/after | Not tested |
| No handle leak | **FAIL** | No handle count monitoring | Not tested |

---

## 8. Security Audit

### Named Pipe ACL Correctness
**PARTIAL FAIL.** The ACL structure is correct in shape (SYSTEM, Admins, primary user, openclaw-svc allow; NETWORK deny). However, SYSTEM and Administrators receive `ReadWrite | CreateNewInstance` instead of the spec-required `FullControl`. The NETWORK deny correctly uses `FullControl`. The `openclaw-svc` SID resolution uses `NTAccount.Translate` which will throw if the account doesn't exist — this is correct fail-fast behavior.

### Local-Only Posture
**PASS.** No HTTP, WebSocket, or network listener code exists. The NETWORK SID is explicitly denied on the pipe.

### Service-User Isolation
**PASS architecturally.** Bridge runs in user session via Task Scheduler. Client CLI is a standalone exe. No shared memory, no service registration. However, the `openclaw-svc` connectivity preflight is not automated.

### No Direct Outlook Access from Service Account
**PASS.** Bridge is the only component that touches Outlook COM, and it runs in the primary user's interactive session.

### Safe Mode Default
**PASS.** `BridgeSettings.Default` has `Mode = "safe"`.

### Redaction Correctness
**CRITICAL FAIL.** Sensitivity-based redaction is entirely absent. The `Sensitivity` field is read from Outlook and stored, but its value is never checked against private (2) or confidential (3). No subject replacement ("Private message" / "Private appointment") occurs. No field nulling based on sensitivity. The `is_redacted` flag is set based on MODE, not SENSITIVITY, which conflates two distinct concepts.

Additionally, safe-mode field suppression is incomplete: `to_json`, `cc_json` (messages), `organizer`, attendee/resource fields (events), and `protected_fields_available=false` are not enforced.

### Logging Hygiene
**PASS for content.** No logger call includes message bodies, subjects, sender info, or preview text. Logger calls use structured parameters for counts and error messages only.
**FAIL for infrastructure.** No file logging provider — `logs\bridge.log` is never written.

---

## 9. Deviations from Spec

| # | Deviation | Severity | Reference |
|---|---|---|---|
| 1 | `AddWindowsService()` and `Microsoft.Extensions.Hosting.WindowsServices` package present | High | [BridgeApplication.cs:52](src/OpenClaw.MailBridge/BridgeApplication.cs#L52), [csproj:26](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L26) |
| 2 | Late-bound COM via reflection instead of PIA/Object Library | High | All COM access through `OutlookComHelpers.InvokeMember` / `GetType().InvokeMember` |
| 3 | Calendar filter `[Start] >= start AND [Start] < end` instead of `[Start] <= end AND [End] >= start` | Critical | [OutlookScanner.cs:338-339](src/OpenClaw.MailBridge/OutlookScanner.cs#L338-L339) |
| 4 | Meeting item_kind = `"meeting"` instead of `"meeting_message"` | Medium | [OutlookScanner.cs:387](src/OpenClaw.MailBridge/OutlookScanner.cs#L387) |
| 5 | No `GetAssociatedAppointment(false)` call | High | Not implemented |
| 6 | No sensitivity-based redaction | Critical | Not implemented |
| 7 | Safe mode doesn't suppress to_json, cc_json, organizer, attendees, resources, or set protected_fields_available=false | Critical | [ResponseShaper.cs:19-54](src/OpenClaw.MailBridge/ResponseShaper.cs#L19-L54) |
| 8 | Pipe ACL: SYSTEM/Admins get ReadWrite+CreateNewInstance, not FullControl | Medium | [PipeRpcWorker.cs:348-355](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L348-L355) |
| 9 | No file logging (bridge.log never written) | High | [BridgeApplication.cs:56-57](src/OpenClaw.MailBridge/BridgeApplication.cs#L56-L57) |
| 10 | `schema_version` never stored in scan_state | Medium | Not implemented |
| 11 | `last_modified_utc` always DBNull for events | Low | [CacheRepository.cs:421](src/OpenClaw.MailBridge/CacheRepository.cs#L421) |
| 12 | to_json, cc_json never populated from Outlook | High | [OutlookScanner.cs:399-400](src/OpenClaw.MailBridge/OutlookScanner.cs#L399-L400) |
| 13 | Attendee/resource JSON fields never populated from Outlook | High | [OutlookScanner.cs:439-441](src/OpenClaw.MailBridge/OutlookScanner.cs#L439-L441) |
| 14 | Individual COM items from EnumerateItems not released | Medium | [OutlookScanner.cs:218-229](src/OpenClaw.MailBridge/OutlookScanner.cs#L218-L229) |
| 15 | `BridgeState.error` never entered | Low | No code path sets it |
| 16 | COM hygiene test runs 25 iterations, not 100 | Medium | [test-mailbridge.ps1:241](scripts/test-mailbridge.ps1#L241) |
| 17 | `is_redacted` set based on mode, not sensitivity | High | Architectural conflation |
| 18 | `Microsoft.Data.Sqlite` at v8.0.11, not .NET 10 version | Low | [csproj:22](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L22) |
| 19 | `System.IO.Pipes.AccessControl` at preview version 6.0.0-preview.5 | Low | [csproj:28](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L28) |
| 20 | Config validation incomplete (4 fields not validated) | Low | [Helpers.cs:121-143](src/OpenClaw.MailBridge.Contracts/Models/Helpers.cs#L121-L143) |
| 21 | Suite E (pipe isolation) not automated | Medium | [test-mailbridge.ps1:10-11](scripts/test-mailbridge.ps1#L10-L11) |
| 22 | No explicit hard-exit on pipe ACL failure | Low | BackgroundService exception propagation |

---

## 10. Remediation List

### Critical

1. **Implement sensitivity-based redaction** — Check `Sensitivity` values 2 (Private) and 3 (Confidential) in both `NormalizeMessage` and `NormalizeEvent`. Replace subject with `"Private message"` / `"Private appointment"`. Null sender, recipient, body preview, location, organizer, attendee fields per spec. Set `is_redacted=true`. Separate from mode-based field suppression.

2. **Fix calendar overlap filter** — Change [OutlookScanner.cs:338-339](src/OpenClaw.MailBridge/OutlookScanner.cs#L338-L339) from `[Start] >= start AND [Start] < end` to `[Start] <= '{end}' AND [End] >= '{start}'` to match the spec's overlap semantics.

3. **Complete safe-mode field suppression** — In `ResponseShaper.ShapeMessage` safe-mode path: null `ToJson`, `CcJson`, set `ProtectedFieldsAvailable=false`. In `ResponseShaper.ShapeEvent` safe-mode path: null `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, set `ProtectedFieldsAvailable=false`.

### High

4. **Implement GetAssociatedAppointment(false)** — When a `MeetingItem` is detected in the inbox scan, call `GetAssociatedAppointment(false)` to link to the appointment. Never pass `true`.

5. **Change meeting item_kind to "meeting_message"** — [OutlookScanner.cs:387](src/OpenClaw.MailBridge/OutlookScanner.cs#L387): change `"meeting"` to `"meeting_message"`. Update the `ListRecentMeetingRequestsAsync` SQL filter in [CacheRepository.cs:194](src/OpenClaw.MailBridge/CacheRepository.cs#L194) accordingly.

6. **Add file logging** — Add a file logging provider (e.g., `Microsoft.Extensions.Logging.File` or custom) that writes to `%LOCALAPPDATA%\OpenClaw\MailBridge\logs\bridge.log`.

7. **Remove AddWindowsService** — Remove `builder.Services.AddWindowsService(...)` from [BridgeApplication.cs:52-55](src/OpenClaw.MailBridge/BridgeApplication.cs#L52-L55) and remove `Microsoft.Extensions.Hosting.WindowsServices` from [OpenClaw.MailBridge.csproj:26](src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj#L26).

8. **Populate to_json, cc_json from Outlook** — Read `To` and `CC` recipients from the Outlook item in `NormalizeMessage` and serialize to JSON arrays.

9. **Populate attendee/resource fields from Outlook** — Read `RequiredAttendees`, `OptionalAttendees`, `Resources` from appointment items in `NormalizeEvent`.

10. **Reference Outlook PIA / Object Library** — Add a COM reference or PIA assembly reference per the setup documentation already in [docs/setup.md](docs/setup.md), and refactor COM access to use early-bound interop.

11. **Separate is_redacted from mode** — `is_redacted` should reflect sensitivity-based redaction only. Mode-based field suppression should control field presence without setting `is_redacted=true`.

### Medium

12. **Fix pipe ACL for SYSTEM/Administrators** — Change `AddAllowRule` calls for SYSTEM and Administrators SIDs at [PipeRpcWorker.cs:84-88](src/OpenClaw.MailBridge/PipeRpcWorker.cs#L84-L88) to grant `PipeAccessRights.FullControl` instead of `ReadWrite | CreateNewInstance`.

13. **Write schema_version to scan_state** — Initialize `schema_version` key during `CacheRepository.InitializeAsync()`.

14. **Release individual COM items** — In `EnumerateItems` or the calling scan methods, release each COM item after normalization using `_com.Release(item)`.

15. **Increase COM hygiene test iterations to 100** — [test-mailbridge.ps1:241](scripts/test-mailbridge.ps1#L241): change `$i -lt 25` to `$i -lt 100`. Add process count / handle monitoring.

16. **Automate Suite E pipe isolation** — Run client CLI under `openclaw-svc` context and verify success. Attempt from unapproved account and verify denial. Test NETWORK SID denial.

17. **Complete config validation** — Add bounds checks for `InboxOverlapMinutes`, `CalendarPastDays`, `CalendarFutureDays`. Validate `LogLevel` against known values.

### Low

18. **Populate last_modified_utc for events** — Read `LastModificationTime` from Outlook appointment items.

19. **Implement error state** — Add a code path that sets `BridgeState.error` (e.g., on unrecoverable pipe ACL failure).

20. **Update Microsoft.Data.Sqlite to .NET 10 version** — Align package version with target framework.

21. **Replace preview System.IO.Pipes.AccessControl** — Use a stable release version.

22. **Add schema validation to acceptance test** — Verify returned JSON fields match expected safe-mode/enhanced-mode schemas.

---

## 11. Final Exit-Criteria Matrix

| Exit Criterion | Status | Evidence | Blocking Issue |
|---|---|---|---|
| Builds and targets net10.0-windows | PASS | `dotnet build` succeeds, all csproj target `net10.0-windows` | — |
| All required artifacts present | PASS | 9/9 artifacts present | — |
| Unit tests pass | PASS | 86 pass, 0 fail, 1 skip | — |
| Bridge is read-only | PASS | No write/send Outlook methods | — |
| Bridge is local-only | PASS | No network listeners | — |
| STA thread for all COM access | PASS | `OutlookStaExecutor` with `SetApartmentState(STA)` | — |
| Named-pipe transport with explicit ACL | PARTIAL | ACL present but SYSTEM/Admins get wrong permission level | SYSTEM/Admins need FullControl |
| Sensitivity-based redaction | **FAIL** | Not implemented | **BLOCKING** — Private/confidential items exposed |
| Safe mode default with correct field suppression | **FAIL** | Safe mode is default but suppression is incomplete (to_json, cc_json, organizer, attendees, resources leaked; protected_fields_available not set to false) | **BLOCKING** — Protected data leaked in safe mode |
| Enhanced mode optional-only | PASS | Default is "safe" | — |
| Calendar overlap filter per spec | **FAIL** | Wrong filter shape | **BLOCKING** — Events partially missed |
| MeetingItem normalization (meeting_message + GetAssociatedAppointment) | **FAIL** | Wrong item_kind; GetAssociatedAppointment not called | **BLOCKING** |
| COM lifetime: release all objects | PARTIAL | Enumerated items not released | Potential COM leak |
| File logging to bridge.log | **FAIL** | Console logging only | **BLOCKING** |
| No Windows service capability | **FAIL** | AddWindowsService() present | **BLOCKING** |
| schema_version in scan_state | **FAIL** | Never written | Moderate |
| Outlet PIA / Object Library reference | **FAIL** | Late-bound reflection, no PIA | **BLOCKING** per spec |
| Acceptance test completeness | **FAIL** | Suite E not automated, Suite F undercounted, schema validation missing | Moderate |
| Pipe ACL hard exit | PARTIAL | Relies on host; no explicit exit | Minor |