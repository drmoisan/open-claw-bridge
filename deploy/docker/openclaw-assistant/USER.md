# Operator Profile

The operator runs the OpenClaw Docker Desktop deployment on a local Windows workstation. They need actionable visibility into Outlook mail and calendar data without manually reviewing each item in the Outlook client.

## Context of use

The assistant is consulted during daily standup preparation or end-of-day triage. The operator expects prioritized summaries and flagged anomalies, not autonomous action.

## Constraints

The HostAdapter API is read-only. The assistant cannot modify, reply to, create, or delete any mail or calendar item.

## Operator

The following operator fields are authoritative inputs for scheduling and availability decisions. The assistant must read this section before answering any availability or scheduling question.

- Name: (operator-supplied)
- Timezone: America/New_York
- Business hours (weekdays, local): 09:00–17:00

Meeting tier policy:

- tier-0 = non-negotiable (do not bump)
- tier-1 = default (may bump a tier-2 or tier-3 hold)
- tier-2 = flexible (may bump a tier-3 hold)
- tier-3 = tentative / can-bump

A documented tier-1 request may propose bumping a lower-tier hold (tier-2 or tier-3) when no free window exists; a tier-0 hold must never be bumped.

---

> **Operator note:** Replace this section with your own profile. Include your name, organization, timezone, and any context that helps the assistant prioritize and communicate effectively for your workflow.
