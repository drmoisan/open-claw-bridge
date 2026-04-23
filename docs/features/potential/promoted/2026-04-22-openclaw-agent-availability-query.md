# openclaw-agent-availability-query (Potential Bug)

- Date captured: 2026-04-22
- Author: drmoisan
- Status: Draft

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

## Summary

The OpenClaw assistant gives defective answers to availability and scheduling questions. When asked "When is my next available 60-minute window?" on 2026-04-22 (Wed), the assistant returned a response that exhibits six distinct defects: it presented times in UTC with no operator-timezone conversion; it labeled a meeting "in progress now" that had already completed earlier in the day; it reported the wrong time for a calendar event ("Monthly Capex Review" shown as 22:00–23:00 UTC when the actual time was 14:00–15:00 local Eastern); it displayed a Thursday event under Wednesday; it included a meeting that had been declined earlier the same day (stale cache) still marked Tentative; and it recommended 00:30 UTC Thursday (overnight local) as the next "clear 60-minute window" because it did not respect the operator's business-hours or meeting-tier policy.

## Environment

- OS/version: Windows 11 workstation; OpenClaw assistant runs in Docker Desktop.
- Agent image: `openclaw-agent` container with `profile: coding` (from `deploy/docker/openclaw-assistant/openclaw.json`).
- Data source: OpenClaw HostAdapter HTTP API at `http://host.docker.internal:4319/v1` (read-only).
- Agent configuration surface: `deploy/docker/openclaw-assistant/{USER.md,AGENTS.md,TOOLS.md,SOUL.md,openclaw.json}` and `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`.
- Operator timezone: America/New_York (Eastern). On 2026-04-22 Eastern is UTC-4 (EDT).

## Steps to Reproduce

1. Start the OpenClaw stack and engage the assistant via the gateway.
2. Ask: "When is my next available 60-minute window?"
3. Observe the assistant's response, which scans the calendar and renders events and the proposed window.

## Expected Behavior

The assistant's availability response should:

1. Render event times in the operator's configured local timezone (Eastern), not UTC.
2. Re-fetch current calendar state before answering so it does not rely on session-start cached data; meetings declined or cancelled since session start must not appear as busy holds.
3. Correctly classify events as past/in-progress/future using the operator's local clock.
4. Restrict "available window" proposals to the operator's configured business hours.
5. Apply the operator's tier-based scheduling policy so higher-tier requests can bump lower-tier holds when proposing availability.
6. Report event dates that match the canonical event start day in the operator's local timezone.

## Actual Behavior

On 2026-04-22 (Wed), the operator received this response (verbatim excerpt):

> **Tonight (Wed Apr 22) — no 60-minute window available:**
>
> | Time (UTC) | Event | Status |
> |---|---|---|
> | 22:00 – 23:00 | Monthly Capex Review | Busy (in progress now) |
> | 23:00 – 23:30 | Data Platform Proposal (RSM) | Tentative |
> | 23:30 – 00:30 | 2026 H2 Planner Executive Review 1.0 | Busy |
>
> **First clear 60-minute window:** Thursday Apr 23, 00:30 – 18:00 UTC is entirely open (a 17.5-hour gap). [...] the earliest possible 60-minute block starts at **00:30 UTC Thursday**.

Defects observed:

1. **Timezone not configured.** Response is entirely UTC. Operator's timezone is Eastern and the assistant has no configured local timezone.
2. **Wrong event time.** Monthly Capex Review is reported as 22:00–23:00 UTC; the actual meeting was 14:00–15:00 local (which is 18:00–19:00 UTC on 2026-04-22). The displayed UTC window is itself incorrect by 4 hours.
3. **Stale "in progress now" label.** Monthly Capex Review was complete hours before the query; the assistant still flagged it as currently running.
4. **Event on wrong day.** Data Platform Proposal (RSM) was scheduled for Thursday 2026-04-23, not Wednesday 2026-04-22.
5. **Stale declined-meeting state.** Data Platform Proposal (RSM) was declined earlier on 2026-04-22, yet the assistant still shows it as Tentative. The at-query calendar state was not consulted.
6. **Business hours ignored.** The assistant proposed 00:30 UTC Thursday (20:30 Wed local) as the "first clear 60-minute window," ignoring the operator's local business-hours constraint.
7. **Meeting tier ignored.** The assistant treats all holds as equally immovable; it does not apply the operator's tier policy where a tier-1 request can bump a lower-tier hold.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot — the operator-captured response is reproduced verbatim above.
- Additional evidence (HostAdapter calendar JSON, agent session logs, declined-meeting timestamps) to be collected during research.

## Impact / Severity

- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

High severity: the assistant's primary purpose is scheduling assistance; incorrect times, stale data, and ignored local constraints make the availability answer actively misleading. Operator cannot trust the output without manually re-checking the calendar, which defeats the assistant's value.

## Suspected Cause / Notes

Likely contributing factors (to be confirmed in research):

1. `deploy/docker/openclaw-assistant/USER.md` is an unpersonalized template — no operator name, timezone, business hours, or tier policy are encoded.
2. `deploy/docker/openclaw-assistant/AGENTS.md` session-start protocol pulls a baseline calendar window but does not require re-fetch before answering availability questions; stale data from session-start is likely being reused.
3. `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` does not instruct the agent to render times in the operator's local timezone, filter declined/cancelled items, or apply business-hours/tier rules.
4. `deploy/docker/openclaw-assistant/TOOLS.md` documents time parameters as ISO-8601 UTC. If the upstream HostAdapter does not expose attendance response status (Accepted/Tentative/Declined) or cancellation status, the agent cannot filter declined items even if instructed to. This is a candidate C# change in `src/OpenClaw.HostAdapter/` (to be verified).
5. The container may also be missing a `TZ` environment variable in `docker-compose.yml` / `openclaw.json` so local-time rendering has a deterministic basis.

## Proposed Fix / Validation Ideas

- [ ] Populate `USER.md` with operator name, timezone, business-hours window, and tier policy (config-only).
- [ ] Update `AGENTS.md` session-start protocol to require a calendar re-fetch before answering availability questions.
- [ ] Update `mailbridge_admin/SKILL.md` to mandate local-timezone rendering, declined-item filtering, business-hours windowing, and tier-aware bumping recommendations.
- [ ] Audit the HostAdapter `/v1/calendar` response shape and add `responseStatus` / `isCancelled` fields if missing; cover with unit tests.
- [ ] Add a C# contract test that the HostAdapter returns attendance-response state for each attendee item.
- [ ] Validate end-to-end by re-asking the same availability question and confirming each of the seven defects no longer reproduces.

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [ ] Move to active fix folder / branch
