---
title: "automate-suite-e-pipe-isolation - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-43"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# automate-suite-e-pipe-isolation (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

Suite E in `scripts/test-mailbridge.ps1` covers three named-pipe ACL enforcement checks: `openclaw-svc` connectivity, unapproved account denial, and NETWORK SID denial. All three are currently deferred to operator-supplied boolean flags (`$OpenClawSvcPipeConnect = $false`, `$NetworkDenyVerified = $false` at lines 10–11). The script's `Write-OperatorEvidence` function (lines 112–126) records whatever the caller passes; it does not attempt any connection, assert any exit code, or verify any ACL outcome. As a result, the three Suite E checks are structurally FAIL in the audit even when the pipe ACL itself is correctly constructed.

The named-pipe ACL is a security control. `openclaw-svc` must be able to connect (functional requirement). Arbitrary unpermissioned accounts must be refused (access control requirement). NETWORK SID must be denied to enforce the local-only posture (transport security requirement). Without machine-verified assertions for each of these, the ACL can regress silently — for example, if the `BuildPipeSecurity` logic is modified — with no test catching the regression.

## Proposed Behavior

Replace the three boolean operator flags with active, machine-executed checks inside `test-mailbridge.ps1`. Each check runs after the bridge is confirmed ready (after Suite A completes). The checks are:

**Check 1 — `openclaw-svc` connectivity:** Register a one-shot Windows Scheduled Task under the `openclaw-svc` identity. The task runs `OpenClaw.MailBridge.Client.exe status` and writes its exit code to a fixed temp file. The test function starts the task, polls for completion, reads the exit code from the file, and asserts it equals `0`. After the assertion, the task and temp file are unregistered and deleted. This approach does not require knowing the `openclaw-svc` password at test time — scheduled tasks run with the stored credentials for the account.

**Check 2 — Unapproved account denied:** Using a pre-existing local standard account that is not in the pipe ACL (account name passed as a parameter, or a well-known local test account), register a one-shot scheduled task under that account's identity and run the same `status` command. Assert that the exit code is non-zero (expected: `2`, the client's IOException exit code for pipe connection failure). The test must also confirm that the failure reason is pipe access or connection timeout, not an unrelated error such as a missing executable.

**Check 3 — NETWORK SID denial:** Use `Invoke-Command -ComputerName localhost` (PowerShell Remoting on loopback) to invoke the client CLI. A loopback WinRM session adds the NETWORK SID to the caller's token — this is the standard technique for exercising NETWORK SID denials without a second machine. Assert that the command fails with a pipe access error. The test must distinguish WinRM-not-enabled failures (a skip condition) from pipe ACL enforcement failures (a pass condition).

All three checks write their outcome to `Write-OperatorEvidence` via structured pass/fail fields, replacing the previous boolean flags. The script should output `AutomatedSuitesPassed: A,B,C,D,E,F` only when all three Suite E assertions pass (or skip with a documented precondition failure).

## Acceptance Criteria (early draft)

- [ ] When `openclaw-svc` exists on the machine and a scheduled task can be registered under that account, running the client CLI as `openclaw-svc` succeeds with exit code `0` and the response contains `ok: true`.
- [ ] When an unapproved local account (not in the pipe ACL) runs the client CLI, the exit code is non-zero and the failure is consistent with pipe access denial or connection timeout — not an unrelated error.
- [ ] When the client CLI is invoked via loopback WinRM (`Invoke-Command -ComputerName localhost`), the connection fails with a pipe access or IOException exit code — not exit code `0`.
- [ ] If `openclaw-svc` does not exist on the machine, Check 1 is skipped with an explicit console message stating the precondition is absent — the suite does not falsely pass.
- [ ] If WinRM is not enabled or loopback remoting is not configured, Check 3 is skipped with an explicit console message — the suite does not falsely pass.
- [ ] The `$OpenClawSvcPipeConnect` and `$NetworkDenyVerified` script parameters are superseded. `Write-OperatorEvidence` records structured outcomes from the automated checks instead of operator-supplied flags.
- [ ] All existing suites (A, B, C, D, F) continue to pass when Suite E automation is added.

## Constraints & Risks

- **Scheduled task approach requires admin privileges.** `Register-ScheduledTask` requires administrator rights. The test script already targets an operator context that has admin rights (Task Scheduler management, script installation), so this is consistent with the existing precondition posture.
- **`openclaw-svc` must exist for Check 1.** If the account does not exist, `Register-ScheduledTask` with that principal will fail. The implementation must probe for the account's existence before attempting task registration and skip the check cleanly if the account is absent.
- **Stored credentials must be set for the scheduled-task approach.** The task must be configured with `RunLevel = Highest` and the correct logon type. If `openclaw-svc` has no cached logon token, the task may fail to launch rather than fail at the pipe. The test must distinguish task-launch failure from pipe-access failure.
- **WinRM loopback may require explicit configuration.** `Invoke-Command -ComputerName localhost` works only if WinRM is running and the current user has remoting permissions. In hardened environments, localhost remoting may be disabled. The test must handle `WinRM` connection errors as a skip condition, not a pass or unexplained fail.
- **Exit code 2 is ambiguous.** The client returns exit code `2` for both `IOException` (pipe connection failure) and other I/O errors. The test for Check 2 and Check 3 should also capture stderr output from the client and assert it contains a pipe-related message to confirm the denial is from the ACL and not from, e.g., a missing executable.
- **Temp file cleanup.** The exit-code temp files used by scheduled tasks must be cleaned up after each check, including on failure paths.
- **Bridge must be running.** All three checks require the bridge to be listening. They must execute only after Suite A confirms the bridge is in `ready` state.

## Test Conditions to Consider

- [ ] **Check 1 — `openclaw-svc` succeeds:** Account exists → task registered → client runs → exit code `0` → response has `ok=true`.
- [ ] **Check 1 — `openclaw-svc` absent:** Account does not exist → skip message emitted → operator evidence records `OpenClawSvcPipeConnect: Skipped (account not found)`.
- [ ] **Check 2 — Unapproved account denied:** Non-ACL account runs client → exit code `2` → stderr indicates pipe connection failure → operator evidence records `UnapprovedAccountDenied: Pass`.
- [ ] **Check 3 — NETWORK SID denied via WinRM loopback:** `Invoke-Command -ComputerName localhost` with client CLI → remote invocation succeeds (WinRM connection itself works) but client fails at pipe → exit code non-zero → operator evidence records `NetworkDenyVerified: Pass`.
- [ ] **Check 3 — WinRM not enabled:** `Invoke-Command` throws WinRM connectivity error → skip message emitted → operator evidence records `NetworkDenyVerified: Skipped (WinRM not available)`.
- [ ] **Regression:** Suites A, B, C, D, F output lines unchanged. `AutomatedSuitesPassed` line updated to include `E` only when all non-skipped Suite E checks pass.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/automate-suite-e-pipe-isolation/` folder from the template

