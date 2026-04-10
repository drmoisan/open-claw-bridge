# OpenClaw MailBridge — Architecture Diagrams

## 1. High-Level System Overview

```mermaid
graph LR
    Outlook["Outlook<br/>(COM Automation)"]
    Bridge["MailBridge Service<br/>(Background Host)"]
    SQLite["SQLite Cache<br/>(cache.db)"]
    Client["MailBridge Client<br/>(CLI)"]

    Outlook -- "COM / MAPI<br/>(STA thread)" --> Bridge
    Bridge -- "Read/Write" --> SQLite
    Client -- "Named Pipe<br/>(JSON RPC)" --> Bridge
    Bridge -- "JSON Response" --> Client
```

## 2. Component Architecture

```mermaid
graph TB
    subgraph Host["".NET Generic Host""]
        direction TB
        BA["BridgeApplication<br/><i>Config, DI, startup</i>"]

        subgraph Workers["Hosted Services"]
            SW["ScanWorker<br/><i>Periodic scan scheduler</i>"]
            PRW["PipeRpcWorker<br/><i>Named-pipe RPC server</i>"]
        end

        subgraph Core["Core Services (DI)"]
            BSS["BridgeStateStore<br/><i>Lifecycle state machine</i>"]
            OS["OutlookScanner<br/><i>COM scan logic</i>"]
            OCH["OutlookComHelpers<br/><i>COM reflection helpers</i>"]
            STA["OutlookStaExecutor<br/><i>Dedicated STA thread</i>"]
            CR["CacheRepository<br/><i>SQLite persistence</i>"]
            RS["ResponseShaper<br/><i>Safe / Enhanced mode</i>"]
        end

        BA --> Workers
        BA --> Core
        SW --> STA
        STA --> OS
        OS --> OCH
        OS --> CR
        OS --> BSS
        PRW --> CR
        PRW --> RS
        PRW --> BSS
    end

    subgraph External
        OL["Outlook (COM)"]
        DB[("SQLite<br/>cache.db")]
        CLI["MailBridge Client"]
    end

    OS -- "COM IDispatch" --> OL
    CR -- "Read/Write" --> DB
    CLI -- "Named Pipe" --> PRW
```

## 3. Scanning Pipeline — Data Flow

```mermaid
sequenceDiagram
    participant SW as ScanWorker
    participant STA as OutlookStaExecutor<br/>(STA Thread)
    participant OS as OutlookScanner
    participant OCH as OutlookComHelpers
    participant OL as Outlook (COM)
    participant CR as CacheRepository
    participant BSS as BridgeStateStore

    loop Every inboxPollSeconds / calendarPollSeconds
        SW->>STA: Queue scan task
        STA->>OS: Execute on STA thread

        Note over OS,OL: Inbox Scan
        OS->>OL: TryGet("Outlook.Application")
        alt Outlook running
            OL-->>OS: COM reference
        else Not running & autostart
            OS->>OL: CreateAndLogonOutlook()
            OL-->>OS: New COM instance
        else Not available
            OS->>BSS: MarkOutlookUnavailable(reason)
            Note over OS: Abort scan
        end

        OS->>OL: GetDefaultFolder(Inbox)
        OL-->>OS: Folder reference
        OS->>OL: Restrict("[ReceivedTime] >= ...")
        OL-->>OS: Filtered items

        loop Each mail item (up to maxItemsPerScan)
            OS->>OCH: GetOptionalString, GetOptionalInt,<br/>GetOptionalBool, etc.
            OCH->>OL: COM reflection (IDispatch)
            OCH-->>OS: Property values
            OS->>OS: Normalize: BridgeIdCodec,<br/>BodySanitizer
            OS->>CR: UpsertMessageAsync(msg)
        end

        Note over OS,OL: Calendar Scan
        OS->>OL: GetDefaultFolder(Calendar)
        OL-->>OS: Folder reference
        OS->>OL: Restrict("[Start] >= ... AND [Start] < ...")
        OL-->>OS: Filtered appointments

        loop Each appointment
            OS->>OCH: GetOptionalString, GetOptionalDateTimeOffset, etc.
            OCH->>OL: COM reflection (IDispatch)
            OCH-->>OS: Property values
            OS->>OS: Normalize: BridgeIdCodec,<br/>BodySanitizer
            OS->>CR: UpsertEventAsync(evt)
        end

        OS->>CR: TouchScanStateAsync(timestamps)
        OS->>BSS: MarkReady()
        STA-->>SW: Task complete
    end
```

## 4. RPC Request / Response Flow

```mermaid
sequenceDiagram
    participant CLI as Client (CLI)
    participant PRW as PipeRpcWorker
    participant RS as ResponseShaper
    participant CR as CacheRepository
    participant BSS as BridgeStateStore

    CLI->>PRW: Connect to named pipe<br/>"openclaw_mailbridge_v1"

    CLI->>PRW: JSON RpcRequest<br/>{id, method, params}

    alt method = "get_status"
        PRW->>BSS: Read state snapshot
        BSS-->>PRW: BridgeStatusDto
    else method = "list_recent_messages"
        PRW->>CR: ListRecentMessagesAsync(since, limit)
        CR-->>PRW: List<MessageDto>
        PRW->>RS: Shape(messages, mode)
        RS-->>PRW: Redacted or full DTOs
    else method = "list_recent_meeting_requests"
        PRW->>CR: ListRecentMeetingRequestsAsync(since, limit)
        CR-->>PRW: List<MessageDto>
        PRW->>RS: Shape(messages, mode)
        RS-->>PRW: Redacted or full DTOs
    else method = "get_message"
        PRW->>CR: GetMessageAsync(bridgeId)
        CR-->>PRW: MessageDto?
        PRW->>RS: Shape(message, mode)
        RS-->>PRW: Shaped DTO
    else method = "list_calendar_window"
        PRW->>CR: ListCalendarWindowAsync(start, end)
        CR-->>PRW: List<EventDto>
        PRW->>RS: Shape(events, mode)
        RS-->>PRW: Redacted or full DTOs
    else method = "get_event"
        PRW->>CR: GetEventAsync(bridgeId)
        CR-->>PRW: EventDto?
        PRW->>RS: Shape(event, mode)
        RS-->>PRW: Shaped DTO
    end

    PRW-->>CLI: JSON RpcResponse<br/>{id, ok, result/error}
    PRW->>PRW: Disconnect pipe
```

## 5. Bridge State Machine

```mermaid
stateDiagram-v2
    [*] --> starting: Host starts

    starting --> waiting_for_outlook: Outlook not found<br/>& autostart disabled
    starting --> ready: First scan succeeds

    waiting_for_outlook --> ready: Outlook becomes<br/>available & scan OK
    waiting_for_outlook --> error: Unrecoverable failure

    ready --> degraded: Scan fails /<br/>Outlook disconnects
    ready --> ready: Scan succeeds

    degraded --> ready: Scan succeeds again
    degraded --> waiting_for_outlook: Outlook lost
    degraded --> error: Unrecoverable failure

    error --> [*]: Host shuts down
    ready --> [*]: Host shuts down
    degraded --> [*]: Host shuts down
    waiting_for_outlook --> [*]: Host shuts down
```

## 6. Response Shaping — Safe vs Enhanced Mode

```mermaid
flowchart TD
    REQ["RPC Response Ready"]
    MODE{{"mode setting?"}}
    TYPE{{"DTO type?"}}
    TYPE2{{"DTO type?"}}

    SAFE_MSG["Message — Safe Mode<br/>Strip:<br/>• BodyPreview → null<br/>• SenderName → null<br/>• SenderEmail → null<br/>• IsRedacted = true"]
    SAFE_EVT["Event — Safe Mode<br/>Strip:<br/>• BodyPreview → null<br/>• IsRedacted = true"]
    ENH_MSG["Message — Enhanced Mode<br/>Include:<br/>• Sanitized BodyPreview<br/>• SenderName<br/>• SenderEmail<br/>• IsRedacted = false"]
    ENH_EVT["Event — Enhanced Mode<br/>Include:<br/>• Sanitized BodyPreview<br/>• IsRedacted = false"]
    OUT["Send RpcResponse to client"]

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

## 7. Data Model — SQLite Cache

```mermaid
erDiagram
    messages {
        text bridge_id PK "msg:base64 or mtg:base64"
        text entry_id "NOT NULL"
        text store_id
        text item_kind "NOT NULL — mail | meeting"
        text subject
        text sender_name
        text sender_email
        text received_utc
        text sent_utc
        int importance
        int sensitivity
        int unread "NOT NULL — 0 or 1"
        int has_attachments "NOT NULL — 0 or 1"
        text message_class
        text to_json
        text cc_json
        text body_preview
        int protected_fields_available "NOT NULL — 0 or 1"
        int is_redacted "NOT NULL — 0 or 1"
        text last_seen_utc "NOT NULL"
    }

    events {
        text bridge_id PK "evt:base64:ISO8601"
        text entry_id
        text store_id
        text global_appointment_id
        text item_kind "NOT NULL — appointment"
        text subject
        text start_utc "NOT NULL"
        text end_utc "NOT NULL"
        text location
        int busy_status
        int meeting_status
        int is_recurring "NOT NULL — 0 or 1"
        int sensitivity
        text organizer
        text required_attendees_json
        text optional_attendees_json
        text resources_json
        text body_preview
        int protected_fields_available "NOT NULL — 0 or 1"
        int is_redacted "NOT NULL — 0 or 1"
        text last_modified_utc
        text last_seen_utc "NOT NULL"
    }

    scan_state {
        text key PK "last_inbox_scan_utc etc."
        text value "NOT NULL — ISO8601 timestamp"
    }
```

## 8. End-to-End Lifecycle

```mermaid
flowchart TB
    START(["Main()"])
    LOAD["Load bridge.settings.json<br/>from %LOCALAPPDATA%"]
    VALIDATE{"Settings valid?"}
    EXIT2["Exit code 2"]
    BUILD["Build .NET Generic Host<br/>Register DI services"]
    RUN["Host.RunAsync()"]

    subgraph Hosted["Parallel Hosted Services"]
        direction LR
        SCAN["ScanWorker loop:<br/>1. Init SQLite<br/>2. Schedule inbox scan<br/>3. Schedule calendar scan<br/>4. Sleep until next tick"]
        PIPE["PipeRpcWorker loop:<br/>1. Create named pipe<br/>2. Set ACL<br/>3. Await connection<br/>4. Handle RPC<br/>5. Disconnect"]
    end

    SERVE["Bridge serving clients<br/>State: ready / degraded"]
    STOP["CancellationToken fired"]
    SHUTDOWN["Release COM objects<br/>Close SQLite<br/>Dispose pipe"]
    DONE(["Exit code 0"])

    START --> LOAD --> VALIDATE
    VALIDATE -- No --> EXIT2
    VALIDATE -- Yes --> BUILD --> RUN --> Hosted
    Hosted --> SERVE
    SERVE --> STOP --> SHUTDOWN --> DONE
```
