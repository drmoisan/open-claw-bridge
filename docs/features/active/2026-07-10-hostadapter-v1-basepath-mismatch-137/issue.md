# hostadapter-v1-basepath-mismatch (Issue #137)

- Date captured: 2026-07-10
- Author: drmoisan
- Status: Promoted -> docs/features/active/hostadapter-v1-basepath-mismatch/ (Issue #137)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #137
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/137
- Last Updated: 2026-07-10
- Work Mode: full-bug

## Summary

Every configured default for `OpenClaw__HostAdapter__BaseUrl` (repo `.env`, `.env.example`, both `docker-compose*.yml` files, `OpenClaw.Core`'s `Program.cs` fallback, and `Install.Preflight.psm1`'s hardcoded default) appends a `/v1` path segment, but `OpenClaw.HostAdapter`'s `Program.cs` has never mapped any route under a `/v1` prefix — it serves `/status`, `/users/{id}/messages`, etc. at the root. This breaks the scripted installer's HostAdapter preflight check with a 404, and would equally break `OpenClaw.Core`'s real runtime calls to HostAdapter once the Docker container is up, since `HostAdapterHttpClient.GetStatusAsync` requests the relative path `"status"` against that same `/v1`-suffixed base address.

## Environment

- OS/version: Windows 11 Pro
- .NET version: 10.0.201 (per `global.json`)
- Command/flags used: `.\Install.ps1 -DockerEnvFilePath (Join-Path $operatorConfig '.env') -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')` from a published bundle (e.g. `artifacts\publish\1.0.2.2\`)
- Data source or fixture: operator `.env` copied verbatim from the bundle's `docker\.env.example`, which mirrors the repo-root `.env.example`

## Steps to Reproduce

1. Publish and install per the README's Step 1-3 flow (`scripts/Publish.ps1`, then `Install.ps1` with `-DockerEnvFilePath`/`-AnthropicEnvFilePath`) with an operator `.env` whose `OpenClaw__HostAdapter__BaseUrl` is left at its default value.
2. `Install.ps1` starts `OpenClaw.HostAdapter` (confirmed listening on `http://127.0.0.1:4319`) and then runs its HostAdapter preflight probe before starting the Docker stage.
3. The preflight probe requests `GET http://127.0.0.1:4319/v1/status`.

## Expected Behavior

The preflight probe requests a URL that HostAdapter actually serves (`/status`), receives a 200/valid envelope, and the installer proceeds to the Docker stage.

## Actual Behavior

`GET http://127.0.0.1:4319/v1/status` returns HTTP 404 (confirmed in the HostAdapter's own request log: `HostAdapter request ... /v1/status completed with 404`). `Install.ps1` throws: "HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 404. ..." and the install stops before the Docker stage.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet:
  ```
  info: OpenClaw.HostAdapter.RequestLoggingMiddleware[0]
        HostAdapter request c3f7364e-bcd4-4eb2-9d9b-51de56a32048 /v1/status completed with 404 in 2 ms. BridgeState=unknown; BridgeErrorCode=none; CliExitCode=(null)
  Exception: HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 404. Confirm
  OpenClaw.HostAdapter is running, the token is valid, and OpenClaw.MailBridge is running, then retry; or pass
  -SkipDocker to skip the container stage.
  ```

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Blocks the entire scripted install (Step 3) for any operator who has not manually edited the operator `.env` to remove the stray `/v1` segment; also affects the live Docker stage's real HostAdapter calls, not just the installer.

## Suspected Cause / Notes

`src/OpenClaw.HostAdapter/Program.cs` has never registered a `/v1`-prefixed route (confirmed via `git log -S'"/v1'` against the file: no match, no controllers, purely minimal-API `app.MapGet` calls at root scope: `/status`, `/users/{id}/messages`, and others). The `/v1` segment is baked into six defaults instead:

- `.env:1` and `.env.example:3` — `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319/v1`
- `docker-compose.yml:27,73` and `docker-compose.dev.yml:14` — `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}`
- `src/OpenClaw.Core/Program.cs:17` — hardcoded `"http://host.docker.internal:4319/v1/"` fallback when config is blank (note: `CoreOptions.cs:16`'s class-level default, `"http://host.docker.internal:4319/"`, is already correct with no `/v1` — only the `Program.cs` fallback string is wrong)
- `scripts/Install.Preflight.psm1:73` — `$baseUrl = 'http://host.docker.internal:4319/v1'` (default when the operator `.env` map lacks the key)

`tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` uses a `BaseAddress` of `http://localhost:4319/` (no `/v1`) and asserts the captured path ends with `/status` — this is the tested, intended contract, confirming the `/v1` segment in the six defaults above is the stray value to remove, not a missing route to add to HostAdapter.

## Proposed Fix / Validation Ideas

- [ ] Strip the stray `/v1` segment from all six locations listed above so every default resolves to HostAdapter's actual root-level route surface.
- [ ] Unit coverage: extend `Install.Preflight.Tests.ps1`/`Install.Tests.ps1` to assert the default preflight URL has no `/v1` segment; confirm `HostAdapterHttpClientTests.cs` continues to pass unchanged (it already reflects the corrected contract).
- [ ] Integration/manual verification: publish a fresh bundle and run the full `Install.ps1` flow end-to-end (through the Docker stage, not just `-SkipDocker`) with an operator `.env` left at its defaults, confirming the preflight probe succeeds and the Docker stack starts.

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch
