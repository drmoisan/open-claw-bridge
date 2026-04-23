# AC-3 Verification — AGENTS.md Availability-Query Protocol

Timestamp: 2026-04-22T23-20
File: `deploy/docker/openclaw-assistant/AGENTS.md`

## Change Summary

A new `## Availability-Query Protocol` subsection was inserted immediately after `## Session-Start Protocol` and before `## Primary Jobs`. No pre-existing heading was modified, renamed, reordered, or removed.

## Heading-Preservation Check

Baseline headings (from `git show development:deploy/docker/openclaw-assistant/AGENTS.md | grep -nE "^## "`):

```
## Session-Start Protocol
## Primary Jobs
## Decision Labels
## Required Output Format
```

Post-change headings (`grep -n "^## " deploy/docker/openclaw-assistant/AGENTS.md`):

```
3:## Session-Start Protocol
15:## Availability-Query Protocol
24:## Primary Jobs
31:## Decision Labels
41:## Required Output Format
```

Net change: single addition `## Availability-Query Protocol` at line 15. All four pre-existing headings remain present.

## Post-change Excerpt (verbatim)

```
## Availability-Query Protocol

Before answering any availability or scheduling question, follow these four rules in order. Session-start baseline data alone is insufficient and must not be reused for availability answers.

1. Before answering any availability or scheduling question, perform a fresh `GET /v1/calendar` fetch covering the relevant time window.
2. Render every event time in the operator's local timezone alongside the original UTC value.
3. Restrict proposed free windows to operator business hours from `USER.md`.
4. Exclude events with `responseStatus == 4` (Declined) from busy holds.
```

## AC Reference

Issue #45, Acceptance Criterion AC-3 requires a new Availability-query protocol subsection in `AGENTS.md` covering (i) pre-answer fresh calendar fetch, (ii) local-timezone rendering, (iii) business-hours filtering, and (iv) declined-item filtering. All four rules are present.

AC-3: SATISFIED
