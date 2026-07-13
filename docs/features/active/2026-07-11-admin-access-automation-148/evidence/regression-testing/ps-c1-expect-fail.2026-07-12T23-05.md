# Capability 1 - Expect-Fail (production script absent)

Timestamp: 2026-07-12T23-05

Command: Invoke-Pester -Configuration (Run.Path = tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1, PassThru)
EXIT_CODE: non-zero (test failure) - PassedCount=0, FailedCount=6, TotalCount=6

Output Summary:
All six capability-1 `It` blocks FAIL because the production script
`scripts/Get-OpenClawControlUiTokenUrl.ps1` does not yet exist (invocation via
`& $script:ScriptPath` cannot resolve the path). This is the expected [expect-fail]
baseline for the test-first sequence. The six behaviors under test are:
(a) valid token + default port -> http://127.0.0.1:18789/#token=<token>;
(b) explicit OPENCLAW_AGENT_PORT used in the URL;
(c) base64url token placed in the fragment verbatim (no re-encoding);
(d) absent OPENCLAW_GATEWAY_TOKEN throws an error naming Invoke-OpenClawAgentOnboarding.ps1 and emits no URL;
(e) empty/whitespace token throws the same guided error and emits no URL;
(f) the token value never appears in the verbose/debug/warning/information/error streams.

Next: implement scripts/Get-OpenClawControlUiTokenUrl.ps1 (P1-T3..P1-T6) to turn these green.
