# Remediation Inputs: OpenClaw Docker Pre-MVP (#23)

**Prepared:** 2026-04-13
**Prepared by:** GitHub Copilot (feature_code_review_agent)
**Feature Folder:** `docs/features/active/2026-04-11-open-claw-docker-23`
**Source Audit:** `feature-audit.2026-04-13T14-00.md`
**Trigger Condition:** Feature audit verdict = NEEDS REVISION (1 PARTIAL criterion)

---

## Remediation Scope

This document captures the inputs required for the atomic planner to produce a remediation plan. Remediation is scoped to a single, minor evidence gap in the operator-troubleshooting record and a secondary investigation of 3 skipped tests. No production code changes are expected.

---

## Remediation Item 1 — STC5: Empty Calendar-Window Operator Troubleshooting Evidence

**Severity:** Minor  
**Criterion:** `spec.md` §Seeded Test Conditions, STC5 — "empty calendar-window results outside cache range" sub-scenario  
**Audit Finding:** `feature-audit.2026-04-13T14-00.md`, STC5 row  
**current state:** The operator-troubleshooting evidence file (`evidence/qa-gates/operator-troubleshooting.2026-04-13T00-21-39Z.md`) contains `MissingTokenFinding`, `InvalidTokenFinding`, `BridgeNotReadyFinding`, `StaleCacheFinding`, and `DegradedReadinessFinding`, but has no `EmptyCalendarWindowFinding`.

**Why this matters:** STC5 explicitly tests that Core serves transparent empty results (not fabricated data) when a calendar window query falls outside the cached range. This is the critical semantic boundary between correct behavior and a silent failure mode.

**What must be done:**

1. Ensure the Docker compose stack is running (safe state) with the HostAdapter accessible on `localhost:4319`.
2. Run the candidate demo command from `ac-traceability.2026-04-13T00-04-16Z.md`:
   ```
   curl.exe -v -H "Authorization: Bearer $(Get-Content -Path "$env:USERPROFILE\.openclaw\hostadapter.token" -Raw).Trim()" \
     "http://localhost:4319/v1/calendar?start=2026-01-01T00:00:00Z&end=2026-01-02T00:00:00Z&limit=5"
   ```
   The `start`/`end` range (2026-01-01 to 2026-01-02) was chosen to precede any realistic cached polling data; the exact range may be adjusted to match any date guaranteed to be before the earliest cached ingest run.
3. Capture the HTTP response body. Expected behavior: `{"data":{"items":[]},"meta":{"requestId":"...","adapterVersion":"...","bridge":{...}}}` — an empty `items` array with a valid `meta.bridge` block.
4. Append to `evidence/qa-gates/operator-troubleshooting.2026-04-13T00-21-39Z.md` (or create a supplemental file `evidence/qa-gates/empty-calendar-window-demo.md`) with:
   ```
   EmptyCalendarWindowFinding: PASS
   DemoCommand: <recorded command>
   ResponseStatus: 200
   ResponseBody: <recorded response body>
   Observation: items array is empty; no fabricated data; meta.bridge.cacheStale reflects correct freshness state
   ```
5. Update `spec.md` STC5 from `- [ ]` to `- [x]` once the evidence is recorded.
6. Update `feature-audit.2026-04-13T14-00.md` STC5 verdict from PARTIAL to PASS.
7. Update `feature-audit.2026-04-13T14-00.md` Overall Verdict from NEEDS REVISION to PASS.

**Prerequisites:**
- Docker Desktop running with `docker compose up -d`
- HostAdapter running on Windows host (either via `dotnet run` or installed as a background task)
- A valid bearer token in `%USERPROFILE%\.openclaw\hostadapter.token`

**Do-not-do list:**
- Do not add production code, new tests, or new policies to close this item.
- Do not adjust the `limit` cap or modify any API behavior.
- Do not weaken or suppress existing tests.
- Do not create temporary files within tests.

---

## Remediation Item 2 — Skipped Tests Investigation (Advisory)

**Severity:** Minor / Advisory  
**Finding:** `code-review.2026-04-13T14-00.md` Minor finding row 2  
**Current state:** 3 tests are skipped in the final QA run (`coverage.2026-04-13T02-07-35Z.md`: `skipped=3`). Their names and skip reasons are not captured in any QA evidence file.

**What must be done:**

1. Identify the 3 skipped tests by running:
   ```
   dotnet test OpenClaw.MailBridge.sln -c Debug --no-build --list-tests | Select-String "Skipped" -Context 2
   ```
   Or, run the tests with verbose output and grep for `[Skipped]`.
2. For each skipped test:
   - If it is intentionally ignored (e.g., `[Ignore("reason")]`), confirm the reason is documented in code.
   - If it is a conditionally skipped test (e.g., environment guard), confirm the guard is intentional and document it.
   - If it is silently skipping a scenario that should be covered, investigate and determine if coverage or behavior is missing.
3. Record the findings in a new evidence file at `evidence/qa-gates/skipped-tests.YYYY-MM-DDTHH-mm.md` with:
   ```
   SkippedTests:
     - TestName: <full test method name>
       Class: <class name>
       Assembly: <assembly name>
       SkipReason: <documented reason or "Unknown (TODO: investigate)">
       IssueRisk: Low | Medium | High
   ```

**This item is advisory:** Skipped tests do not affect the feature audit verdict. The feature-audit.2026-04-13T14-00.md will move to PASS once Item 1 is resolved. This item should be completed before the PR is merged to preserve test-suite fidelity.

---

## Remediation Item 3 — HTTP 500 vs 503 for Missing Token File (Future Consideration)

**Severity:** Minor  
**Finding:** `code-review.2026-04-13T14-00.md` Minor finding row 1  
**Current state:** When the server-side token file is absent, `AuthMiddleware` returns `HTTP 500` with `ErrorCode: CONFIGURATION_ERROR` rather than `HTTP 503 Service Unavailable`.

**Why this is not blocking:** HTTP 500 is semantically valid for a server-side configuration error. The operator-troubleshooting evidence captures this behavior and documents it clearly. Operators can identify missing token files via the `CONFIGURATION_ERROR` code.

**Suggested future action (post-merge):** Consider changing the missing-token response to `HTTP 503` with a body of `{"error":{"code":"CONFIGURATION_ERROR","message":"Server is unavailable: token file not found."}}` to improve operator automation compatibility. The test `HostAdapterAuthTests.HostAdapter_should_return_401_for_request_without_authorization_header_and_not_invoke_cli` is for a missing *bearer* header (not missing server-side token file); the server-side missing-token test, if it exists, would need to be updated to assert 503.

**Do-not-do now:** Do not implement this change as part of the STC5 remediation. It is a separate, lower-priority improvement. Raise as a new issue if the team agrees to address it.

---

## Remediation Plan Handoff

If an atomic planner plan is generated for Item 1, the following context is provided as input:

**Plan scope:** Items 1 and 2 only (Item 3 is a future consideration, not in scope for this remediation pass)

**AC to close:**
- `spec.md` §Seeded Test Conditions, STC5 — check off when `EmptyCalendarWindowFinding: PASS` is recorded

**Evidence files to create or update:**
- `evidence/qa-gates/operator-troubleshooting.2026-04-13T00-21-39Z.md` — append `EmptyCalendarWindowFinding: PASS`
  OR
- `evidence/qa-gates/empty-calendar-window-demo.md` — new supplemental evidence file (preferred to preserve history)
- `evidence/qa-gates/skipped-tests.YYYY-MM-DDTHH-mm.md` — new file for Item 2

**Artifacts to update on completion:**
- `docs/features/active/2026-04-11-open-claw-docker-23/spec.md` — check off STC5
- `docs/features/active/2026-04-11-open-claw-docker-23/feature-audit.2026-04-13T14-00.md` — update STC5 to PASS, Overall Verdict to PASS
- `docs/features/active/2026-04-11-open-claw-docker-23/user-story.md` — confirm all `[x]` (no changes expected)

**No production code changes expected.**

**Plan of record for this remediation:** To be created by atomic_planner as `remediation-plan.2026-04-13T14-00.md` in `docs/features/active/2026-04-11-open-claw-docker-23/`.
