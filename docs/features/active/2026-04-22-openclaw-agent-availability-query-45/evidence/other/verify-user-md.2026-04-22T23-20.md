# AC-1 Verification — USER.md operator fields

Timestamp: 2026-04-22T23-20
File: `deploy/docker/openclaw-assistant/USER.md`

## Change Summary

The existing `# Operator Profile`, `## Context of use`, and `## Constraints` headings were preserved in their original positions. A new `## Operator` section was added after `## Constraints` containing:

- `Timezone: America/New_York`
- `Business hours (weekdays, local): 09:00–17:00` (en-dash)
- A four-tier `Meeting tier policy:` block covering tier-0 through tier-3 with interaction rules.

## Post-change Excerpt (verbatim)

```
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
```

## Verification Commands and Results

- `grep -n "Timezone: America/New_York" deploy/docker/openclaw-assistant/USER.md` → line 18 match (EXIT 0).
- `grep -n "Business hours (weekdays, local): 09:00.17:00" deploy/docker/openclaw-assistant/USER.md` → line 19 match (dash is `–`).
- `grep -n "tier-[0-3]" deploy/docker/openclaw-assistant/USER.md` → lines 23, 24, 25, 26, 28 match.

## AC Reference

Issue #45, Acceptance Criterion AC-1 requires `USER.md` to contain timezone (`America/New_York`), business-hours (`09:00–17:00` local), and a written meeting-tier policy. All three fields are present.

AC-1: SATISFIED
