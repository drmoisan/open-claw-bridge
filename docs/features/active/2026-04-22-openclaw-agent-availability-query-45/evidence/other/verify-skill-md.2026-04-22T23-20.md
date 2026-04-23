# AC-2 Verification — SKILL.md Scheduling Rules

Timestamp: 2026-04-22T23-20
File: `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md`

## Change Summary

A new `## Scheduling Rules` subsection was inserted between `## Required Workflow` and `## HTTP Patterns`, containing six mandatory rules labeled (a) through (f). No pre-existing heading or content was modified.

## Heading-Preservation Check

Baseline (`git show development:…`): `## Required Workflow`, `## HTTP Patterns`.
Post-change: `## Required Workflow`, `## Scheduling Rules` (new), `## HTTP Patterns`.

All pre-existing headings remain present; single addition is `## Scheduling Rules`.

## Post-change Excerpt (verbatim)

```
## Scheduling Rules

The following rules apply to every availability or scheduling question. They are mandatory and must be applied in addition to the Required Workflow above.

(a) Render every event time in the operator's local timezone from `USER.md`, alongside the original UTC value.

(b) Before answering any availability or scheduling question, perform a fresh `GET /v1/calendar` call covering the relevant time window.

(c) After `GET /v1/status`, consult `meta.bridge.cacheStale`; when `true`, issue a fresh `GET /v1/calendar` before computing any scheduling answer.

(d) Exclude events whose `responseStatus == 4` (Declined, `OlResponseStatus.olResponseDeclined`) from busy holds and from calendar summaries.

(e) Restrict proposed free windows to the operator business-hours range from `USER.md`; do not propose windows outside that range.

(f) Apply the operator's meeting-tier policy from `USER.md`; a documented tier-1 request may propose bumping a lower-tier hold (tier-2 or tier-3) with clear rationale.
```

## Rule-to-AC-2 Mapping

| AC-2 Clause | Rule | Evidence |
|---|---|---|
| (a) local-timezone rendering | (a) | line 30 |
| (b) mandatory pre-answer fresh fetch | (b) | line 32 |
| (c) cacheStale-aware fresh fetch | (c) | line 34 |
| (d) exclude `responseStatus == 4` | (d) | line 36 |
| (e) business-hours windowing | (e) | line 38 |
| (f) tier-aware recommendations | (f) | line 40 |

## AC Reference

Issue #45, Acceptance Criterion AC-2 requires the six rules listed above. All six are present.

AC-2: SATISFIED
