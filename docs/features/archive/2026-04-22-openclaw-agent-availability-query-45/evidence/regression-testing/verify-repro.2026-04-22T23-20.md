# P5-T4 — Operator Repro Verification (operator runbook + evidence template)

Timestamp: 2026-04-22T23-20

## Status

PENDING_MANUAL_VERIFY

## Operator Runbook

Once P5-T1, P5-T2, and P5-T3 are complete, send the exact prompt to the OpenClaw assistant through the gateway interface and record the full verbatim response.

### Steps

1. Open the operator gateway interface (web UI or CLI) that is wired to the `openclaw-agent` service.
2. Send the exact prompt (no paraphrasing):

   ```
   When is my next available 60-minute window?
   ```

3. Capture the full assistant response verbatim (including any structured sections or proposed windows).
4. Evaluate each of the D1–D7 defects from `issue.md` against the new response. Record PASS (defect did not reproduce) or FAIL (defect still present) with a short observation for each row below.
5. Store the verbatim prompt and response in this artifact in the "Captured Response" section.

## Defect Checklist

| # | Defect | Status | Observation |
|---|---|---|---|
| D1 | Times rendered in operator-local Eastern time alongside UTC | PENDING | |
| D2 | Monthly Capex Review (if present) displays the correct UTC value | PENDING | |
| D3 | No completed meeting is labeled "in progress now" | PENDING | |
| D4 | Each event appears under its correct local-date header | PENDING | |
| D5 | Events the operator declined are not shown as Tentative | PENDING | |
| D6 | Proposed next clear window falls inside operator business hours (09:00–17:00 local) | PENDING | |
| D7 | Recommendation language references the tier policy when relevant | PENDING | |

## AC-9 Determination

- **AC-9 is SATISFIED** only when all seven rows are PASS.
- If any row is FAIL, mark this artifact as `AC-9: BLOCKED — see D# observation` and open a remediation subtask before closing the plan.

## Captured Response (to be filled in by operator)

### Prompt

```
When is my next available 60-minute window?
```

### Response

```
<paste full verbatim response here>
```

### Verification timestamp

```
<ISO-8601 timestamp at which the response was captured>
```
