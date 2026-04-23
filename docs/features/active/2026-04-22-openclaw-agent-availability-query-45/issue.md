# Bug — OpenClaw assistant availability query: timezone, staleness, business-hours, and tier defects (Issue #45)

- Promotion type: bug
- Branch: `bug/openclaw-agent-availability-query-45`
- Base branch: `development`
- Created (UTC): 2026-04-22
- Associated GitHub issue: https://github.com/drmoisan/open-claw-bridge/issues/45

- Issue: #45
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/45
- Last Updated: 2026-04-22
- Status: Promoted -> `docs/features/active/2026-04-22-openclaw-agent-availability-query-45/`
- Work Mode: full-bug

## Summary

The OpenClaw assistant gave a defective answer to "When is my next available 60-minute window?" on 2026-04-22. The response exhibits seven distinct defects rooted in missing operator configuration, missing skill-level rules, a limited HostAdapter data contract, and an absent timezone anchor on the agent container. The bug is reproducible and blocks the assistant from reliably performing scheduling triage, which is one of its primary jobs.

Full potential-entry with verbatim defective response: `docs/features/potential/promoted/2026-04-22-openclaw-agent-availability-query.md`. Root-cause research: `artifacts/research/2026-04-22-openclaw-agent-availability-query-research.md`.

## Defect Inventory

| # | Defect | Evidence |
|---|---|---|
| D1 | Times rendered in UTC; no operator-local conversion | Defective response shows "Time (UTC)" column header; `USER.md` has no timezone; `SKILL.md` has no render rule |
| D2 | Wrong UTC value for "Monthly Capex Review" (22:00–23:00 reported; 18:00–19:00 UTC actual) | Operator confirmation; evidence collection pending |
| D3 | Completed meeting labeled "(in progress now)" | Defective response; session-start data reused without re-fetch |
| D4 | Thursday event reported under Wednesday | Data Platform Proposal actual date Thu 2026-04-23 |
| D5 | Declined meeting still shown as Tentative | Operator declined earlier 2026-04-22; `EventDto` lacks `ResponseStatus` |
| D6 | 00:30 UTC Thu (20:30 Wed local) proposed as next clear window | Business hours not configured or enforced |
| D7 | Meeting-tier policy not applied | `USER.md` lacks tier policy; `SKILL.md` has no tier rule |

## Acceptance Criteria

- [x] **AC-1** — `deploy/docker/openclaw-assistant/USER.md` contains explicit operator fields for timezone (`America/New_York`), weekday business-hours window (e.g., `09:00–17:00` local), and a written meeting-tier policy that defines tiers and their interaction rules. Evidence: `verify-user-md.2026-04-22T23-20.md`.
- [x] **AC-2** — `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` mandates: (a) rendering every event time in the operator's local timezone alongside the original UTC value; (b) a mandatory calendar re-fetch immediately before computing any availability answer; (c) consultation of `meta.bridge.cacheStale` and a fresh `GET /v1/calendar` when true; (d) exclusion of events with response status `Declined` (OlResponseStatus 4) from busy holds; (e) proposed windows restricted to operator business hours from `USER.md`; (f) tier-aware recommendations that can propose bumping a lower-tier hold for a documented tier-1 request. Evidence: `verify-skill-md.2026-04-22T23-20.md`.
- [x] **AC-3** — `deploy/docker/openclaw-assistant/AGENTS.md` session-start protocol adds an "Availability-query protocol" subsection that requires: (i) a pre-answer fresh calendar fetch, (ii) local-timezone rendering, (iii) business-hours filtering, (iv) declined-item filtering. Evidence: `verify-agents-md.2026-04-22T23-20.md`.
- [x] **AC-4** — HostAdapter `EventDto` (`src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`) includes an additive `ResponseStatus int?` field that carries `AppointmentItem.ResponseStatus` values. `OutlookScanner.NormalizeEvent` populates it from the Outlook COM `ResponseStatus` property. `CacheRepository` stores and retrieves the new column; schema migration adds the column with a backfill default of `null`. Evidence: `verify-event-dto.2026-04-22T23-20.md`, `toolchain-csharp.2026-04-22T23-20.md`.
- [x] **AC-5** — `deploy/docker/openclaw-assistant/TOOLS.md` documents the new `responseStatus` field for the calendar endpoints, including value mapping (`0 None`, `1 Organized`, `2 Tentative`, `3 Accepted`, `4 Declined`, `5 NotResponded`) and states that declined items are filtered by the skill. Evidence: `verify-tools-md.2026-04-22T23-20.md`.
- [x] **AC-6** — `docker-compose.yml` sets `TZ=America/New_York` on the `openclaw-agent` service so container wall-clock comparisons anchor to operator-local time. Hardening flags (`read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, and the three tmpfs mount flags) remain unchanged. Evidence: `compose-tz-and-hardening.2026-04-22T23-20.md`, `compose-config-validate.2026-04-22T23-20.md`.
- [x] **AC-7** — Toolchains pass on the changed-file set:
  - C# — CSharpier format clean; `dotnet build` with analyzers clean; `dotnet test` MSTest green.
  - Markdown — no required toolchain gate but must pass any repository markdown lint.
  Evidence: `toolchain-csharp.2026-04-22T23-20.md`, `csharp-regression-existing-tests.2026-04-22T23-20.md`, `qa-toolchain-summary.2026-04-22T23-20.md`.
- [x] **AC-8** — Repository-wide line coverage remains ≥ 80 percent; any new or changed C# module reaches ≥ 90 percent. Evidence: `coverage-delta.2026-04-22T23-20.md`, `qa-coverage-ac8.2026-04-22T23-20.md` (repo 89.34%, new/modified methods 100%).
- [ ] **AC-9** — PENDING_MANUAL_VERIFY. End-to-end repro is verified: the operator re-asks "When is my next available 60-minute window?" against the updated stack and confirms each of D1–D7 no longer reproduces. Operator runbooks prepared at `docker-build-agent.2026-04-22T23-20.md`, `docker-recreate-agent.2026-04-22T23-20.md`, `mailbridge-restart.2026-04-22T23-20.md`, `verify-repro.2026-04-22T23-20.md`, and summary template at `ac9-summary.2026-04-22T23-20.md`. The operator must execute P5-T1 through P5-T4 and update the defect checklist before AC-9 can be checked off.

## Out of Scope

- Write-side actions (reply, accept, decline, reschedule): the HostAdapter API is read-only and this bug does not change that.
- Global changes to the Outlook scanner beyond reading the additional `ResponseStatus` property.
- Refactoring the existing attendee-JSON fields (`RequiredAttendeesJson`, `OptionalAttendeesJson`) that are currently always null.
- Non-English timezone / calendar locales beyond the operator's (`America/New_York`).
- Changes to the gateway, plugin runtime, or upstream `openclaw-agent` image.
- Browser-based dashboard UI.

## References

- Research artifact: `artifacts/research/2026-04-22-openclaw-agent-availability-query-research.md`
- Agent skill contract: `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`
- Agent configuration: `deploy/docker/openclaw-assistant/{USER.md,AGENTS.md,TOOLS.md,SOUL.md,openclaw.json}`
- HostAdapter contract: `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`
- Scanner: `src/OpenClaw.MailBridge/OutlookScanner.cs`
- Cache repository: `src/OpenClaw.MailBridge/CacheRepository.cs`
- Container compose: `docker-compose.yml`
- Prior related bug (capabilities=none): `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/`
