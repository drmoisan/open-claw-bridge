<!-- markdownlint-disable-file -->

# Task Research Notes: external OpenClaw agent integration on the existing Docker system

## Research Executed

### File Analysis

- `README.md`
  - Confirms the current supported Docker deployment is `OpenClaw.Core` in Docker Desktop, with `OpenClaw.HostAdapter` running on the Windows host and `OpenClaw.MailBridge.Client` remaining the canonical transport fallback.
- `docs/api-reference.md`
  - Confirms the current approved transport seam is Windows named-pipe RPC locally and authenticated HTTP through `OpenClaw.HostAdapter` for the container path; the six read-only operations are canonical.
- `docs/mailbridge-runbook.md`
  - Confirms the Windows host remains the only Outlook/COM boundary, `OpenClaw.HostAdapter` is the only approved network seam, and the containerized path depends on `host.docker.internal`, a bearer token file, and loopback-only port exposure.
- `docs/architecture-diagrams.md`
  - Confirms the current additive topology: Outlook -> `OpenClaw.MailBridge` -> `OpenClaw.MailBridge.Client` -> `OpenClaw.HostAdapter` on Windows, then `OpenClaw.Core` in Docker calling `HostAdapter` through `host.docker.internal`.
- `docker-compose.yml`
  - Confirms the production container shape is already defined: service `openclaw-core`, loopback-only publish `127.0.0.1:${OPENCLAW_HTTP_PORT:-8080}:8080`, read-only root filesystem, non-root user, named `/data` volume, and read-only HostAdapter token-file bind mount.
- `docker-compose.dev.yml`
  - Confirms the development compose topology already uses `host.docker.internal:host-gateway` and a token bind mount, which is the correct pattern for any additional Dockerized consumer of `HostAdapter`.
- `.env.example`
  - Confirms the current container-side configuration contract already exposes `OpenClaw__HostAdapter__BaseUrl`, `OpenClaw__HostAdapter__TokenFile`, `OPENCLAW_HTTP_PORT`, and `HOSTADAPTER_TOKEN_FILE`.
- `deploy/docker/openclaw-core.Dockerfile`
  - Confirms the current Docker runtime is a Linux ASP.NET Core container on .NET 10 with a non-root user, dedicated `/data`, and health-check scripts.
- `src/OpenClaw.HostAdapter/Program.cs`
  - Confirms all six HTTP endpoints are implemented now and already depend on Windows-host execution of `OpenClaw.MailBridge.Client` rather than direct pipe access from the container.
- `src/OpenClaw.HostAdapter/BearerTokenMiddleware.cs`
  - Confirms HostAdapter already enforces bearer-token authentication and returns normalized `401` errors without exposing token values.
- `src/OpenClaw.HostAdapter/HostAdapterCommandBuilder.cs`
  - Confirms HostAdapter shells out only to the existing CLI verbs and is the correct current boundary instead of direct named-pipe calls from Docker.
- `src/OpenClaw.Core/Program.cs`
  - Confirms `OpenClaw.Core` already defaults to `http://host.docker.internal:4319/v1/`, persists `/data/openclaw.db`, and exposes `/health/live`, `/health/ready`, `/api/status`, `/api/messages/recent`, `/api/messages/{bridgeId}`, `/api/events/window`, and `/api/events/{bridgeId}`.
- `src/OpenClaw.Core/HostAdapterHttpClient.cs`
  - Confirms the current Dockerized application already consumes the Windows HostAdapter over HTTP with bearer-token auth.
- `src/OpenClaw.MailBridge/PipeRpcWorker.cs`
  - Confirms the named pipe ACL grants `LocalSystem`, Administrators, the current interactive user, and `openclaw-svc`, while denying `NETWORK`; this remains a Windows-local transport and is not a container-facing seam.
- `OpenClaw.MailBridge.sln`
  - Confirms the HostAdapter/Core implementation is already part of the main solution; the Docker system is not a prototype anymore.
- `artifacts\research\20260412-open-claw-docker-research.md`
  - Confirms the earlier research correctly selected the additive Windows-host-plus-Docker topology and documented the approved HostAdapter boundary.
- `docs/features/active/2026-04-11-open-claw-docker-23/spec.md`
  - Confirms the active feature explicitly defines the Docker model as additive, local-only, and dependent on `host.docker.internal` plus token auth, not as a replacement for the Windows bridge.

### Code Search Results

- `openclaw-svc|PipeSecurity|NetworkSid`
  - Matches in `src/OpenClaw.MailBridge/PipeRpcWorker.cs`, `docs/api-reference.md`, and archived evidence confirm `openclaw-svc` is a Windows named-pipe ACL concern, not a container requirement.
- `host.docker.internal|HOSTADAPTER_TOKEN_FILE|OPENCLAW_HTTP_PORT`
  - Matches across `.env.example`, `docker-compose.yml`, `docker-compose.dev.yml`, `README.md`, `docs/mailbridge-runbook.md`, `docs/api-reference.md`, and `src/OpenClaw.Core/*` confirm the current Docker stack already standardizes on Windows-host HTTP access through `host.docker.internal` and loopback-only local publishing.
- `GET /v1/status|GET /v1/messages|GET /v1/calendar|GET /v1/events`
  - Matches in `docs/api-reference.md`, `docs/features/active/2026-04-11-open-claw-docker-23/spec.md`, and `src/OpenClaw.HostAdapter/Program.cs` confirm the HostAdapter HTTP contract is implemented and stable.
- `admin-assistant|openclaw onboard|agents add|skills list|exec approvals|whatsapp`
  - No repository implementation matches were found. These concepts appear only in the user-supplied prompt, not in the current repo.

### External Research

- #fetch:https://docs.openclaw.ai/install
  - Verified limitation: live extraction from `docs.openclaw.ai` failed in this environment, and direct HTTPS fetch attempts from the terminal timed out. The exact external OpenClaw install/onboard command surface could not be revalidated live.
- #fetch:https://docs.openclaw.ai/gateway
  - Verified limitation: live extraction from `docs.openclaw.ai` failed in this environment, and direct HTTPS fetch attempts from the terminal timed out. Gateway-specific configuration claims from the prompt remain unverified here.
- #fetch:https://docs.openclaw.ai/cli/agents
  - Verified limitation: live extraction from `docs.openclaw.ai` failed in this environment, and direct HTTPS fetch attempts from the terminal timed out. Agent workspace/skill command details from the prompt remain prompt-grounded only.
- #fetch:https://learn.microsoft.com/en-us/dotnet/standard/frameworks
  - Prior repo research already verified that cross-platform application layers should target the base TFM while Windows-only Outlook/pipe layers remain Windows-specific; this supports keeping any Dockerized integration on the cross-platform HTTP side.
- #fetch:https://docs.docker.com/desktop/features/networking/networking-how-tos/
  - Prior repo research already verified `host.docker.internal` is the Docker Desktop hostname for reaching host services from containers.
- #fetch:https://docs.docker.com/engine/network/port-publishing/
  - Prior repo research already verified loopback-only publishing is the correct control for a local-only UI/service exposure model.

### Project Conventions

- Standards referenced: `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/csharp-code-change.instructions.md`, `.github/instructions/csharp-unit-test.instructions.md`, `.github/instructions/tonality.instructions.md`, `AGENTS.md`, `README.md`, `docs/api-reference.md`, and `docs/mailbridge-runbook.md`.
- Instructions followed: research-only work in `artifacts/research/`, evidence-first findings, no source changes, and removal of speculative alternatives that are contradicted by the current repo topology.

## Key Discoveries

### Project Structure

The repository already implements the Docker system that the user wants to preserve:

- Windows host:
  - `OpenClaw.MailBridge` owns Outlook COM, the SQLite bridge cache, and the named-pipe server.
  - `OpenClaw.MailBridge.Client` is the canonical six-command transport adapter.
  - `OpenClaw.HostAdapter` is the approved HTTP bridge boundary and already exposes authenticated `GET /v1/*` routes.
- Docker Desktop:
  - `OpenClaw.Core` is already deployed as a Linux container and already consumes the Windows HostAdapter through `host.docker.internal`.

This means the prompt’s proposal to have an OpenClaw assistant call `OpenClaw.MailBridge.Client.exe` directly is not compatible with the repo’s current Docker architecture. Inside the Docker system, the approved seam is already `HostAdapter`, not the Windows executable path and not the named pipe.

The repository also uses the name `OpenClaw.Core` for its local-only Docker application. That name is separate from the external OpenClaw gateway/agent platform described in the prompt. Any future Dockerized external OpenClaw runtime must therefore be added as a separate service name and volume/config boundary to avoid conflating the repo’s `OpenClaw.Core` application with the external platform runtime.

### Implementation Patterns

The current repo establishes four non-negotiable integration patterns that drive the adaptation:

1. **Windows-only Outlook boundary**
   - Outlook COM and named-pipe access stay on the Windows host in the interactive user session.
   - The container must not attempt direct Outlook COM, direct named-pipe access, or direct execution of `OpenClaw.MailBridge.Client.exe` from a Linux runtime.

2. **HostAdapter as the only approved container-facing network seam**
   - `OpenClaw.HostAdapter` already authenticates requests with a bearer token, validates UTC timestamps and limits, maps CLI failures into normalized HTTP responses, and shells out to the client on the Windows host.
   - `OpenClaw.Core` already uses this seam exactly as intended.

3. **Docker Desktop local-only runtime posture**
   - The current compose files already enforce loopback-only publishing, non-root execution, read-only root filesystem, read-only token-file bind mounts, and `host.docker.internal` host reachability.
   - Any external OpenClaw container added to this system should reuse the same posture.

4. **Read-only MailBridge capability today**
   - The current canonical API remains read-only: status, message list/detail, meeting-request list, calendar-window list, and event detail.
   - The assistant scope therefore remains triage, summarization, scheduling analysis, and draft generation only.

### Complete Examples

```csharp
// Source: src/OpenClaw.Core/HostAdapterHttpClient.cs
public Task<ApiEnvelope<ItemsResponse<MessageDto>>> ListMessagesAsync(
    DateTimeOffset sinceUtc,
    int limit = 100,
    string? requestId = null,
    CancellationToken cancellationToken = default
)
{
    return SendAsync<ItemsResponse<MessageDto>>(
        $"messages?since={Uri.EscapeDataString(sinceUtc.ToString("O"))}&limit={limit}",
        requestId,
        cancellationToken
    );
}

// Source: src/OpenClaw.Core/Program.cs
builder
    .Services.AddHttpClient<IHostAdapterClient, HostAdapterHttpClient>(
        (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenClawOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.HostAdapter.BaseUrl));
        }
    );
```

This is the most important verified adaptation point: the existing Dockerized code already proves that the correct container-side contract is HTTP to `HostAdapter`, not direct process execution of `OpenClaw.MailBridge.Client.exe`.

### API and Schema Documentation

Current repo-verified HTTP contract that a Dockerized external OpenClaw assistant should consume:

- `GET /v1/status`
- `GET /v1/messages?since=<utc>&limit=<n>`
- `GET /v1/messages/{bridgeId}`
- `GET /v1/meeting-requests?since=<utc>&limit=<n>`
- `GET /v1/calendar?start=<utc>&end=<utc>&limit=<n>`
- `GET /v1/events/{bridgeId}`

Current repo-verified container/runtime contract:

- `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319/v1`
- `OpenClaw__HostAdapter__TokenFile=/run/openclaw/hostadapter.token`
- `HOSTADAPTER_TOKEN_FILE=C:\ProgramData\OpenClaw\HostAdapter\adapter.token`
- `OPENCLAW_HTTP_PORT=<loopback-only published UI/API port>`

Prompt items that are **not** repo-verified and could not be externally revalidated in this environment:

- exact `openclaw onboard` command shape
- exact `openclaw agents add` command shape
- exact `openclaw approvals allowlist add` command shape
- exact skill frontmatter requirements for the external OpenClaw runtime
- exact external gateway config schema and field names

### Configuration Examples

```yaml
# Source: docker-compose.yml
services:
  openclaw-core:
    environment:
      OpenClaw__HostAdapter__BaseUrl: ${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}
      OpenClaw__HostAdapter__TokenFile: /run/openclaw/hostadapter.token
    ports:
      - "127.0.0.1:${OPENCLAW_HTTP_PORT:-8080}:8080"
    volumes:
      - openclaw_data:/data
      - type: bind
        source: ${HOSTADAPTER_TOKEN_FILE}
        target: /run/openclaw/hostadapter.token
        read_only: true
```

This is the exact pattern that any additional external OpenClaw container should follow for HostAdapter access on this repo.

### Technical Requirements

- The existing Docker system must remain intact:
  - keep `OpenClaw.Core` as the current repo-owned local UI/cache container;
  - keep `OpenClaw.HostAdapter` on the Windows host;
  - keep Outlook COM and the named pipe on the Windows host only.
- If the external OpenClaw platform is added to this repo’s deployment, it should be added as a **separate Docker service**, not as a replacement for `openclaw-core` and not by repurposing the existing `OpenClaw.Core` image.
- The prompt’s direct `exec` path to `OpenClaw.MailBridge.Client.exe` must be adapted for this repo:
  - **do not** have the Dockerized assistant call the Windows executable path directly;
  - **do** have it call `OpenClaw.HostAdapter` over HTTP using the mounted bearer token file.
- The prompt’s skill/tool instructions can be reused conceptually, but the command examples must be rewritten from CLI-client commands to HostAdapter HTTP calls.
- The prompt’s dedicated `admin-assistant` agent/workspace concept can be reused, but its storage path should be adapted from a Windows user profile path to a Docker volume or bind-mounted workspace path.
- The prompt’s conservative assistant behavior is compatible with the repo and can be reused as-is in substance:
  - read-only behavior only;
  - safe-mode-first;
  - explicit redaction caveats;
  - no claims of writes, replies, or calendar mutation.
- The prompt’s `openclaw-svc` runtime-account guidance needs adaptation:
  - `openclaw-svc` remains meaningful on the Windows host for named-pipe ACLs and bridge-side host concerns;
  - it is not the correct primary integration boundary for a Dockerized assistant that consumes `HostAdapter` over HTTP.

**Mandatory unachievable objective callout**:
- **Live verification of the external OpenClaw platform documentation was not achievable from this environment. Both the webpage fetcher and direct HTTPS fetches to `docs.openclaw.ai` failed. Therefore, exact external OpenClaw CLI/config/plugin commands from the prompt must be revalidated against the official docs before implementation.**

## Recommended Approach

Use the repo’s **existing HostAdapter-centered Docker topology** and adapt the prompt to that topology instead of adapting the repo back to the prompt’s native-Windows `exec` model.

### What can be used exactly as-is from the prompt

These prompt elements align with the repo and can be reused with minimal or no semantic change:

- The dedicated assistant persona and workspace concept (`admin-assistant`).
- The day-1 read-only scope: triage, scheduling analysis, summarization, and draft generation only.
- The conservative instruction model:
  - do not fabricate details;
  - respect redaction;
  - do not claim writes occurred;
  - surface unknowns and human-approval points explicitly.
- The recommendation to keep MailBridge in `safe` mode first.
- The idea of a future typed-tool/plugin integration as the cleaner long-term path.

### What must be adapted for this repo

1. **Deployment location**
   - Prompt: external OpenClaw runtime installed natively on Windows.
   - Repo adaptation: deploy the external OpenClaw runtime as a **separate Docker service** on the existing Docker Desktop system.

2. **Bridge access method**
   - Prompt: assistant uses `exec` to call `OpenClaw.MailBridge.Client.exe` directly.
   - Repo adaptation: assistant must call `OpenClaw.HostAdapter` over HTTP using the existing bearer-token contract.

3. **Tool/skill examples**
   - Prompt: `status`, `list-messages`, `list-calendar`, `get-message`, `get-event` command lines against the Windows executable.
   - Repo adaptation: replace those examples with authenticated HTTP requests to `GET /v1/status`, `GET /v1/messages`, `GET /v1/meeting-requests`, `GET /v1/calendar`, `GET /v1/messages/{bridgeId}`, and `GET /v1/events/{bridgeId}`.

4. **Identity and storage paths**
   - Prompt: Windows account `openclaw-svc` and Windows workspace paths under `C:/Users/openclaw-svc/.openclaw/...`.
   - Repo adaptation: use Docker volume or bind-mounted workspace/config paths for the external OpenClaw container; keep `openclaw-svc` only where Windows-host ACLs or bridge-side service operations require it.

5. **Operational topology**
   - Prompt: OpenClaw assistant reaches MailBridge directly.
   - Repo adaptation: OpenClaw assistant should sit **beside** `openclaw-core` as another consumer of the Windows `HostAdapter`, not below the named pipe.

### Selected target topology

The recommended deployment shape for this repo is:

1. Windows interactive user runs `OpenClaw.MailBridge`.
2. Windows host runs `OpenClaw.HostAdapter` on `127.0.0.1:4319` with the existing token file.
3. Docker Desktop runs:
   - existing `openclaw-core` service for the repo’s UI/cache path;
   - new external OpenClaw service for the assistant/gateway path.
4. Both Dockerized consumers call the same Windows HostAdapter through `host.docker.internal` using mounted token-file access.

### Rejected alternatives

- **Use the prompt exactly as written with direct `exec` to `OpenClaw.MailBridge.Client.exe` from Docker**
  - Rejected because the repo already defines `HostAdapter` as the approved container-facing seam and the Docker runtime is Linux-based, not a Windows process host.
- **Replace the repo’s existing `openclaw-core` container with the external OpenClaw runtime**
  - Rejected because `OpenClaw.Core` is already implemented, tested, documented, and serves a different purpose from the external assistant runtime.
- **Have the Dockerized external OpenClaw runtime call the named pipe directly**
  - Rejected because the named pipe is Windows-local, the repo already provides `HostAdapter` as the approved network seam, and direct pipe access would bypass the repo’s validated auth/validation/error-mapping layer.

## Implementation Guidance

- **Objectives**:
  - preserve the current Windows `MailBridge` + `HostAdapter` + Docker `OpenClaw.Core` design;
  - add the external OpenClaw assistant runtime as an additional Dockerized consumer of `HostAdapter`;
  - adapt the prompt’s skill/tool strategy to HTTP-based access rather than Windows executable access.
- **Key Tasks**:
  - define a second compose service for the external OpenClaw runtime instead of altering `openclaw-core`;
  - mount the existing HostAdapter token file read-only into that new service;
  - configure the new service to use `http://host.docker.internal:4319/v1` as its MailBridge data source;
  - translate the prompt’s `TOOLS.md` and skill instructions from CLI verbs into HostAdapter HTTP operations;
  - keep the assistant behavior read-only and safe-mode-aware;
  - document the naming distinction between repo `OpenClaw.Core` and the external OpenClaw runtime to avoid operator confusion.
- **Dependencies**:
  - working Windows `OpenClaw.MailBridge` installation;
  - running Windows `OpenClaw.HostAdapter` with a valid token file;
  - Docker Desktop and the existing `host.docker.internal` path;
  - external OpenClaw image/install documentation revalidated outside this environment before implementation.
- **Success Criteria**:
  - the current repo Docker stack remains unchanged and still functional;
  - the external OpenClaw runtime is introduced as a separate local-only Docker service;
  - the assistant consumes MailBridge data only through `HostAdapter` HTTP endpoints;
  - no container attempts direct use of the Windows executable or named pipe;
  - the assistant instructions clearly preserve read-only behavior, redaction handling, and no-write claims.