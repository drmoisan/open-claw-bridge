# 2026-04-22-openclaw-agent-availability-query (Spec)

- **Issue:** #45
- **Owner:** drmoisan
- **Last Updated:** 2026-04-22
- **Status:** Approved
- **Version:** 1.0

## Context

The OpenClaw assistant is a Docker-Desktop-deployed agent that reads Outlook mail and calendar state through the read-only HostAdapter HTTP API on the host. One of its primary jobs is scheduling triage and availability recommendations.

On 2026-04-22 the assistant returned an availability answer with seven concurrent defects (D1–D7, catalogued in `issue.md`). Root-cause research at `artifacts/research/2026-04-22-openclaw-agent-availability-query-research.md` attributes the defects to five gaps:

1. `USER.md` is an unpersonalized template; no timezone, business hours, or tier policy is encoded.
2. `AGENTS.md` and `skills/mailbridge_admin/SKILL.md` lack local-time rendering, re-fetch-before-availability, declined filtering, business-hours windowing, tier-aware recommendations, and `cacheStale` handling.
3. `EventDto` (C# contract) does not expose attendance response status, and the Outlook scanner does not read `AppointmentItem.ResponseStatus`.
4. `TOOLS.md` does not document a response-status field.
5. The `openclaw-agent` service in `docker-compose.yml` has no `TZ` environment variable; it defaults to UTC with no anchor to operator-local time.

## Repro & Evidence

**Steps to reproduce:**

1. Start the full Docker Compose stack and connect to the OpenClaw assistant via its gateway.
2. Ask: "When is my next available 60-minute window?"
3. Observe the response.

**Expected behavior:** Times render in operator-local Eastern time; only non-declined, non-cancelled events count as busy holds; proposed free windows fall inside the operator's business hours; recommendations honor tier policy; declined / completed items are not listed as current holds.

**Actual behavior:** See verbatim excerpt in `issue.md` and the full potential-entry at `docs/features/potential/promoted/2026-04-22-openclaw-agent-availability-query.md`.

**Frequency:** Deterministic — reproducible on every availability question.

## Scope & Non-Goals

**In scope:**

- Personalize `USER.md` with operator timezone (`America/New_York`), business-hours window, and tier policy.
- Extend `AGENTS.md` session protocol with an availability-query subsection (fresh fetch, local rendering, business-hours filter, declined filter).
- Extend `skills/mailbridge_admin/SKILL.md` with six scheduling rules (see AC-2).
- Additive C# contract change: add `ResponseStatus int?` to `EventDto`, populate in `OutlookScanner.NormalizeEvent`, persist in `CacheRepository`, migrate SQLite schema.
- Document the new field in `TOOLS.md` (value mapping).
- Add `TZ=America/New_York` to the `openclaw-agent` service in `docker-compose.yml`.
- Unit-test coverage for the new C# field and any helper.
- Rebuild instructions / runbook note if needed.

**Out of scope / non-goals:**

- Write-side calendar actions (still blocked by read-only HostAdapter contract).
- Refactoring `RequiredAttendeesJson` / `OptionalAttendeesJson` (known empty; separate issue).
- Changes to the gateway, plugin runtime, or upstream `openclaw-agent` image.
- Non-operator timezones or multi-timezone support.
- Browser dashboard UI changes.

**Explicitly excluded:**

- Any modification to `openclaw.json` beyond what is strictly required (the `coding` profile from issue #43 must remain).
- Any relaxation of container hardening in `docker-compose.yml`.

## Root Cause Analysis

Confirmed gaps (per research artifact):

| Gap | File | Evidence |
|---|---|---|
| No operator timezone / business hours / tier policy | `deploy/docker/openclaw-assistant/USER.md` | Template lines 1–16; operator note at line 15 unreplaced |
| No local rendering / re-fetch / filter / window / tier rules | `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` | Current required-workflow section 1–5 does not mention any of them |
| No availability-query protocol | `deploy/docker/openclaw-assistant/AGENTS.md` | Session-start protocol stops at the 14-day baseline fetch |
| Missing `responseStatus` on events | `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (EventDto) | No field for Outlook `AppointmentItem.ResponseStatus` |
| Scanner does not read `ResponseStatus` | `src/OpenClaw.MailBridge.Bridge/Outlook/OutlookScanner.cs` (`NormalizeEvent`) | No `GetOptionalInt("ResponseStatus", ...)` call |
| No `TZ` on agent container | `docker-compose.yml` (`openclaw-agent` service) | Service block has no `environment: TZ=...` |
| `cacheStale` exposed but unused | `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` | Workflow step 1 reads `/v1/status` but does not branch on `cacheStale` |

## Proposed Fix

### Design summary

Four change surfaces, in order of isolation:

1. **Config / markdown (no build impact):** `USER.md`, `AGENTS.md`, `SKILL.md`, `TOOLS.md`.
2. **Docker compose:** add one line (`TZ`) to `openclaw-agent` environment.
3. **C# contract additive:** `EventDto.ResponseStatus` (nullable int), scanner population, repository persistence, SQLite migration.
4. **Tests:** unit tests for the new field mapping and any skill-side logic that can be unit-checked (parsing, etc.).

### Boundaries and invariants to preserve

- `openclaw.json`: `"profile": "coding"` (from issue #43, v2) must remain.
- `docker-compose.yml` hardening: `read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, tmpfs flags (`noexec,nosuid,nodev`) must remain unchanged.
- `EventDto` change must be additive — no existing consumer may break.
- `CacheRepository` schema migration must be backward-compatible (nullable column with default `null`).
- The HostAdapter API remains read-only; no write-side endpoints are added.
- Research artifact note: `OlResponseStatus` enum values are `0 None`, `1 Organized` (you are the organizer), `2 Tentative`, `3 Accepted`, `4 Declined`, `5 NotResponded`. Filter the skill on value `4` for declined exclusion.

### Dependencies or blocked work

None. All changes are contained within the repository. Operator must rebuild the Docker image and recreate the `openclaw-agent` container to pick up the new configuration + compose TZ. C# changes require a `dotnet build` and a MailBridge service restart to take effect.

### Implementation strategy (what changes, not sequencing)

#### Files/modules to change

| File | Change |
|---|---|
| `deploy/docker/openclaw-assistant/USER.md` | Populate operator fields: name, timezone (`America/New_York`), business hours (weekday 09:00–17:00 local or operator-specified), tier policy (define tier-0/1/2/3 with precedence rules) |
| `deploy/docker/openclaw-assistant/AGENTS.md` | Add `## Availability-Query Protocol` subsection with pre-answer fresh fetch, local rendering, business-hours filter, declined filter |
| `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` | Add six explicit rules: local rendering, re-fetch-before-availability, `cacheStale`-aware fetch, declined exclusion, business-hours windowing, tier-aware recommendations |
| `deploy/docker/openclaw-assistant/TOOLS.md` | Document `responseStatus` field on calendar endpoints with value table |
| `docker-compose.yml` | Add `environment: TZ: "America/New_York"` to `openclaw-agent` service |
| `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | Add `int? ResponseStatus { get; init; }` to `EventDto` |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | In `NormalizeEvent`, read `AppointmentItem.ResponseStatus` via `GetOptionalInt` and populate the DTO |
| `src/OpenClaw.MailBridge/CacheRepository.cs` | Add column, migration, and read/write support for the new field |
| `tests/OpenClaw.MailBridge.Tests/**` | Unit tests for the new field mapping, migration, and any null-handling |

Planner will sequence these into atomic phases.

#### Functions / classes / CLI commands impacted

- `EventDto` — additive member
- `OutlookScanner.NormalizeEvent(AppointmentItem, ...)` — one additional property read
- `CacheRepository.UpsertEvent`, `CacheRepository.GetEvents` (or equivalents) — include the new column
- SQLite schema migration (idempotent `ALTER TABLE events ADD COLUMN response_status INTEGER NULL` guarded by an existence check or versioned migration)

#### Data flow and validation changes

- Calendar event payload gains one additive integer field.
- Agent-side: skill filters out events where `responseStatus == 4` (Declined) before computing busy holds.
- No new validation rules beyond accepting a null or an integer.

#### Error handling and logging updates

- Scanner: log a debug message if `ResponseStatus` throws a COM exception and treat the event's response status as `null`. Do not fail the scan on a single-event read error.
- Skill: if the calendar response does not contain `responseStatus`, continue treating attendance as unknown (do not silently exclude).

#### Rollback / feature-flag considerations

- Rolling back `USER.md` / `AGENTS.md` / `SKILL.md` / `TOOLS.md` / `docker-compose.yml` is a textual revert plus a container rebuild.
- Rolling back the C# change requires the migration to remain in place (SQLite will not drop a column easily); document that the column may remain populated but unread if the code is reverted.

### Technical specifications (interfaces / contracts)

#### Inputs / outputs

- `EventDto` JSON gains a `responseStatus` field (`null` or integer 0–5).
- Outlook COM source: `Microsoft.Office.Interop.Outlook.AppointmentItem.ResponseStatus` (`OlResponseStatus` enum).
- SQLite schema: new column `response_status INTEGER NULL` on the `events` table.

#### Required configuration keys and defaults

```yaml
# docker-compose.yml openclaw-agent service
environment:
  TZ: "America/New_York"
```

```markdown
<!-- USER.md must contain explicit fields matching this structure -->
## Operator
Name: <operator>
Timezone: America/New_York
Business hours (weekdays, local): 09:00–17:00
Meeting tier policy: <tier definitions>
```

#### Backward-compatibility expectations

- `EventDto`: adding a nullable init-only property is source- and wire-compatible.
- Migration: nullable column with default `null` is backward-compatible with existing rows.
- Agent config (markdown): all additions are additive; existing rules remain.
- `TZ` env var: does not affect container hardening or network behavior; only formats / `/etc/localtime` inside the container.

#### Performance constraints

None material. One extra COM property read per event during scan; negligible overhead on scans of ~hundreds of events. Schema migration runs once on startup.

## Assumptions, Constraints, Dependencies

**Assumptions:**

- Operator uses `America/New_York` as their single local timezone (DST-aware).
- Operator business hours default to weekdays 09:00–17:00 local unless the operator edits `USER.md`.
- The Outlook COM `AppointmentItem.ResponseStatus` property reflects the current attendance state after a user declines via Outlook UI (confirmed by Microsoft docs).
- The MailBridge scanner will pick up response-status changes on its next scan cycle (default 300s).

**Constraints:**

- Container hardening in `docker-compose.yml` (AC-6) must not regress.
- Repository-wide coverage ≥ 80 percent and changed-module coverage ≥ 90 percent (AC-8).
- The `coding` tools profile from issue #43 must remain active.

**Dependencies:**

- None external. All changes are local.

## Test plan (high level)

- Unit tests for `OutlookScanner.NormalizeEvent` populating `ResponseStatus` from a mocked `AppointmentItem`.
- Unit tests for `CacheRepository` round-tripping the new column, including null handling.
- Migration unit test confirming the `ALTER TABLE` is idempotent on a populated schema.
- Markdown review of `USER.md`, `AGENTS.md`, `SKILL.md`, `TOOLS.md` for AC criteria compliance.
- End-to-end manual verification (AC-9): operator re-asks the availability question and confirms each of D1–D7 does not reproduce.
