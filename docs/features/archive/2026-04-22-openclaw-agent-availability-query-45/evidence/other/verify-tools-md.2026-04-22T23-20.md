# AC-5 Verification — TOOLS.md responseStatus documentation

Timestamp: 2026-04-22T23-20
File: `deploy/docker/openclaw-assistant/TOOLS.md`

## Change Summary

Both calendar endpoints (`GET /v1/calendar` list and `GET /v1/events/{bridgeId}` single-event) now document the new `responseStatus` field with a six-row OlResponseStatus value table. The pre-existing line-10 UTC contract is preserved (`**Date/time format:** All date/time parameters use ISO-8601 UTC format ...`).

## Verification Commands and Results

- `grep -c "responseStatus" deploy/docker/openclaw-assistant/TOOLS.md` → 4 (2+ required, one per endpoint; the excerpts include a field line and a filtering note for each of two endpoints).
- `grep -n "All date/time parameters use ISO-8601 UTC format" deploy/docker/openclaw-assistant/TOOLS.md` → line 10 (original contract intact).
- `grep -n "^| [0-5] | " deploy/docker/openclaw-assistant/TOOLS.md` → 12 matches across lines 97–102 and 124–129. Each endpoint section has all six rows.

## Post-change Excerpt (verbatim, list endpoint)

```
- **Expected response:** `200 OK` with JSON body `{ "items": [ { "bridgeId": "...", "subject": "...", "start": "...", "end": "...", "responseStatus": <int or null>, ... } ] }`
  - `responseStatus` (optional, nullable integer): the operator's response to a meeting invite for that event. See the OlResponseStatus table below. Items with `responseStatus == 4` (Declined) are filtered out of busy holds by the scheduling skill and must not be treated as busy.
- **Error responses:** `401 Unauthorized` if the token is missing or invalid

**Purpose:** Retrieve calendar events within a specified time window for scheduling conflict analysis.

### OlResponseStatus values

| Value | Name | Meaning |
|---|---|---|
| 0 | None | No response status recorded (typical for operator-owned appointments) |
| 1 | Organized | Operator is the meeting organizer |
| 2 | Tentative | Operator responded tentatively |
| 3 | Accepted | Operator accepted the meeting |
| 4 | Declined | Operator declined the meeting; the skill filters these from busy holds |
| 5 | NotResponded | Operator has not responded yet |
```

The identical table is also repeated under `## Tool: Retrieve Single Calendar Event` with the same six rows and the same filter note.

## AC Reference

Issue #45, Acceptance Criterion AC-5 requires the new `responseStatus` field to be documented for the calendar endpoints with the full OlResponseStatus value mapping (0 None, 1 Organized, 2 Tentative, 3 Accepted, 4 Declined, 5 NotResponded) and a statement that declined items are filtered by the skill. All six values are present in both endpoints, and the filtering note is included.

AC-5: SATISFIED
