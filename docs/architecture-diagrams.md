# OpenClaw MailBridge — Architecture Diagrams

The Mermaid diagrams in this document set a 14px font baseline, which is approximately 10.5pt, in renderers that honor Mermaid init directives. Extremely wide diagrams may still be scaled down by the Markdown host, so the widest diagrams also use shorter labels or more vertical layouts to improve readability.

## 0. Additive Deployment Topology

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}}}%%
graph TB
    subgraph WindowsHost["Windows host"]
        Outlook["Outlook COM"]
        Bridge["OpenClaw.MailBridge<br/>STA scanner + named-pipe RPC"]
        BridgeDb["%LOCALAPPDATA%\\OpenClaw\\MailBridge\\cache.db"]
        Client["OpenClaw.MailBridge.Client<br/>six read-only verbs"]
        HostAdapter["OpenClaw.HostAdapter<br/>ASP.NET Core Minimal API"]
        AdapterConfig["%ProgramData%\\OpenClaw\\HostAdapter\\appsettings.json"]
        TokenFile["token file<br/>%ProgramData%\\OpenClaw\\HostAdapter\\adapter.token"]
    end

    subgraph DockerDesktop["Docker Desktop local-only path"]
        Core["OpenClaw.Core<br/>UI + internal API + pollers"]
        CoreDb["/data/openclaw.db"]
        TokenMount["/run/openclaw/hostadapter.token<br/>read-only bind mount"]
        Healthcheck["healthcheck.sh<br/>GET /health/live"]
        Agent["openclaw-agent<br/>external assistant runtime<br/>127.0.0.1:8181"]
        AgentTokenMount["/run/openclaw/hostadapter.token<br/>read-only bind mount (agent)"]
    end

    Browser["Local browser / operator<br/>http://127.0.0.1:8080"]

    Outlook --> Bridge
    Bridge --> BridgeDb
    Client -->|"named pipe / JSON RPC"| Bridge
    HostAdapter -->|"shell-out via ArgumentList"| Client
    AdapterConfig --> HostAdapter
    TokenFile --> HostAdapter
    TokenFile --> TokenMount
    TokenFile --> AgentTokenMount
    Core -->|"GET /v1/* via host.docker.internal"| HostAdapter
    Core --> CoreDb
    TokenMount --> Core
    Healthcheck --> Core
    Browser -->|"127.0.0.1 only"| Core
    Agent -->|"GET /v1/* via host.docker.internal"| HostAdapter
    AgentTokenMount --> Agent
    Browser -->|"127.0.0.1:8181"| Agent
```

This topology includes two container services that independently consume the HostAdapter HTTP API. `OpenClaw.Core` is the repository-owned UI and cache container. `openclaw-agent` is the external OpenClaw assistant runtime for AI-powered triage and summarization. Both run as non-root containers with read-only root filesystems, loopback-only port publishing, and read-only token-file bind mounts. The current Windows path remains available as the fallback to `OpenClaw.MailBridge.Client`.

## 1. Existing Bridge Runtime

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}}}%%
graph TB
    Outlook["Outlook<br/>(COM Automation)"]
    Bridge["MailBridge Service<br/>(Background Host)"]
    SQLite["SQLite Cache<br/>(cache.db)"]
    Client["MailBridge Client<br/>(CLI)"]

    Outlook -- "COM / MAPI<br/>(STA thread)" --> Bridge
    Bridge -- "Read/Write" --> SQLite
    Client -- "Named Pipe<br/>(JSON RPC)" --> Bridge
    Bridge -- "JSON Response" --> Client
```

## 2. Deployed Component Architecture

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}}}%%
graph TB
    subgraph WindowsHost["Windows host"]
        subgraph BridgeHost["OpenClaw.MailBridge"]
            SW["ScanWorker"]
            PRW["PipeRpcWorker"]
            STA["OutlookStaExecutor"]
            OS["OutlookScanner"]
            CR["CacheRepository"]
            BSS["BridgeStateStore"]
            RS["ResponseShaper"]
        end

        subgraph Adapter["OpenClaw.HostAdapter"]
            Log["RequestLoggingMiddleware"]
            Auth["BearerTokenMiddleware<br/>Bearer token + X-Request-Id"]
            Validate["HostAdapterRequestValidation<br/>UTC, limit, bridgeId, window"]
            StatusCache["StatusCacheService<br/>5 second TTL"]
            Build["HostAdapterCommandBuilder<br/>allowlisted ArgumentList"]
            Run["HostAdapterProcessRunner"]
            Map["HostAdapterResponseMapper<br/>401 / 404 / 409 / 502 / 503"]
        end
    end

    subgraph Container["OpenClaw.Core container"]
        subgraph CoreApp["OpenClaw.Core"]
            Http["HostAdapterHttpClient<br/>Bearer token + X-Request-Id"]
            MPW["MessagePollingWorker"]
            CPW["CalendarPollingWorker"]
            Repo["CoreCacheRepository"]
            Health["CoreHealthState"]
            UI["Razor Pages UI<br/>/"]
            API["/health/* + /api/*"]
        end
    end

    Outlook["Outlook COM"]
    Browser["Local browser"]
    Token["adapter.token"]
    BridgeDb[("Bridge SQLite")]
    CoreDb[("Core SQLite")]

    SW --> STA
    STA --> OS
    OS --> Outlook
    OS --> CR
    OS --> BSS
    PRW --> CR
    PRW --> RS
    PRW --> BSS
    CR --> BridgeDb

    Log --> Auth
    Auth --> Validate
    Auth --> StatusCache
    Validate --> Build
    Build --> Run
    Run --> Client["OpenClaw.MailBridge.Client"]
    Run --> Map
    StatusCache --> Run
    Client --> PRW
    Token --> Auth

    MPW --> Http
    CPW --> Http
    Http --> Log
    MPW --> Repo
    CPW --> Repo
    MPW --> Health
    CPW --> Health
    UI --> Repo
    UI --> Health
    API --> Repo
    API --> Health
    Repo --> CoreDb
    Browser --> UI
    Browser --> API
    Token --> Http
```

## 3. HostAdapter Request Path

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}, "sequence": {"actorFontSize": 14, "messageFontSize": 14, "noteFontSize": 14}}}%%
sequenceDiagram
    participant Caller as Core / operator
    participant Log as Logging
    participant Auth as Auth
    participant Token as adapter.token
    participant Cache as Status cache
    participant Validate as Validation
    participant Build as Command builder
    participant Run as Process runner
    participant Client as MailBridge.Client
    participant Pipe as Pipe RPC
    participant Repo as Cache repo
    participant Shape as Response shaper
    participant Map as Response mapper

    Caller->>Log: GET /v1/*
    Log->>Auth: invoke
    Auth->>Token: read expected token
    alt missing or invalid token
        Auth-->>Caller: 401 or 503 ApiEnvelope error
    else token accepted
        Auth->>Cache: GetStatusAsync(requestId)
        alt cache miss
            Cache->>Run: execute status
            Run->>Client: status
            Client->>Pipe: JSON RPC
            Pipe->>Repo: read status snapshot
            Pipe-->>Client: RpcResponse
            Run-->>Cache: BridgeStatusDto
        else cache hit within 5 seconds
            Cache-->>Auth: cached BridgeStatusDto
        end

        alt route = /v1/status
            Auth-->>Caller: 200 ApiEnvelope<BridgeStatusDto>
        else bridge state is starting or waiting_for_outlook
            Auth-->>Caller: 409 BRIDGE_NOT_READY
        else data route
            Auth->>Validate: validate UTC timestamps, window, limit, bridgeId
            Validate->>Build: choose allowlisted CLI verb
            Build->>Run: ProcessStartInfo.ArgumentList
            Run->>Client: list-messages / get-message / list-meeting-requests / list-calendar / get-event
            Client->>Pipe: JSON RPC over named pipe
            Pipe->>Repo: read cached rows
            Pipe->>Shape: safe or enhanced shaping
            Pipe-->>Client: RpcResponse
            Run->>Map: map exit code and bridge error
            Map-->>Caller: ApiEnvelope<T> with meta.requestId, meta.adapterVersion, meta.bridge
        end
    end
```

The HostAdapter is intentionally narrow. It adds token authentication, request correlation, deterministic validation, short-lived status caching, HTTP envelope metadata, and CLI-to-HTTP error mapping without introducing direct container access to the named pipe.

## 4. Core Polling, Persistence, and Degraded Reads

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}, "sequence": {"actorFontSize": 14, "messageFontSize": 14, "noteFontSize": 14}}}%%
sequenceDiagram
    participant MPW as Message poller
    participant CPW as Calendar poller
    participant Http as Adapter client
    participant HA as HostAdapter
    participant Repo as Core cache
    participant Health as Health state

    loop every messagesInterval / meetingRequestsInterval
        MPW->>Repo: GetCursor(messages_since_utc)
        MPW->>Http: ListMessagesAsync(since, limit)
        Http->>HA: GET /v1/messages
        HA-->>Http: ApiEnvelope<ItemsResponse<MessageDto>>
        MPW->>Http: ListMeetingRequestsAsync(since, limit)
        Http->>HA: GET /v1/meeting-requests
        HA-->>Http: ApiEnvelope<ItemsResponse<MessageDto>>
        alt HostAdapter success
            MPW->>Repo: UpsertBridgeStatusSnapshot
            MPW->>Repo: UpsertMessages
            MPW->>Repo: SetCursor(messages_since_utc / meeting_requests_since_utc)
            MPW->>Repo: AddIngestRun(success)
            MPW->>Health: MarkPollSuccess
        else HostAdapter failure or stale bridge
            MPW->>Repo: UpsertBridgeStatusSnapshot if available
            MPW->>Repo: AddIngestRun(failed)
            MPW->>Health: MarkPollFailure
        end
    end

    loop every calendarInterval
        CPW->>Http: ListCalendarWindowAsync(start, end, limit)
        Http->>HA: GET /v1/calendar
        HA-->>Http: ApiEnvelope<ItemsResponse<EventDto>>
        alt HostAdapter success
            CPW->>Repo: UpsertBridgeStatusSnapshot
            CPW->>Repo: UpsertEvents
            CPW->>Repo: SetCursor(calendar_window_last_run_utc)
            CPW->>Repo: AddIngestRun(success)
            CPW->>Health: MarkPollSuccess
        else HostAdapter failure or stale bridge
            CPW->>Repo: UpsertBridgeStatusSnapshot if available
            CPW->>Repo: AddIngestRun(failed)
            CPW->>Health: MarkPollFailure
        end
    end
```

When the bridge becomes unavailable, `OpenClaw.Core` keeps its SQLite cache and continues serving cached reads. The degraded condition is surfaced through `CoreHealthState`, `/health/ready`, `/api/status`, and the UI instead of being hidden behind retries.

## 5. Core Cached-Read UI and API Flow

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}, "sequence": {"actorFontSize": 14, "messageFontSize": 14, "noteFontSize": 14}}}%%
sequenceDiagram
    participant Browser as Browser / user
    participant UI as UI or /api/*
    participant Repo as Core cache
    participant Health as Health state

    Browser->>UI: GET /, /api/status, /api/messages/recent, /api/events/window
    UI->>Health: read readiness + latest bridge snapshot
    UI->>Repo: query cached messages, meeting requests, events, counts
    Repo-->>UI: SQLite-backed rows + counts
    Health-->>UI: database status, adapter reachability, poll timestamps, stale flags
    UI-->>Browser: cached response with freshness and redaction indicators
```

The Core read path is cache-backed by design. UI and internal API requests do not fan out into live bridge calls. The page at `/` shows recent mail, meeting requests, and events with stale and redacted badges, while `/api/status` returns cache counts, bridge freshness, and failure timestamps.

## 6. Bridge State Machine

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}}}%%
stateDiagram-v2
    [*] --> starting: Host starts

    starting --> waiting_for_outlook: Outlook not found<br/>& autostart disabled
    starting --> ready: First scan succeeds

    waiting_for_outlook --> ready: Outlook becomes<br/>available and scan succeeds
    waiting_for_outlook --> error: Unrecoverable failure

    ready --> degraded: Scan fails or Outlook disconnects
    ready --> ready: Scan succeeds

    degraded --> ready: Scan succeeds again
    degraded --> waiting_for_outlook: Outlook lost
    degraded --> error: Unrecoverable failure

    error --> [*]: Host shuts down
    ready --> [*]: Host shuts down
    degraded --> [*]: Host shuts down
    waiting_for_outlook --> [*]: Host shuts down
```

`starting` and `waiting_for_outlook` are treated as bridge-not-ready states by the HostAdapter and return `409`. `degraded` remains readable for cached data and is propagated end-to-end through HostAdapter metadata and Core health/status surfaces.

## 7. Safe vs Enhanced Response Shaping

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}}}%%
flowchart TD
    REQ["RPC payload ready"]
    MODE{{"bridge mode?"}}
    TYPE{{"DTO type?"}}
    TYPE2{{"DTO type?"}}

    SAFE_MSG["MessageDto safe mode<br/>BodyPreview = null<br/>SenderName = null<br/>SenderEmail = null<br/>IsRedacted = true"]
    SAFE_EVT["EventDto safe mode<br/>BodyPreview = null<br/>IsRedacted = true"]
    ENH_MSG["MessageDto enhanced mode<br/>Sanitized BodyPreview<br/>SenderName<br/>SenderEmail<br/>IsRedacted = false"]
    ENH_EVT["EventDto enhanced mode<br/>Sanitized BodyPreview<br/>IsRedacted = false"]
    OUT["cached result returned to client / HostAdapter / Core"]

    REQ --> MODE
    MODE -- "safe" --> TYPE
    MODE -- "enhanced" --> TYPE2
    TYPE -- "MessageDto" --> SAFE_MSG
    TYPE -- "EventDto" --> SAFE_EVT
    TYPE2 -- "MessageDto" --> ENH_MSG
    TYPE2 -- "EventDto" --> ENH_EVT
    SAFE_MSG --> OUT
    SAFE_EVT --> OUT
    ENH_MSG --> OUT
    ENH_EVT --> OUT
```

The privacy boundary still belongs to the Windows bridge. HostAdapter and Core surface `isRedacted`, `protectedFieldsAvailable`, bridge mode, and stale-cache state, but they do not attempt to reconstruct redacted fields.

## 8. SQLite Data Models

### 8.1 Bridge Cache

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}, "er": {"fontSize": 14}}}%%
erDiagram
    messages {
        text bridge_id PK
        text entry_id
        text store_id
        text item_kind
        text subject
        text sender_name
        text sender_email
        text received_utc
        text sent_utc
        int importance
        int sensitivity
        int unread
        int has_attachments
        text message_class
        text to_json
        text cc_json
        text body_preview
        int protected_fields_available
        int is_redacted
        text last_seen_utc
    }

    events {
        text bridge_id PK
        text global_appointment_id
        text subject
        text start_utc
        text end_utc
        text location
        int busy_status
        int meeting_status
        int is_recurring
        int sensitivity
        text organizer
        text required_attendees_json
        text optional_attendees_json
        text resources_json
        text body_preview
        int protected_fields_available
        int is_redacted
        text last_modified_utc
        text last_seen_utc
    }

    scan_state {
        text key PK
        text value
    }
```

### 8.2 Core Cache

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}, "er": {"fontSize": 14}}}%%
erDiagram
    bridge_status_snapshots {
        int id PK
        text request_id
        text observed_at_utc
        text state
        text mode
        int outlook_connected
        int cache_stale
        text stale_reason
        text last_inbox_scan_utc
        text last_calendar_scan_utc
    }

    messages {
        text bridge_id PK
        text item_kind
        text subject
        text received_utc
        text sent_utc
        text sender_name
        text sender_email
        text body_preview
        int protected_fields_available
        int is_redacted
        text bridge_mode
        int cache_stale
        text stale_reason
        text adapter_request_id
        text observed_at_utc
    }

    events {
        text bridge_id PK
        text subject
        text start_utc
        text end_utc
        text organizer
        text body_preview
        int is_redacted
        text bridge_mode
        int cache_stale
        text stale_reason
        text adapter_request_id
        text observed_at_utc
    }

    poll_cursors {
        text key PK
        text value
        text observed_at_utc
    }

    ingest_runs {
        int id PK
        text operation_name
        text outcome
        text request_id
        text started_at_utc
        text finished_at_utc
        text error_message
    }
```

The feature adds a second SQLite boundary under `/data/openclaw.db`. It stores the latest bridge state, poll cursors, ingest history, and cached message/event rows with request IDs, stale flags, and redaction metadata preserved from the HostAdapter envelope.

## 9. Core Health, Readiness, and Container Hardening

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}}}%%
flowchart TD
    Compose["docker compose<br/>openclaw-core"]
    Runtime["Container runtime hardening<br/>non-root user<br/>read_only root filesystem<br/>cap_drop ALL<br/>no-new-privileges<br/>tmpfs /tmp"]
    Liveness["/health/live<br/>always 200 when app is running"]
    Ready{{"database ready and HostAdapter reachable?"}}
    ReadyOk["/health/ready = 200<br/>status = ready"]
    ReadyBad["/health/ready = 503<br/>status = degraded"]
    Status["/api/status<br/>counts, bridge freshness,<br/>last success/failure timestamps"]
    CacheUi["UI badges<br/>Fresh Cache / Stale Cache<br/>Redacted / Visible"]

    Compose --> Runtime
    Runtime --> Liveness
    Runtime --> Ready
    Ready -- "yes" --> ReadyOk
    Ready -- "no" --> ReadyBad
    ReadyOk --> Status
    ReadyBad --> Status
    Status --> CacheUi
```

The health model is intentionally split. Liveness proves the container is running. `/health/ready` proves both SQLite initialization and HostAdapter reachability. `/api/status` and the UI provide the operator detail needed to diagnose stale cache, failed polls, and bridge unavailability without exposing token values, message bodies, or attendee details.

## 10. End-to-End Lifecycle

```mermaid
%%{init: {"themeVariables": {"fontSize": "14px"}}}%%
flowchart TB
    HostStart["Start OpenClaw.MailBridge<br/>and OpenClaw.HostAdapter on Windows"]
    HostReady["Bridge scans Outlook<br/>and serves named-pipe cached reads"]
    ContainerStart["docker compose up openclaw-core"]
    TokenMount["Mount token file read-only<br/>at /run/openclaw/hostadapter.token"]
    Poll["Core pollers call HostAdapter<br/>through host.docker.internal"]
    Cache["Persist bridge snapshots,<br/>messages, events, cursors, ingest runs"]
    Serve["Serve UI and internal APIs<br/>at http://127.0.0.1:8080"]
    Degraded{"Bridge degraded or unavailable?"}
    Warn["Keep cached reads available<br/>show degraded / stale warnings"]
    Fallback["fallback to OpenClaw.MailBridge.Client<br/>for direct Windows-host troubleshooting"]

    HostStart --> HostReady
    ContainerStart --> TokenMount --> Poll --> Cache --> Serve --> Degraded
    Degraded -- "no" --> Serve
    Degraded -- "yes" --> Warn --> Fallback
```

This is the deployed additive flow. The Windows bridge remains the source of Outlook access and response shaping, while the new HostAdapter and Core layers add authenticated HTTP access, containerized polling, local-only UI/API endpoints, cached-read persistence, health signaling, and a documented degraded-state fallback path.
