Timestamp: 2026-04-13T00:21:39Z
Command: `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "<troubleshooting evidence bundle using an isolated HostAdapter on 127.0.0.1:4321 for missing-token validation, targeted dotnet test filters for invalid-token and waiting_for_outlook coverage, and the prior [P7-T10] Docker Desktop validation artifact for stale-cache and degraded-readiness evidence>"`
EXIT_CODE: 0
Output Summary: Troubleshooting evidence is recorded for all required operator scenarios. Missing token file behavior was validated through an isolated HostAdapter instance on `127.0.0.1:4321`, which returned `500 CONFIGURATION_ERROR` with the configured-missing-token message. Invalid bearer-token behavior was validated through the deterministic HostAdapter test `HostAdapter_should_return_401_for_invalid_bearer_token_without_exposing_expected_token`, which passed. The bridge-not-ready branch was validated through the deterministic MailBridge tests `EnsureOutlook_should_set_waiting_for_outlook_when_AutostartOutlook_is_false` and `Outlook_scanner_should_set_waiting_state_when_autostart_disabled`, which both passed and explicitly assert `BridgeState.waiting_for_outlook`. Stale-cache serving and degraded readiness were already demonstrated in [P7-T10], where the Docker Desktop validation Core container returned `/health/ready` `503` with status `degraded`, `/api/status` `200`, and continued serving cached messages and cached events at `200` with 5 items each after the bridge outage was induced.
MissingTokenFinding: PASS
MissingTokenDetails:
- Source: `TestResults/p7-t12-final/hostadapter-missing-token.stdout.log`
- Observation: isolated HostAdapter on `127.0.0.1:4321` returned `HTTP 500`.
- ErrorCode: `CONFIGURATION_ERROR`
- Message: `The configured HostAdapter token file is missing or empty.`
InvalidTokenFinding: PASS
InvalidTokenDetails:
- Source command: `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build --filter "Name~HostAdapter_should_return_401_for_invalid_bearer_token_without_exposing_expected_token"`
- ExitCode: `0`
- Verified behavior: invalid bearer token is rejected before CLI invocation and the response path remains `401 UNAUTHORIZED` without exposing the expected token value.
BridgeNotReadyFinding: PASS
BridgeNotReadyDetails:
- Source command: `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build --filter "Name~EnsureOutlook_should_set_waiting_for_outlook_when_AutostartOutlook_is_false|Name~Outlook_scanner_should_set_waiting_state_when_autostart_disabled"`
- ExitCode: `0`
- Verified behavior: both targeted tests passed and explicitly assert `BridgeState.waiting_for_outlook`, which is the required bridge-not-ready troubleshooting branch when Outlook is unavailable and autostart is disabled.
StaleCacheFinding: PASS
StaleCacheDetails:
- Source: `docs/features/active/2026-04-11-open-claw-docker-23/evidence/qa-gates/docker-desktop-validation.2026-04-13T00-12-28Z.md`
- Verified behavior: after the bridge outage was induced, the Docker Desktop validation Core container continued serving cached messages and cached events at `HTTP 200` with 5 items each.
DegradedReadinessFinding: PASS
DegradedReadinessDetails:
- Source: `docs/features/active/2026-04-11-open-claw-docker-23/evidence/qa-gates/docker-desktop-validation.2026-04-13T00-12-28Z.md`
- Verified behavior: after the bridge outage was induced, the Docker Desktop validation Core container returned `/health/ready` `HTTP 503` with status `degraded` while `/api/status` remained `HTTP 200`.
