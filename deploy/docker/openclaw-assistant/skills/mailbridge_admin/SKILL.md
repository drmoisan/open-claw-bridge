---
name: mailbridge_admin
description: Read Outlook inbox, meeting requests, and calendar from the OpenClaw HostAdapter HTTP API.
metadata:
  openclaw:
    os: ["windows"]
---

# MailBridge Admin

Use this skill whenever the operator asks about inbox items, meeting requests, scheduling conflicts, calendar analysis, or drafting an administrative response.

## Required Workflow

1. Call `GET /v1/status` on the HostAdapter. If the bridge is not `ready`, stop and report the bridge state.
2. Use list endpoints first:
   - `GET /v1/messages?since=<utc>&limit=<n>`
   - `GET /v1/meeting-requests?since=<utc>&limit=<n>`
   - `GET /v1/calendar?start=<utc>&end=<utc>&limit=<n>`
3. Use get endpoints only for items already identified as relevant:
   - `GET /v1/messages/{bridgeId}`
   - `GET /v1/events/{bridgeId}`
4. Respect redaction: if `isRedacted=true` or the bridge mode is `safe`, state that details are unavailable. Never fabricate sender, body, or attendee details.
5. The bridge is read-only: never claim to have replied, sent, created, updated, accepted, declined, or rescheduled anything.

## Scheduling Rules

The following rules apply to every availability or scheduling question. They are mandatory and must be applied in addition to the Required Workflow above.

(a) Render every event time in the operator's local timezone from `USER.md`, alongside the original UTC value.

(b) Before answering any availability or scheduling question, perform a fresh `GET /v1/calendar` call covering the relevant time window.

(c) After `GET /v1/status`, consult `meta.bridge.cacheStale`; when `true`, issue a fresh `GET /v1/calendar` before computing any scheduling answer.

(d) Exclude events whose `responseStatus == 4` (Declined, `OlResponseStatus.olResponseDeclined`) from busy holds and from calendar summaries.

(e) Restrict proposed free windows to the operator business-hours range from `USER.md`; do not propose windows outside that range.

(f) Apply the operator's meeting-tier policy from `USER.md`; a documented tier-1 request may propose bumping a lower-tier hold (tier-2 or tier-3) with clear rationale.

## HTTP Patterns

All requests use base URL `http://host.docker.internal:4319/v1` and require:
```
Authorization: Bearer <token>
```
Read the token from the file at `/run/openclaw/hostadapter.token`.

| Endpoint | Pattern |
|---|---|
| Bridge status | `GET /v1/status` |
| List messages | `GET /v1/messages?since=2026-04-11T00:00:00Z&limit=50` |
| Get message | `GET /v1/messages/{bridgeId}` |
| List meeting requests | `GET /v1/meeting-requests?since=2026-04-11T00:00:00Z&limit=50` |
| List calendar events | `GET /v1/calendar?start=2026-04-18T00:00:00Z&end=2026-05-02T00:00:00Z&limit=100` |
| Get calendar event | `GET /v1/events/{bridgeId}` |
