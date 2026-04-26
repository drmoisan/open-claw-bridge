Timestamp: 2026-04-13T00:14:17Z
Command: `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "<validation wrapper that temporarily copied TestResults/qa-csharp/phase7-bridge.settings.json into %LOCALAPPDATA%\\OpenClaw\\MailBridge\\bridge.settings.json, verified OpenClaw.MailBridge.Client.exe status, exercised HostAdapter routes on 127.0.0.1:4319 with the phase7 token, launched docker run --name openclaw-core-validation -p 127.0.0.1:8082:8080 openclaw/core:pre-mvp with 5-second poll intervals, ran targeted automated contract tests via dotnet test --filter, then performed live route checks for all required HostAdapter and Core endpoints>"`
EXIT_CODE: 0
Output Summary: Targeted automated contract tests passed with exit code `0`. Live manual contract checks also passed for every required HostAdapter and Core route against the safe-state validation environment. HostAdapter returned `200` for `/v1/status`, `/v1/messages`, `/v1/messages/{bridgeId}`, `/v1/meeting-requests`, `/v1/calendar`, and `/v1/events/{bridgeId}`. The temporary Docker Desktop validation Core container on `127.0.0.1:8082` returned `200` for `/health/live`, `/health/ready`, `/api/status`, `/api/messages/recent`, `/api/messages/{bridgeId}`, `/api/events/window`, and `/api/events/{bridgeId}`. The automated contract coverage came from targeted MSTest execution over `HostAdapterEnvelopeTests`, `HostAdapterEndpointTests`, `HostAdapterValidationTests`, `CoreReadinessTests`, `CoreStatusTests`, `CoreMessagesApiTests`, and `CoreEventsApiTests`.
AutomatedChecks: PASS
AutomatedChecksDetails:
- Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build --filter "FullyQualifiedName~OpenClaw.HostAdapter.Tests.HostAdapterEnvelopeTests|FullyQualifiedName~OpenClaw.HostAdapter.Tests.HostAdapterEndpointTests|FullyQualifiedName~OpenClaw.HostAdapter.Tests.HostAdapterValidationTests|FullyQualifiedName~OpenClaw.Core.Tests.CoreReadinessTests|FullyQualifiedName~OpenClaw.Core.Tests.CoreStatusTests|FullyQualifiedName~OpenClaw.Core.Tests.CoreMessagesApiTests|FullyQualifiedName~OpenClaw.Core.Tests.CoreEventsApiTests" --results-directory "TestResults/p7-t11-12-live/contract-tests"`
- ExitCode: `0`
- Validation Core log captured at `TestResults/p7-t11-12-live/core-contract.log`.
GET /v1/status: PASS (HTTP 200)
GET /v1/messages: PASS (HTTP 200)
GET /v1/messages/{bridgeId}: PASS (HTTP 200)
GET /v1/meeting-requests: PASS (HTTP 200)
GET /v1/calendar: PASS (HTTP 200)
GET /v1/events/{bridgeId}: PASS (HTTP 200)
/health/live: PASS (HTTP 200)
/health/ready: PASS (HTTP 200)
/api/status: PASS (HTTP 200)
/api/messages/recent: PASS (HTTP 200)
/api/messages/{bridgeId}: PASS (HTTP 200)
/api/events/window: PASS (HTTP 200)
/api/events/{bridgeId}: PASS (HTTP 200)
