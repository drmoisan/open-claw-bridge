# 2026-04-22-openclaw-agent-availability-query (Plan)

- **Issue:** #45
- **Owner:** drmoisan
- **Last Updated:** 2026-04-22T23-20
- **Status:** Execution complete (AC-1..AC-8 PASS, AC-9 PENDING_MANUAL_VERIFY). See `evidence/qa-gates/traceability-matrix.2026-04-22T23-20.md`.
- **Version:** 1.0
- **Work Mode:** full-bug
- **Feature Folder:** `docs/features/active/2026-04-22-openclaw-agent-availability-query-45`
- **Branch:** `bug/openclaw-agent-availability-query-45`
- **Base Branch:** `development`

## Overview

The OpenClaw assistant returned a defective answer to "When is my next available 60-minute window?" on 2026-04-22 (seven defects, D1–D7). The fix spans four change surfaces: (1) operator and agent markdown configuration (`USER.md`, `AGENTS.md`, `SKILL.md`, `TOOLS.md`), (2) container timezone anchor in `docker-compose.yml`, (3) an additive C# contract change (`EventDto.ResponseStatus`), scanner population, cache persistence, and an idempotent SQLite schema migration, and (4) unit tests plus coverage and manual end-to-end verification.

This plan sequences those changes into six deterministic phases with binary acceptance criteria per task. Every acceptance criterion AC-1 through AC-9 from `issue.md` is explicitly covered by at least one task, with evidence artifact paths specified.

**Fail-closed evidence rule:** Every baseline and verification command-step task writes an evidence artifact. An artifact is complete only when it contains `Timestamp:` (ISO-8601 `yyyy-MM-ddTHH-mm`), `Command:`, `EXIT_CODE:`, and `Output Summary:`. Coverage-bearing evidence artifacts must contain numeric coverage headline values. Do not check off any evidence-backed task without a complete artifact at the stated path.

**Invariants to preserve (must remain unchanged by this plan):**

- `deploy/docker/openclaw-assistant/openclaw.json` must continue to contain `"profile": "coding"` (from issue #43 v2).
- `docker-compose.yml` hardening flags on the `openclaw-agent` service must remain byte-identical outside the single new `environment: TZ` addition: `read_only: true`, `cap_drop: ALL`, `security_opt: ["no-new-privileges:true"]`, tmpfs flags `noexec,nosuid,nodev`.
- `EventDto` change is additive-only: no rename, no reorder, no removal of existing members.
- SQLite schema migration must be idempotent and must not drop or modify existing columns.
- Agent markdown files may only receive additive content; existing section headings remain.
- Write-side calendar actions remain out of scope (HostAdapter remains read-only).

**Verified source paths (used throughout this plan):**

- `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (EventDto)
- `src/OpenClaw.MailBridge/OutlookScanner.cs` (NormalizeEvent)
- `src/OpenClaw.MailBridge/CacheRepository.cs` (events table, upsert/read)
- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` (MSTest project for scanner/cache tests)

Note: `spec.md §"Files/modules to change"` lists `src/OpenClaw.MailBridge.Bridge/...` paths that do not exist in the repository; `research.md §A.6/C.3/F` and `Glob` verification confirm the canonical paths above (no `.Bridge` segment). This plan uses the verified paths.

**Evidence location convention:** All evidence artifacts for this plan are written under
`docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/` in the subfolders defined by `evidence-and-timestamp-conventions`:

- `evidence/baseline/` — Phase 0 baseline captures
- `evidence/other/` — toolchain logs, diffs, structural checks
- `evidence/qa-gates/` — final QA gates, coverage deltas
- `evidence/regression-testing/` — regression / E2E verification

The timestamp token `2026-04-22T23-20` is used uniformly for artifacts generated from this plan version. Executors may append a refined ISO-8601 timestamp per run.

---

### Phase 0 — Preflight and Baseline Capture

- [x] [P0-T1] Record policy-compliance reading order in a Phase 0 evidence artifact
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/phase0-instructions-read.2026-04-22T23-20.md`
  - Acceptance: Artifact exists; contains `Timestamp:` (ISO-8601), `Policy Order:`, and explicit list of every file read — at minimum `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/tonality.md`.

- [x] [P0-T2] Read `CLAUDE.md` (if present) and record read confirmation
  - Acceptance: File content has been read in full; its path appears in `phase0-instructions-read.2026-04-22T23-20.md` under `Policy Order:`. If the file does not exist, the artifact records `CLAUDE.md: not present` under `Policy Order:` with a note.

- [x] [P0-T3] Read `.claude/rules/general-code-change.md` in full
  - Acceptance: Path appears in `phase0-instructions-read.2026-04-22T23-20.md` under `Policy Order:`.

- [x] [P0-T4] Read `.claude/rules/general-unit-test.md` in full
  - Acceptance: Path appears in `phase0-instructions-read.2026-04-22T23-20.md` under `Policy Order:`.

- [x] [P0-T5] Read `.claude/rules/csharp.md` in full
  - Acceptance: Path appears in `phase0-instructions-read.2026-04-22T23-20.md` under `Policy Order:`.

- [x] [P0-T6] Read `.claude/rules/tonality.md` in full
  - Acceptance: Path appears in `phase0-instructions-read.2026-04-22T23-20.md` under `Policy Order:`.

- [x] [P0-T7] Confirm current branch is `bug/openclaw-agent-availability-query-45`
  - Command: `git rev-parse --abbrev-ref HEAD`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-branch.2026-04-22T23-20.md`
  - Acceptance: Command stdout equals `bug/openclaw-agent-availability-query-45`; artifact contains `Timestamp:`, `Command: git rev-parse --abbrev-ref HEAD`, `EXIT_CODE: 0`, `Output Summary:` with the branch name.

- [x] [P0-T8] Capture base SHA of `development` for future diff reference
  - Command: `git rev-parse development`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-base-sha.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, `Command: git rev-parse development`, `EXIT_CODE: 0`, `Output Summary:` with the 40-char SHA.

- [x] [P0-T9] Capture pre-change `git status` baseline
  - Command: `git status`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-git-status.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, `Command: git status`, `EXIT_CODE: 0`, `Output Summary:` with the working-tree state (clean or list of modified files).

- [x] [P0-T10] Verify `dotnet`, `docker`, and `gh` are on PATH
  - Commands:
    - `dotnet --version`
    - `docker --version`
    - `gh --version`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-tools.2026-04-22T23-20.md`
  - Acceptance: Each command exits 0; artifact contains `Timestamp:`, each `Command:`, each `EXIT_CODE: 0`, `Output Summary:` with the version string returned by each tool.

- [x] [P0-T11] Confirm invariant: `openclaw.json` still contains `"profile": "coding"`
  - Command: `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-profile-coding.2026-04-22T23-20.md`
  - Acceptance: Command output contains `"profile": "coding"` (exit code 0); artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary: "profile": "coding" confirmed`.

- [x] [P0-T12] Confirm the feature folder exists with `issue.md` and `spec.md`
  - Commands:
    - `test -f docs/features/active/2026-04-22-openclaw-agent-availability-query-45/issue.md`
    - `test -f docs/features/active/2026-04-22-openclaw-agent-availability-query-45/spec.md`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-feature-folder.2026-04-22T23-20.md`
  - Acceptance: Both commands exit 0; artifact contains `Timestamp:`, each `Command:`, each `EXIT_CODE: 0`, `Output Summary:` listing both file paths as present.

- [x] [P0-T13] Capture baseline C# build status on the MailBridge solution
  - Command: `dotnet build OpenClaw.MailBridge.sln --nologo`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-dotnet-build.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:` (record exact value, 0 if clean), `Output Summary:` with warning/error counts and the final `Build succeeded` / `Build FAILED` line. If the baseline build does not succeed, Phase 3 must still begin from a green tree — record the discrepancy and do not proceed to Phase 3 until the baseline reaches `EXIT_CODE: 0`.

- [x] [P0-T14] Capture baseline repository-wide C# coverage headline via `dotnet test` with coverage
  - Command: `dotnet test OpenClaw.MailBridge.sln --nologo --collect:"XPlat Code Coverage" --results-directory artifacts/coverage/baseline`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-dotnet-test-coverage.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` with numeric pass/fail counts and repository-wide line coverage percent (e.g., `Baseline line coverage: NN.N%`). If the solution-wide coverage value is not emitted directly, also record `Total Line coverage:` extracted from the generated `coverage.cobertura.xml` under `artifacts/coverage/baseline`. Numeric placeholders such as `UNVERIFIED` are invalid.

- [x] [P0-T15] Capture baseline targeted coverage for `OpenClaw.MailBridge` project (scanner + cache)
  - Command: `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --nologo --collect:"XPlat Code Coverage" --results-directory artifacts/coverage/baseline-mailbridge`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-mailbridge-coverage.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` with numeric targeted line-coverage percent for the `OpenClaw.MailBridge` project (baseline value, e.g., `MailBridge line coverage: NN.N%`). Numeric placeholders are invalid.

- [x] [P0-T16] Record pre-change hashes of files that Phase 1/2/3 will modify
  - Command: `git hash-object deploy/docker/openclaw-assistant/USER.md deploy/docker/openclaw-assistant/AGENTS.md deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md deploy/docker/openclaw-assistant/TOOLS.md docker-compose.yml src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs src/OpenClaw.MailBridge/OutlookScanner.cs src/OpenClaw.MailBridge/CacheRepository.cs`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/baseline/baseline-file-hashes.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with one hash per listed file. Used later to prove that unrelated files are unmodified.

---

### Phase 1 — Agent Markdown Configuration (AC-1, AC-2, AC-3, AC-5)

Scope: update operator and agent markdown files. No C# or Docker changes in this phase.

- [x] [P1-T1] Add operator timezone field to `deploy/docker/openclaw-assistant/USER.md`
  - Change: insert `Timezone: America/New_York` into a newly added `## Operator` section (additive; do not modify existing `# Operator Profile`, `## Context of use`, or `## Constraints` headings).
  - Acceptance: `grep -n "Timezone: America/New_York" deploy/docker/openclaw-assistant/USER.md` returns at least one match at a line inside the new `## Operator` section.

- [x] [P1-T2] Add operator weekday business-hours field to `deploy/docker/openclaw-assistant/USER.md`
  - Change: insert `Business hours (weekdays, local): 09:00–17:00` in the `## Operator` section. Default value is `09:00–17:00` unless the operator supplies an override before this task executes.
  - Acceptance: `grep -n "Business hours (weekdays, local): 09:00.17:00" deploy/docker/openclaw-assistant/USER.md` returns at least one match (dash character may be en-dash `–` or hyphen `-`; the grep pattern above allows either).

- [x] [P1-T3] Add meeting-tier policy block to `deploy/docker/openclaw-assistant/USER.md`
  - Change: insert a `Meeting tier policy:` block (or subsection) that defines the four tiers exactly as follows:
    - tier-0 = non-negotiable (do not bump)
    - tier-1 = default (may bump a tier-2 or tier-3 hold)
    - tier-2 = flexible (may bump a tier-3 hold)
    - tier-3 = tentative / can-bump
  - Acceptance: `grep -n "tier-0" deploy/docker/openclaw-assistant/USER.md`, `grep -n "tier-1" …`, `grep -n "tier-2" …`, and `grep -n "tier-3" …` each return at least one match; the four definition lines appear within the same contiguous block under `Meeting tier policy:`.

- [x] [P1-T4] Write AC-1 evidence artifact with `USER.md` post-change excerpt
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/verify-user-md.2026-04-22T23-20.md`
  - Acceptance: Artifact exists and contains `Timestamp:`, a verbatim excerpt of the post-change `## Operator` section showing all three fields (name placeholder or value, `Timezone: America/New_York`, business-hours line, tier policy block), and a reference line to AC-1. `AC-1: SATISFIED` line appears at the end.

- [x] [P1-T5] Add `## Availability-Query Protocol` subsection to `deploy/docker/openclaw-assistant/AGENTS.md`
  - Change: append (or insert after the existing session-start protocol section) a new heading `## Availability-Query Protocol` with four numbered rules:
    1. Before answering any availability or scheduling question, perform a fresh `GET /v1/calendar` fetch.
    2. Render every event time in the operator's local timezone alongside the original UTC value.
    3. Restrict proposed free windows to operator business hours from `USER.md`.
    4. Exclude events with `responseStatus == 4` (Declined) from busy holds.
  - Acceptance: `grep -n "^## Availability-Query Protocol" deploy/docker/openclaw-assistant/AGENTS.md` returns exactly one match; the subsequent four numbered rules are present and textually match the above wording (spot-check any one phrase per rule).

- [x] [P1-T6] Confirm existing `AGENTS.md` headings are intact after P1-T5
  - Command: `grep -nE "^## " deploy/docker/openclaw-assistant/AGENTS.md`
  - Acceptance: All pre-existing `##` headings that existed at baseline (captured via `git show development:deploy/docker/openclaw-assistant/AGENTS.md | grep -nE "^## "`) are present in the post-change file; only the net addition is `## Availability-Query Protocol`.

- [x] [P1-T7] Write AC-3 evidence artifact with `AGENTS.md` post-change excerpt
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/verify-agents-md.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, verbatim excerpt of `## Availability-Query Protocol` with all four rules, and `AC-3: SATISFIED` at the end.

- [x] [P1-T8] Add SKILL rule (a): local-timezone rendering to `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`
  - Change: in the Required Workflow (or a new `## Scheduling Rules` subsection), add rule (a): "Render every event time in the operator's local timezone from `USER.md`, alongside the original UTC value."
  - Acceptance: `grep -n "local timezone" deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` returns at least one match within the added rules block.

- [x] [P1-T9] Add SKILL rule (b): mandatory pre-answer fresh `GET /v1/calendar`
  - Change: add rule (b): "Before answering any availability or scheduling question, perform a fresh `GET /v1/calendar` call covering the relevant time window."
  - Acceptance: `grep -n "fresh \`GET /v1/calendar\`" deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` returns at least one match (or equivalent with backticks around the endpoint); rule text matches wording above.

- [x] [P1-T10] Add SKILL rule (c): `cacheStale`-aware fresh fetch
  - Change: add rule (c): "After `GET /v1/status`, consult `meta.bridge.cacheStale`; when `true`, issue a fresh `GET /v1/calendar` before computing any scheduling answer."
  - Acceptance: `grep -n "cacheStale" deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` returns at least one match.

- [x] [P1-T11] Add SKILL rule (d): exclude `responseStatus == 4` (Declined)
  - Change: add rule (d): "Exclude events whose `responseStatus == 4` (Declined, `OlResponseStatus.olResponseDeclined`) from busy holds and from calendar summaries."
  - Acceptance: `grep -n "responseStatus == 4" deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` returns at least one match.

- [x] [P1-T12] Add SKILL rule (e): restrict proposed windows to operator business hours
  - Change: add rule (e): "Restrict proposed free windows to the operator business-hours range from `USER.md`; do not propose windows outside that range."
  - Acceptance: `grep -n "business.hours" deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` returns at least one match.

- [x] [P1-T13] Add SKILL rule (f): tier-aware recommendation language
  - Change: add rule (f): "Apply the operator's meeting-tier policy from `USER.md`; a documented tier-1 request may propose bumping a lower-tier hold (tier-2 or tier-3) with clear rationale."
  - Acceptance: `grep -n "tier-1" deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` returns at least one match, and `grep -n "bump" deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` returns at least one match (case-insensitive via `-i` if needed).

- [x] [P1-T14] Confirm existing `SKILL.md` section headings are intact
  - Command: `grep -nE "^## " deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`
  - Acceptance: All pre-existing `##` headings from `git show development:deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` remain present after the rule additions.

- [x] [P1-T15] Write AC-2 evidence artifact with `SKILL.md` post-change excerpt
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/verify-skill-md.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, the verbatim post-change excerpt containing all six rules labeled (a) through (f), and `AC-2: SATISFIED` at the end.

- [x] [P1-T16] Add `responseStatus` documentation to `deploy/docker/openclaw-assistant/TOOLS.md`
  - Change: document the new `responseStatus` field on both `GET /v1/calendar` and `GET /v1/events/{bridgeId}` endpoints. Include the full `OlResponseStatus` value table: `0 None`, `1 Organized`, `2 Tentative`, `3 Accepted`, `4 Declined`, `5 NotResponded`. Note that declined items are filtered by the skill.
  - Acceptance: `grep -n "responseStatus" deploy/docker/openclaw-assistant/TOOLS.md` returns at least two matches (one per endpoint section); the value table contains all six rows (`0 None` through `5 NotResponded`).

- [x] [P1-T17] Confirm existing `TOOLS.md` section headings and line-10 UTC contract are intact
  - Command: `grep -n "All date/time parameters use ISO-8601 UTC format" deploy/docker/openclaw-assistant/TOOLS.md`
  - Acceptance: Command returns at least one match; the original UTC-format contract is not removed or modified by the additions.

- [x] [P1-T18] Write AC-5 evidence artifact with `TOOLS.md` post-change excerpt
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/verify-tools-md.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, the verbatim post-change excerpt of the `responseStatus` documentation block including the six-row value table, and `AC-5: SATISFIED` at the end.

- [x] [P1-T19] Diff-check Phase 1: only markdown files under the agent config folder were modified
  - Command: `git status --porcelain`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/phase1-diff-scope.2026-04-22T23-20.md`
  - Acceptance: `git status --porcelain` output contains only the four files modified in Phase 1: `deploy/docker/openclaw-assistant/USER.md`, `deploy/docker/openclaw-assistant/AGENTS.md`, `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`, `deploy/docker/openclaw-assistant/TOOLS.md` (plus new evidence files under `docs/features/active/.../evidence/`). No C#, Docker Compose, or Dockerfile files appear as modified. Artifact records the porcelain output verbatim.

---

### Phase 2 — Container Timezone (AC-6)

Scope: add a single `TZ` environment variable to the `openclaw-agent` service. No other compose changes permitted.

- [x] [P2-T1] Add `TZ: "America/New_York"` to the `environment:` block of the `openclaw-agent` service in `docker-compose.yml`
  - Change: insert a single line `TZ: "America/New_York"` under the existing `environment:` mapping of the `openclaw-agent` service. Do not reorder existing keys.
  - Acceptance: `grep -n "TZ:" docker-compose.yml` returns exactly one match inside the `openclaw-agent` service block; the value is the quoted string `"America/New_York"`.

- [x] [P2-T2] Confirm hardening flags in `docker-compose.yml` are byte-identical on everything except the new TZ line
  - Commands:
    - `grep -n "read_only: true" docker-compose.yml`
    - `grep -n "cap_drop:" docker-compose.yml`
    - `grep -n "no-new-privileges:true" docker-compose.yml`
    - `grep -n "noexec,nosuid,nodev" docker-compose.yml`
  - Acceptance: Each grep returns at least one match within the `openclaw-agent` service block; the counts match the pre-change baseline counts (captured from `git show development:docker-compose.yml` with the same four greps).

- [x] [P2-T3] Verify the diff on `docker-compose.yml` contains only the single TZ addition
  - Command: `git diff development -- docker-compose.yml`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/compose-tz-and-hardening.2026-04-22T23-20.md`
  - Acceptance: Diff added-line count is exactly 1 (or exactly the single `TZ: "America/New_York"` line with no reformatting of other lines); diff removed-line count is exactly 0; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with the single added line, and `AC-6: SATISFIED` at the end.

- [x] [P2-T4] Validate the compose file still parses correctly
  - Command: `docker compose config`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/compose-config-validate.2026-04-22T23-20.md`
  - Acceptance: Command exits 0; artifact contains `Timestamp:`, `Command: docker compose config`, `EXIT_CODE: 0`, `Output Summary:` noting parse success and that the `openclaw-agent` service has `TZ=America/New_York` resolved in the rendered config.

---

### Phase 3 — HostAdapter C# Contract (AC-4, AC-7, AC-8)

Scope: additive `ResponseStatus int?` on `EventDto`; scanner reads the property via `OutlookComHelpers.GetOptionalInt`; `CacheRepository` persists the new column with an idempotent migration; unit tests; C# toolchain loop until clean.

- [x] [P3-T1] Add `int? ResponseStatus { get; init; }` to `EventDto` in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`
  - Change: append `int? ResponseStatus = null` as a new positional parameter at the end of the `EventDto` record declaration (after `IsRedacted`). Placement must not reorder existing members; the addition is strictly tail-appended.
  - Acceptance: `grep -n "ResponseStatus" src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` returns at least one match; the `EventDto` record compiles after P3-T9 toolchain runs.

- [x] [P3-T2] Populate `ResponseStatus` in `OutlookScanner.NormalizeEvent` (`src/OpenClaw.MailBridge/OutlookScanner.cs`)
  - Change: in `NormalizeEvent`, read `AppointmentItem.ResponseStatus` via the existing `OutlookComHelpers.GetOptionalInt("ResponseStatus", appointmentItem, …)` pattern (identical to `BusyStatus`/`MeetingStatus` reads already in that method). Pass the resulting `int?` into the new `EventDto.ResponseStatus` field. On per-event COM error, swallow to `null` with a debug log; do not fail the scan.
  - Acceptance: `grep -n "GetOptionalInt(\"ResponseStatus\"" src/OpenClaw.MailBridge/OutlookScanner.cs` returns at least one match; the call site is inside `NormalizeEvent`; the DTO construction includes the new field.

- [x] [P3-T3] Add `response_status INTEGER NULL` column to the events-table DDL in `src/OpenClaw.MailBridge/CacheRepository.cs`
  - Change: add `response_status INTEGER NULL` to the `CREATE TABLE IF NOT EXISTS events (...)` DDL for new databases, and add an idempotent migration step that runs `ALTER TABLE events ADD COLUMN response_status INTEGER NULL` guarded by a column-existence check (for example via `PRAGMA table_info(events)` inspection) so repeated invocations do not fail. Do not drop or rename existing columns.
  - Acceptance: `grep -n "response_status" src/OpenClaw.MailBridge/CacheRepository.cs` returns at least two matches (DDL + migration); migration code contains an existence guard (e.g., comparison against the result of `PRAGMA table_info(events)`).

- [x] [P3-T4] Persist `ResponseStatus` in the cache `UpsertEvent` path
  - Change: extend the INSERT/UPSERT statement and parameter binding in `CacheRepository` so `response_status` is written when a non-null value is provided (and stored as NULL otherwise).
  - Acceptance: `grep -n "response_status" src/OpenClaw.MailBridge/CacheRepository.cs` shows the column name bound in the upsert SQL; existing upsert behavior for other columns is unchanged (confirmed by toolchain build + existing-test pass in P3-T9/P3-T10).

- [x] [P3-T5] Read `response_status` back in the cache read path and populate `EventDto.ResponseStatus`
  - Change: extend the SELECT column list and row-materialization path in `CacheRepository` so `response_status` is read into the DTO.
  - Acceptance: Round-trip unit test (added in P3-T6) passes. `grep -n "ResponseStatus" src/OpenClaw.MailBridge/CacheRepository.cs` returns at least one match on the read side.

- [x] [P3-T6] Add MSTest unit test: scanner populates `ResponseStatus` from the COM property
  - Change: add a MSTest test file under `tests/OpenClaw.MailBridge.Tests/` (e.g., `tests/OpenClaw.MailBridge.Tests/OutlookScannerResponseStatusTests.cs`) with at least two tests — one asserts that when the mocked COM source returns `3` (Accepted), the resulting `EventDto.ResponseStatus == 3`; the other asserts that when the COM source throws, `ResponseStatus` is `null` and the scan continues.
  - Acceptance: Test file exists at the path above; the two tests have distinct method names; `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --filter "FullyQualifiedName~OutlookScannerResponseStatus" --nologo` reports 2 passed / 0 failed after P3-T9.

- [x] [P3-T7] Add MSTest unit test: `CacheRepository` round-trips `ResponseStatus` (including null)
  - Change: add a test file under `tests/OpenClaw.MailBridge.Tests/` (e.g., `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs`) with at least two tests — one asserts that upserting an event with `ResponseStatus = 4` and reading it back returns `4`; the other asserts that `ResponseStatus = null` round-trips as `null`.
  - Acceptance: Test file exists at the path above; `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --filter "FullyQualifiedName~CacheRepositoryResponseStatus" --nologo` reports 2 passed / 0 failed after P3-T9.

- [x] [P3-T8] Add MSTest unit test: migration is idempotent on an already-migrated schema
  - Change: add a test under `tests/OpenClaw.MailBridge.Tests/` (e.g., `tests/OpenClaw.MailBridge.Tests/CacheRepositoryMigrationIdempotencyTests.cs`) that opens or creates a SQLite database, runs the repository migration step twice in sequence on the same connection, and asserts the second invocation does not throw and leaves the `events` table schema unchanged (verified via `PRAGMA table_info(events)`).
  - Acceptance: Test file exists at the path above; `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --filter "FullyQualifiedName~CacheRepositoryMigrationIdempotency" --nologo` reports 1 passed / 0 failed after P3-T9.

- [x] [P3-T9] Run C# toolchain loop: format → lint/analyzers → nullable/type-check → test; repeat from step 1 if any step fails or modifies files
  - Commands (in order):
    1. `dotnet csharpier .` (or repo-configured formatter command; if the repo uses `dotnet tool run csharpier`, use that invocation)
    2. `dotnet build OpenClaw.MailBridge.sln --nologo /warnaserror`
    3. `dotnet test OpenClaw.MailBridge.sln --nologo`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/toolchain-csharp.2026-04-22T23-20.md`
  - Acceptance: All three commands exit 0 in a single consecutive pass (no file modifications after the final pass begins). Artifact contains `Timestamp:`, each `Command:`, each `EXIT_CODE: 0`, `Output Summary:` with analyzer warning count (must be 0 if `/warnaserror` is used), test pass/fail counts (0 failed), and a note that the loop converged in one clean pass. If any step modifies files or fails, restart from step 1 and record each iteration in the artifact.

- [x] [P3-T10] Confirm no existing C# tests regressed
  - Command: `dotnet test OpenClaw.MailBridge.sln --nologo --filter "FullyQualifiedName!~OutlookScannerResponseStatus&FullyQualifiedName!~CacheRepositoryResponseStatus&FullyQualifiedName!~CacheRepositoryMigrationIdempotency"`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/regression-testing/csharp-regression-existing-tests.2026-04-22T23-20.md`
  - Acceptance: Command exits 0; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with pass/fail counts; failed count is 0.

- [x] [P3-T11] Write AC-4 evidence artifact with C# source excerpts, schema diff, and green unit tests
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/other/verify-event-dto.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, the updated `EventDto` declaration excerpt (showing the additive `int? ResponseStatus` at the tail), the `NormalizeEvent` excerpt showing the new `GetOptionalInt("ResponseStatus", …)` call, the schema diff hunk for the events-table DDL and migration, the test names and their pass counts from P3-T6/P3-T7/P3-T8, and `AC-4: SATISFIED` at the end.

---

### Phase 4 — Coverage and Documentation (AC-7, AC-8)

Scope: final solution-wide coverage with the new tests, delta against baseline, and coverage-delta documentation.

- [x] [P4-T1] Run full solution `dotnet test` in coverage mode and record post-change coverage
  - Command: `dotnet test OpenClaw.MailBridge.sln --nologo --collect:"XPlat Code Coverage" --results-directory artifacts/coverage/post-change`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/qa-gates/qa-dotnet-test-coverage.2026-04-22T23-20.md`
  - Acceptance: Command exits 0; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with total passed/failed test counts (0 failed) and repository-wide line coverage percent (numeric). Numeric placeholders are invalid.

- [x] [P4-T2] Run targeted `dotnet test` on `OpenClaw.MailBridge.Tests` in coverage mode and record post-change targeted coverage
  - Command: `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --nologo --collect:"XPlat Code Coverage" --results-directory artifacts/coverage/post-change-mailbridge`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/qa-gates/qa-mailbridge-coverage.2026-04-22T23-20.md`
  - Acceptance: Command exits 0; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with targeted line-coverage percent for `OpenClaw.MailBridge` (numeric) and for the two changed files (`OutlookScanner.cs`, `CacheRepository.cs`) where available from the Cobertura report.

- [x] [P4-T3] Compute coverage delta and enforce thresholds (≥ 80% repo, ≥ 90% changed module)
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/qa-gates/coverage-delta.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, a table with three rows — `Baseline`, `Post-change`, `Delta` — for both (a) repository-wide line coverage and (b) `OpenClaw.MailBridge` project line coverage; values are numeric and drawn from P0-T14, P0-T15, P4-T1, P4-T2. Artifact records `Repo coverage ≥ 80%: PASS|FAIL` and `Changed module (OpenClaw.MailBridge) new/changed lines coverage ≥ 90%: PASS|FAIL`. If any threshold is FAIL, the artifact records `Outcome: REMEDIATION REQUIRED`; otherwise `Outcome: PASS`. If either threshold is FAIL, do not mark AC-8 satisfied.

- [x] [P4-T4] Write AC-7 evidence artifact consolidating toolchain results
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/qa-gates/qa-toolchain-summary.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, a list that references `toolchain-csharp.2026-04-22T23-20.md` (P3-T9) and its clean-pass status, plus an explicit note that no PowerShell or Python files changed in this feature (and therefore their toolchains are not exercised); closes with `AC-7: SATISFIED`.

- [x] [P4-T5] Write AC-8 evidence artifact referencing `coverage-delta`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/qa-gates/qa-coverage-ac8.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, a reference to `coverage-delta.2026-04-22T23-20.md` with its repo and module percentages, and `AC-8: SATISFIED` only if both thresholds PASS in P4-T3. If P4-T3 outcome is `REMEDIATION REQUIRED`, this artifact records `AC-8: BLOCKED — see coverage-delta` and the plan stops before Phase 5.

---

### Phase 5 — End-to-End Verification (AC-9)

Scope: rebuild the agent image, recreate the container, restart MailBridge, manually replay the operator's question, and capture the resulting response as evidence that D1–D7 no longer reproduce.

- [x] [P5-T1] Rebuild the `openclaw-agent` Docker image
  - Command: `docker compose build openclaw-agent`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/regression-testing/docker-build-agent.2026-04-22T23-20.md`
  - Acceptance: Command exits 0; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` confirming build success.

- [x] [P5-T2] Recreate the `openclaw-agent` container using the updated image and compose TZ
  - Command: `docker compose up -d --force-recreate openclaw-agent`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/regression-testing/docker-recreate-agent.2026-04-22T23-20.md`
  - Acceptance: Command exits 0; artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` noting container recreated; `docker compose exec openclaw-agent printenv TZ` returns `America/New_York` and is recorded in the same artifact.

- [x] [P5-T3] Restart the MailBridge service so the updated scanner + cache are live
  - Command: operator-documented restart procedure (e.g., stop and relaunch the local MailBridge process, or `dotnet run --project src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` depending on how it is hosted on the operator workstation)
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/regression-testing/mailbridge-restart.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, the exact command(s) executed, `EXIT_CODE:` for each (0 when the process started cleanly, or a captured error and next step if not), `Output Summary:` noting that at least one calendar scan cycle completed after restart (observed via MailBridge log line indicating a completed scan) and that `GET /v1/status` now returns `cacheStale: false` with a fresh `LastCalendarScanUtc`.

- [x] [P5-T4] Operator asks the availability question against the updated stack and captures the full response
  - Action: operator sends the exact prompt `When is my next available 60-minute window?` to the OpenClaw assistant via the gateway interface.
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/regression-testing/verify-repro.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, the exact prompt, the full verbatim response excerpt, and a D1–D7 defect checklist where each row records `PASS` (defect did not reproduce) or `FAIL` (defect still reproduces) with a brief observation:
    - D1 — times rendered in operator-local Eastern time alongside UTC
    - D2 — Monthly Capex Review (if present) displays the correct UTC value
    - D3 — no completed meeting is labeled "in progress now"
    - D4 — each event appears under its correct local-date header
    - D5 — events the operator declined are not shown as Tentative
    - D6 — proposed next clear window falls inside operator business hours
    - D7 — recommendation language references the tier policy when relevant
  - AC-9 is satisfied only when all seven rows are `PASS`. If any row is `FAIL`, record `AC-9: BLOCKED — see D# observation` and do not mark AC-9 satisfied; open a remediation subtask.

- [x] [P5-T5] Write AC-9 summary evidence artifact
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/regression-testing/ac9-summary.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, a one-line pass/fail summary per D1–D7 row from P5-T4, an explicit `AC-9: SATISFIED` or `AC-9: BLOCKED` line, and a reference to `verify-repro.2026-04-22T23-20.md` as the source-of-truth artifact.

---

### Phase 6 — Plan Completion and Traceability

- [x] [P6-T1] Check off AC-1 through AC-9 in `issue.md` with references to their evidence artifacts
  - Acceptance: In `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/issue.md`, all nine `- [ ]` entries in the `## Acceptance Criteria` section are updated to `- [x]`, and each checked AC appends a reference line identifying its primary evidence artifact filename (e.g., `Evidence: verify-user-md.2026-04-22T23-20.md`). Only ACs whose evidence artifacts record `SATISFIED` may be checked off; any AC whose evidence records `BLOCKED` remains unchecked and the plan records `Outcome: REMEDIATION REQUIRED`.

- [x] [P6-T2] Update the AC traceability table in this plan file with final evidence paths
  - Acceptance: The Requirements Traceability table below (section "Requirements Traceability") lists for each AC the phase, task IDs, and evidence artifact filenames that satisfy it; every cell is populated (no `TBD`).

- [x] [P6-T3] Confirm invariants after all phases complete
  - Commands:
    - `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json`
    - `grep -n "read_only: true" docker-compose.yml`
    - `grep -n "cap_drop:" docker-compose.yml`
    - `grep -n "no-new-privileges:true" docker-compose.yml`
    - `grep -n "noexec,nosuid,nodev" docker-compose.yml`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/qa-gates/invariants-final.2026-04-22T23-20.md`
  - Acceptance: All five commands exit 0 with matches that equal the pre-change baseline; artifact contains `Timestamp:`, each `Command:`, each `EXIT_CODE: 0`, `Output Summary:` noting each invariant as preserved.

- [x] [P6-T4] Final `git status` and change manifest
  - Command: `git status --porcelain`
  - Evidence artifact: `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/evidence/qa-gates/final-change-manifest.2026-04-22T23-20.md`
  - Acceptance: Artifact contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` listing exactly the expected modified/added files: the four agent markdown files, `docker-compose.yml`, `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`, `src/OpenClaw.MailBridge/OutlookScanner.cs`, `src/OpenClaw.MailBridge/CacheRepository.cs`, three new test files under `tests/OpenClaw.MailBridge.Tests/`, and evidence files under `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/`. No unexpected file modifications (cross-referenced against P0-T16 baseline hashes).

---

## Requirements Traceability

| AC | Phase | Task(s) | Evidence Artifact(s) |
|---|---|---|---|
| AC-1 | Phase 1 | P1-T1, P1-T2, P1-T3, P1-T4 | `verify-user-md.2026-04-22T23-20.md` |
| AC-2 | Phase 1 | P1-T8, P1-T9, P1-T10, P1-T11, P1-T12, P1-T13, P1-T14, P1-T15 | `verify-skill-md.2026-04-22T23-20.md` |
| AC-3 | Phase 1 | P1-T5, P1-T6, P1-T7 | `verify-agents-md.2026-04-22T23-20.md` |
| AC-4 | Phase 3 | P3-T1, P3-T2, P3-T3, P3-T4, P3-T5, P3-T6, P3-T7, P3-T8, P3-T11 | `verify-event-dto.2026-04-22T23-20.md`, `toolchain-csharp.2026-04-22T23-20.md` |
| AC-5 | Phase 1 | P1-T16, P1-T17, P1-T18 | `verify-tools-md.2026-04-22T23-20.md` |
| AC-6 | Phase 2 | P2-T1, P2-T2, P2-T3, P2-T4 | `compose-tz-and-hardening.2026-04-22T23-20.md`, `compose-config-validate.2026-04-22T23-20.md` |
| AC-7 | Phase 3, Phase 4 | P3-T9, P3-T10, P4-T4 | `toolchain-csharp.2026-04-22T23-20.md`, `csharp-regression-existing-tests.2026-04-22T23-20.md`, `qa-toolchain-summary.2026-04-22T23-20.md` |
| AC-8 | Phase 0, Phase 4 | P0-T14, P0-T15, P4-T1, P4-T2, P4-T3, P4-T5 | `baseline-dotnet-test-coverage.2026-04-22T23-20.md`, `baseline-mailbridge-coverage.2026-04-22T23-20.md`, `qa-dotnet-test-coverage.2026-04-22T23-20.md`, `qa-mailbridge-coverage.2026-04-22T23-20.md`, `coverage-delta.2026-04-22T23-20.md`, `qa-coverage-ac8.2026-04-22T23-20.md` |
| AC-9 | Phase 5 | P5-T1, P5-T2, P5-T3, P5-T4, P5-T5 | `docker-build-agent.2026-04-22T23-20.md`, `docker-recreate-agent.2026-04-22T23-20.md`, `mailbridge-restart.2026-04-22T23-20.md`, `verify-repro.2026-04-22T23-20.md`, `ac9-summary.2026-04-22T23-20.md` |

---

## Scope Coverage — 11 Required Scope Items

| # | Scope Item (from caller) | Plan Task(s) |
|---|---|---|
| 1 | `USER.md` operator profile (name, timezone, business hours, tier policy; default 09:00–17:00 and four-tier policy) | P1-T1, P1-T2, P1-T3, P1-T4 |
| 2 | `AGENTS.md` new `## Availability-Query Protocol` subsection | P1-T5, P1-T6, P1-T7 |
| 3 | `SKILL.md` six mandated rules (local rendering; pre-answer fresh fetch; cacheStale-aware fetch; declined exclusion; business-hours windowing; tier-aware recs) | P1-T8 through P1-T15 |
| 4 | `TOOLS.md` documents `responseStatus` with the six-value `OlResponseStatus` table | P1-T16, P1-T17, P1-T18 |
| 5 | `docker-compose.yml` adds `TZ: "America/New_York"` to `openclaw-agent`; hardening unchanged | P2-T1, P2-T2, P2-T3, P2-T4 |
| 6 | `BridgeContracts.cs` additive `int? ResponseStatus` on `EventDto` | P3-T1 |
| 7 | `OutlookScanner.NormalizeEvent` reads `AppointmentItem.ResponseStatus` via `GetOptionalInt`; swallows per-event COM error to null | P3-T2 |
| 8 | `CacheRepository.cs` persists/reads new column; idempotent migration | P3-T3, P3-T4, P3-T5 |
| 9 | SQLite schema migration (additive `ALTER TABLE events ADD COLUMN response_status INTEGER NULL`, idempotent) | P3-T3 |
| 10 | Tests (MSTest) — field population, null handling, migration idempotency | P3-T6, P3-T7, P3-T8 |
| 11 | Evidence collection — named evidence files per AC | Every `verify-*`, `qa-*`, `baseline-*`, and regression-testing artifact listed above |

No scope item is waived.

---

## Implementation Notes

- **Verified source paths:** The plan uses `src/OpenClaw.MailBridge/OutlookScanner.cs` and `src/OpenClaw.MailBridge/CacheRepository.cs` (verified via `Glob`). `spec.md §"Files/modules to change"` references `src/OpenClaw.MailBridge.Bridge/...` paths that do not exist in the repository; the verified paths are authoritative.
- **Test project:** All new MSTest unit tests are added under `tests/OpenClaw.MailBridge.Tests/`. No new test project is created.
- **No PowerShell or Python changes** are expected; if an incidental change occurs during toolchain runs, an additional toolchain-loop task must be inserted for that language before AC-7 is marked satisfied.
- **Idempotent migration:** The P3-T3 migration must guard the `ALTER TABLE` with a `PRAGMA table_info(events)` existence check (or equivalent versioned migration table already used in `CacheRepository`). Running the migration twice in sequence must not throw.
- **No write-side calendar actions** are introduced. The HostAdapter remains read-only.
- **Operator action outside automation scope:** P5-T3 and P5-T4 require operator action on the local workstation. The plan captures their commands and expected evidence, but execution cannot be fully automated by the agent without operator participation.
- **File size limit:** None of the changed files approach the 500-line limit as a result of this plan's additions. If an addition pushes a file near the limit, split into a focused supplemental file per repository policy before continuing.
