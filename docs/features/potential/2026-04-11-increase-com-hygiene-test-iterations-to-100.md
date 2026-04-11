---
title: "increase-com-hygiene-test-iterations-to-100 - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-42"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# increase-com-hygiene-test-iterations-to-100 (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

Suite F of the bridge acceptance test ([test-mailbridge.ps1:241](scripts/test-mailbridge.ps1#L241)) is intended to validate that the bridge handles sustained repeated calls without accumulating COM handles or producing orphan Outlook processes. The spec requires 100 iterations. The current loop condition is `$i -lt 25`, so only 25 `status` + `list-messages` call pairs are executed — 25% of the required load. A leak that manifests only after dozens of cycles would not be detected at this scale.

Additionally, the loop contains no baseline capture or post-loop measurement of Outlook process count or handle count, so even if the iteration count were corrected to 100 today, the test would still produce no signal about COM hygiene state — a passing result cannot be interpreted as evidence that no leak occurred.

This is listed as deviation #16 (Medium) in the design audit. It is the acceptance-level counterpart to the source-level `release-individual-com-items` work item.

## Proposed Behavior

Three changes are required to make Suite F a meaningful COM hygiene gate:

**1. Correct the iteration count**

Change `$i -lt 25` to `$i -lt 100` at [test-mailbridge.ps1:241](scripts/test-mailbridge.ps1#L241). The loop body (one `status` call + one `list-messages` call per iteration) is unchanged.

**2. Capture Outlook process count before and after**

Before the loop begins, record the count of running `OUTLOOK.EXE` processes:

```powershell
$outlookProcessesBefore = (Get-Process -Name 'OUTLOOK' -ErrorAction SilentlyContinue).Count
```

After the loop completes, record it again and assert no new Outlook process was spawned:

```powershell
$outlookProcessesAfter = (Get-Process -Name 'OUTLOOK' -ErrorAction SilentlyContinue).Count
if ($outlookProcessesAfter -gt $outlookProcessesBefore) {
    throw "COM hygiene failure: Outlook process count grew from $outlookProcessesBefore to $outlookProcessesAfter after 100 iterations."
}
```

**3. Capture bridge process handle count before and after**

Before the loop, record the handle count of the bridge process (identified by the scheduled task name or by the `OpenClaw.MailBridge` process name):

```powershell
$bridgeProcess = Get-Process -Name 'OpenClaw.MailBridge' -ErrorAction SilentlyContinue | Select-Object -First 1
$handlesBefore = $bridgeProcess?.HandleCount
```

After the loop, re-measure and assert the count has not grown beyond a reasonable threshold. An exact equality check is not practical because the .NET runtime itself acquires and releases handles during normal operation. A threshold of `$handlesBefore * 1.1` (10% growth tolerance) or an absolute cap (e.g., `+ 50 handles`) provides a practical signal without excessive false positives. The exact threshold should be calibrated against a clean run and documented in the test or in a comment.

```powershell
$bridgeProcessAfter = Get-Process -Name 'OpenClaw.MailBridge' -ErrorAction SilentlyContinue | Select-Object -First 1
$handlesAfter = $bridgeProcessAfter?.HandleCount
$handleGrowth = $handlesAfter - $handlesBefore
if ($handleGrowth -gt 50) {
    throw "COM hygiene failure: bridge handle count grew by $handleGrowth after 100 iterations (before=$handlesBefore, after=$handlesAfter)."
}
```

The output of both checks (before/after values and whether they passed) should be written to the operator evidence file via the existing `Write-OperatorEvidence` function or appended to its output path.

## Acceptance Criteria (early draft)

- [ ] The Suite F loop at [test-mailbridge.ps1:241](scripts/test-mailbridge.ps1#L241) runs exactly 100 iterations (`$i -lt 100`).
- [ ] Outlook process count is recorded immediately before the loop starts.
- [ ] Outlook process count is recorded immediately after the loop ends.
- [ ] The test throws (fails) if the post-loop Outlook process count exceeds the pre-loop count.
- [ ] Bridge process handle count is recorded immediately before the loop starts.
- [ ] Bridge process handle count is recorded immediately after the loop ends.
- [ ] The test throws (fails) if handle growth exceeds the defined threshold.
- [ ] The before/after handle and process counts are included in the operator evidence output.
- [ ] The test still calls `status` and `list-messages` on every iteration (no reduction in RPC coverage).
- [ ] The test fails fast on any individual `status` or `list-messages` RPC failure within the loop (existing behavior is retained).
- [ ] If the bridge process cannot be found by name before the loop, the handle check is skipped with a warning rather than a hard failure (the bridge may run under a different process name in some configurations).

## Constraints & Risks

- This change increases Suite F execution time from approximately 25 × (two RPC round-trips) to 100 × (two RPC round-trips). On a system with a pipe round-trip time of ~10 ms per call, this is approximately 2 seconds total; on a slower system it could be several seconds. This is acceptable for an acceptance test that runs after deployment, not in the unit test loop.
- Handle count thresholds are environment-sensitive. A threshold that passes on a developer workstation with a lightly loaded Outlook may fail on a heavily loaded system, or vice versa. The threshold value must be explicitly documented in the script with a comment explaining its basis.
- `Get-Process -Name 'OpenClaw.MailBridge'` assumes the bridge process name matches the assembly name. If the process is renamed or run under a wrapper, this lookup will silently find nothing. The acceptance criterion above requires a warning in that case rather than a hard failure, but the operator must be aware that the handle check is being skipped.
- This work item validates COM hygiene at the acceptance level but does not fix any underlying leak. It should be coupled with `release-individual-com-items` (the source-level fix) so that there is both a regression guard and a fix. Running the updated Suite F before the COM release fix is in place will likely produce a failing handle count check, which is the intended behavior — a pre-existing leak being surfaced by the stricter test.
- `Get-Process` `HandleCount` reflects the OS-reported handle count for the process, which includes file handles, thread handles, and event objects in addition to COM RCWs. It is a coarse proxy for COM handle leaks. A growing count is a useful signal, but a stable count does not guarantee zero COM leaks; it only means the leak (if present) is small enough to not accumulate measurably in 100 iterations.

## Test Conditions to Consider

- [ ] Manual verification: run the updated Suite F against a bridge that has the `release-individual-com-items` fix applied; confirm no Outlook process growth and handle growth within threshold.
- [ ] Manual verification: run the updated Suite F against a bridge that does NOT have the `release-individual-com-items` fix applied (i.e., the current code); confirm that handle growth is measurable and the test throws.
- [ ] Verify the `Write-Output 'AutomatedSuitesPassed: A,B,C,D,F'` line at the end of the script continues to be reached only when all 100 iterations pass.
- [ ] Verify that a single RPC failure within the loop causes an early exit with a descriptive error (existing behavior).
- [ ] Verify operator evidence output contains before/after handle and process count values.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/increase-com-hygiene-test-iterations-to-100/` folder from the template

