# OpenClaw MailBridge API Reference

## Overview

The MailBridge exposes a local JSON-RPC API over Windows Named Pipes. An OpenClaw agent (or any local process) sends a JSON request to the pipe and receives a JSON response. There are two ways to call the API:

1. **CLI client** (`OpenClaw.MailBridge.Client.exe`) -- shell out to the client executable
2. **Named pipe directly** -- connect to the pipe from any language that supports Windows Named Pipes

The repository also includes an additive HTTP-and-container path:

- `OpenClaw.HostAdapter` runs on Windows and translates authenticated HTTP requests such as `GET /v1/status` into the same six `OpenClaw.MailBridge.Client` commands.
- `OpenClaw.Core` runs locally in Docker, calls the HostAdapter through `host.docker.internal`, and serves cache-backed UI and internal API responses.
- The six-command `OpenClaw.MailBridge.Client` contract remains unchanged and is still the canonical transport seam.

The bridge must be running before you make any calls. Start it with `OpenClaw.MailBridge.exe` (it runs as a background host process).

---

## Transport: Named Pipes

| Property | Value |
|---|---|
| Pipe name | `openclaw_mailbridge_v1` (default, configurable) |
| Direction | Duplex (InOut) |
| Transmission mode | Message |
| Max request size | 64 KB |
| Max response size | 1 MB |
| Encoding | UTF-8 JSON |

The pipe is ACL-restricted to the current interactive user, BUILTIN\Administrators, LocalSystem, and the `openclaw-svc` account. Network access is denied.

---

## Additive HostAdapter HTTP Surface

`OpenClaw.HostAdapter` is the only approved network seam for the containerized path. It wraps the existing DTOs in `ApiEnvelope<T>` responses and preserves the current CLI contract by shelling out to `OpenClaw.MailBridge.Client` with allowlisted arguments.

| Route | Backing `OpenClaw.MailBridge.Client` command | Response shape |
|---|---|---|
| `GET /v1/status` | `status` | `ApiEnvelope<BridgeStatusDto>` |
| `GET /v1/messages?since=<utc>&limit=<n>` | `list-messages --since <utc> --limit <n>` | `ApiEnvelope<ItemsResponse<MessageDto>>` |
| `GET /v1/messages/{bridgeId}` | `get-message --id <bridgeId>` | `ApiEnvelope<MessageDto>` |
| `GET /v1/meeting-requests?since=<utc>&limit=<n>` | `list-meeting-requests --since <utc> --limit <n>` | `ApiEnvelope<ItemsResponse<MessageDto>>` |
| `GET /v1/calendar?start=<utc>&end=<utc>&limit=<n>` | `list-calendar --start <utc> --end <utc> --limit <n>` | `ApiEnvelope<ItemsResponse<EventDto>>` |
| `GET /v1/events/{bridgeId}` | `get-event --id <bridgeId>` | `ApiEnvelope<EventDto>` |

Requests use `Authorization: Bearer <token>` and may include `X-Request-Id`. The additive `OpenClaw.Core` container path reaches this HTTP surface through `host.docker.internal`.

---

## Additive Core Internal API

`OpenClaw.Core` is the local-only UI and cache layer used by the Docker deployment. It serves `/health/live`, `/health/ready`, `/api/status`, `/api/messages/recent`, `/api/messages/{bridgeId}`, `/api/events/window`, and `/api/events/{bridgeId}` from its SQLite-backed cache.

---

## Configuration

Settings are stored at:
```
%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json
```

A default file is created on first run:

```json
{
  "pipeName": "openclaw_mailbridge_v1",
  "mode": "safe",
  "autostartOutlook": true,
  "inboxPollSeconds": 30,
  "calendarPollSeconds": 300,
  "inboxOverlapMinutes": 5,
  "calendarPastDays": 14,
  "calendarFutureDays": 60,
  "maxItemsPerScan": 500,
  "bodyPreviewMaxChars": 500,
  "logLevel": "Information"
}
```

### Key settings for agent integration

| Setting | Effect on API responses |
|---|---|
| `mode` | `"safe"` strips sender info and body previews from responses. `"enhanced"` includes them. |
| `maxItemsPerScan` | Upper bound for the `limit` parameter on list methods (1--2000). |
| `bodyPreviewMaxChars` | Max length of `bodyPreview` strings in enhanced mode (1--2000). |
| `inboxPollSeconds` | How frequently the cache refreshes from Outlook. Data may lag by this interval. |
| `calendarPastDays` / `calendarFutureDays` | Window of calendar events the bridge keeps cached. |

---

## Request Format

Every API call is a JSON object with this shape:

```json
{
  "id": "<unique-request-id>",
  "method": "<method-name>",
  "params": {
    "<key>": "<value>"
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | Yes | A unique identifier for this request. Use a UUID. Echoed back in the response. |
| `method` | string | Yes | One of the six method names listed below. |
| `params` | object | Depends | A flat dictionary of string key/value pairs. Required by most methods. |

---

## Response Format

Every response is a JSON object with this shape:

### Success

```json
{
  "id": "<echoed-request-id>",
  "ok": true,
  "result": { ... },
  "error": null
}
```

### Failure

```json
{
  "id": "<echoed-request-id>",
  "ok": false,
  "result": null,
  "error": {
    "code": "<ERROR_CODE>",
    "message": "Human-readable description."
  }
}
```

### Error codes

| Code | Meaning |
|---|---|
| `INVALID_REQUEST` | Malformed JSON, unsupported method, missing/invalid parameter. |
| `UNAUTHORIZED` | Pipe ACL denied the caller. |
| `OUTLOOK_UNAVAILABLE` | Outlook is not running and could not be started. |
| `NOT_FOUND` | The requested message or event does not exist in the cache. |
| `INTERNAL_ERROR` | Unexpected server error. |
| `PAYLOAD_TOO_LARGE` | Request exceeds 64 KB or response exceeds 1 MB. |

---

## API Methods

### 1. `get_status`

Check bridge health before making other calls. Use this to confirm the bridge is `ready` and Outlook is connected.

**Parameters:** None required.

**Request:**
```json
{
  "id": "abc-123",
  "method": "get_status",
  "params": null
}
```

**Response (`result`):**
```json
{
  "state": "ready",
  "mode": "safe",
  "outlookConnected": true,
  "cacheStale": false,
  "staleReason": null,
  "lastInboxScanUtc": "2026-04-10T14:30:00.0000000+00:00",
  "lastCalendarScanUtc": "2026-04-10T14:25:00.0000000+00:00"
}
```

| Field | Type | Description |
|---|---|---|
| `state` | string | `starting`, `waiting_for_outlook`, `ready`, `degraded`, or `error` |
| `mode` | string | `safe` or `enhanced` |
| `outlookConnected` | bool | Whether the bridge has an active COM connection to Outlook |
| `cacheStale` | bool | Whether cached data may be outdated |
| `staleReason` | string? | Why the cache is stale (e.g. `"scan_failure"`, `"running_instance_unavailable"`) |
| `lastInboxScanUtc` | string? | ISO-8601 timestamp of last successful inbox scan |
| `lastCalendarScanUtc` | string? | ISO-8601 timestamp of last successful calendar scan |

**Agent guidance:** Call `get_status` first. If `state` is not `ready`, wait and retry. If `cacheStale` is `true`, data may be outdated -- include this caveat when presenting results to the user.

---

### 2. `list_recent_messages`

List email messages received since a given timestamp.

**Parameters:**

| Key | Type | Required | Description |
|---|---|---|---|
| `since` | string | Yes | ISO-8601 timestamp. Only messages with `receivedUtc >= since` are returned. |
| `limit` | string | Yes | Maximum number of messages to return (1 to `maxItemsPerScan`). |

**Request:**
```json
{
  "id": "abc-124",
  "method": "list_recent_messages",
  "params": {
    "since": "2026-04-10T00:00:00Z",
    "limit": "50"
  }
}
```

**Response (`result`):**
```json
{
  "items": [
    {
      "bridgeId": "msg:dGVzdC1lbnRyeS1pZA",
      "itemKind": "mail",
      "subject": "Q2 Planning Update",
      "receivedUtc": "2026-04-10T09:15:00.0000000+00:00",
      "sentUtc": "2026-04-10T09:14:30.0000000+00:00",
      "importance": 1,
      "sensitivity": 0,
      "unread": true,
      "hasAttachments": false,
      "messageClass": "IPM.Note",
      "senderName": null,
      "senderEmail": null,
      "toJson": null,
      "ccJson": null,
      "bodyPreview": null,
      "protectedFieldsAvailable": true,
      "isRedacted": true
    }
  ]
}
```

**Safe mode vs Enhanced mode:**

In **safe mode** (default), each message has:
- `senderName`: `null`
- `senderEmail`: `null`
- `bodyPreview`: `null`
- `isRedacted`: `true`

In **enhanced mode**, these fields are populated with actual values and `isRedacted` is `false`.

**Items are sorted by `receivedUtc` descending** (newest first).

---

### 3. `list_recent_meeting_requests`

Same as `list_recent_messages` but filtered to meeting requests only (`itemKind = "meeting"`).

**Parameters:**

| Key | Type | Required | Description |
|---|---|---|---|
| `since` | string | Yes | ISO-8601 timestamp. |
| `limit` | string | Yes | Max results (1 to `maxItemsPerScan`). |

**Request:**
```json
{
  "id": "abc-125",
  "method": "list_recent_meeting_requests",
  "params": {
    "since": "2026-04-09T00:00:00Z",
    "limit": "20"
  }
}
```

**Response:** Same shape as `list_recent_messages`, but every item has `"itemKind": "meeting"`.

---

### 4. `get_message`

Retrieve a single message by its bridge ID.

**Parameters:**

| Key | Type | Required | Description |
|---|---|---|---|
| `id` | string | Yes | The `bridgeId` value from a list response (e.g. `"msg:dGVzdC1lbnRyeS1pZA"`). |

**Request:**
```json
{
  "id": "abc-126",
  "method": "get_message",
  "params": {
    "id": "msg:dGVzdC1lbnRyeS1pZA"
  }
}
```

**Success response (`result`):** A single `MessageDto` object (same shape as items in `list_recent_messages`).

**Failure response (not found):**
```json
{
  "id": "abc-126",
  "ok": false,
  "result": null,
  "error": {
    "code": "NOT_FOUND",
    "message": "Message not found."
  }
}
```

**Failure response (invalid ID format):**
```json
{
  "id": "abc-126",
  "ok": false,
  "result": null,
  "error": {
    "code": "INVALID_REQUEST",
    "message": "The supplied message bridge ID is invalid."
  }
}
```

---

### 5. `list_calendar_window`

List calendar events whose start time falls within a given window.

**Parameters:**

| Key | Type | Required | Description |
|---|---|---|---|
| `start` | string | Yes | ISO-8601 timestamp. Events with `startUtc >= start` are included. |
| `end` | string | Yes | ISO-8601 timestamp. Events with `startUtc < end` are included. Must be after `start`. |
| `limit` | string | Yes | Max results (1 to `maxItemsPerScan`). |

**Request:**
```json
{
  "id": "abc-127",
  "method": "list_calendar_window",
  "params": {
    "start": "2026-04-10T00:00:00Z",
    "end": "2026-04-17T00:00:00Z",
    "limit": "100"
  }
}
```

**Response (`result`):**
```json
{
  "items": [
    {
      "bridgeId": "evt:Z2xvYmFsLWlk:2026-04-11T10:00:00.0000000Z",
      "globalAppointmentId": "global-id",
      "subject": "Sprint Retrospective",
      "startUtc": "2026-04-11T10:00:00.0000000+00:00",
      "endUtc": "2026-04-11T11:00:00.0000000+00:00",
      "location": "Conference Room B",
      "busyStatus": 2,
      "meetingStatus": 1,
      "isRecurring": true,
      "sensitivity": 0,
      "organizer": null,
      "requiredAttendeesJson": null,
      "optionalAttendeesJson": null,
      "resourcesJson": null,
      "bodyPreview": null,
      "protectedFieldsAvailable": true,
      "isRedacted": true
    }
  ]
}
```

**Safe mode vs Enhanced mode:**

In **safe mode**, `bodyPreview` and `bodyFull` are `null` and `isRedacted` is `true`.

In **enhanced mode**, `bodyPreview` contains sanitized event body text, `bodyFull` contains the full untruncated body text, and `isRedacted` is `false`.

**Items are sorted by `startUtc` ascending** (earliest first).

**The bridge only caches events within the configured window:** `calendarPastDays` (default 14) before today through `calendarFutureDays` (default 60) after today. Requests outside this window return empty results.

---

### 6. `get_event`

Retrieve a single calendar event by its bridge ID.

**Parameters:**

| Key | Type | Required | Description |
|---|---|---|---|
| `id` | string | Yes | The `bridgeId` value from a list response (e.g. `"evt:Z2xvYmFsLWlk:2026-04-11T10:00:00.0000000Z"`). |

**Request:**
```json
{
  "id": "abc-128",
  "method": "get_event",
  "params": {
    "id": "evt:Z2xvYmFsLWlk:2026-04-11T10:00:00.0000000Z"
  }
}
```

**Success response (`result`):** A single `EventDto` object (same shape as items in `list_calendar_window`).

**Failure responses** follow the same pattern as `get_message` (`NOT_FOUND` or `INVALID_REQUEST`).

---

## DTO Field Reference

### MessageDto

| Field | Type | Description |
|---|---|---|
| `bridgeId` | string | Unique ID in format `msg:<base64>` or `mtg:<base64>`. Use this for `get_message`. |
| `itemKind` | string | `"mail"` for regular email, `"meeting"` for meeting requests. |
| `subject` | string? | Email subject line. |
| `receivedUtc` | string? | ISO-8601 timestamp when the message was received. |
| `sentUtc` | string? | ISO-8601 timestamp when the message was sent. |
| `importance` | int? | Outlook importance: 0=Low, 1=Normal, 2=High. |
| `sensitivity` | int? | Outlook sensitivity: 0=Normal, 1=Personal, 2=Private, 3=Confidential. |
| `unread` | bool | Whether the message is unread in Outlook. |
| `hasAttachments` | bool | Whether the message has attachments. |
| `messageClass` | string? | Outlook message class (e.g. `"IPM.Note"`, `"IPM.Schedule.Meeting.Request"`). |
| `senderName` | string? | Display name of the sender. **Null in safe mode.** |
| `senderEmail` | string? | Email address of the sender. **Null in safe mode.** |
| `toJson` | string? | Reserved for future use. |
| `ccJson` | string? | Reserved for future use. |
| `bodyPreview` | string? | Sanitized body text (HTML/file paths stripped). **Null in safe mode.** |
| `protectedFieldsAvailable` | bool | Whether protected fields could be retrieved from Outlook. |
| `isRedacted` | bool | `true` in safe mode, `false` in enhanced mode. |

### EventDto

| Field | Type | Description |
|---|---|---|
| `bridgeId` | string | Unique ID in format `evt:<base64>:<ISO-8601-start>`. Use this for `get_event`. |
| `globalAppointmentId` | string? | Outlook's GlobalAppointmentID for recurring series. |
| `subject` | string? | Event title. |
| `startUtc` | string | ISO-8601 start time. |
| `endUtc` | string | ISO-8601 end time. |
| `location` | string? | Meeting location. |
| `busyStatus` | int? | 0=Free, 1=Tentative, 2=Busy, 3=OutOfOffice, 4=WorkingElsewhere. |
| `meetingStatus` | int? | 0=NonMeeting, 1=Meeting, 3=Received, 5=Canceled. |
| `isRecurring` | bool | Whether this is an instance of a recurring series. |
| `sensitivity` | int? | 0=Normal, 1=Personal, 2=Private, 3=Confidential. |
| `organizer` | string? | Display name of the meeting organizer. |
| `requiredAttendeesJson` | string? | JSON string of required attendees. |
| `optionalAttendeesJson` | string? | JSON string of optional attendees. |
| `resourcesJson` | string? | JSON string of resource attendees (rooms, etc.). |
| `bodyPreview` | string? | Sanitized event body text. **Null in safe mode.** |
| `protectedFieldsAvailable` | bool | Whether protected fields could be retrieved from Outlook. |
| `isRedacted` | bool | `true` in safe mode, `false` in enhanced mode. |
| `responseStatus` | int? | Outlook response status: 0=None, 1=Organized, 2=Tentative, 3=Accepted, 4=Declined, 5=NotResponded. |
| `categories` | string[]? | Outlook categories assigned to the event. |
| `isOrganizer` | bool | Whether the mailbox owner organized the event (derived from `responseStatus == 1`). |
| `isOnlineMeeting` | bool | Whether Outlook flags the event as an online meeting. May report `false` for some third-party add-in meetings. |
| `allowNewTimeProposals` | bool | Whether the organizer permits new time proposals. |
| `iCalUId` | string? | iCalendar UID. Sourced from Outlook's GlobalAppointmentID; not a true RFC 5545 UID. |
| `seriesMasterId` | string? | Identifier of the recurring-series master for an occurrence or exception; `null` for non-recurring events and series masters. |
| `lastModifiedDateTime` | string? | ISO-8601 timestamp of the event's last modification. |
| `bodyFull` | string? | Full (untruncated) event body text. **Null in safe mode**; returned unsanitized in enhanced mode. |
| `sensitivityLabel` | string? | Sensitivity as a label string: `normal`, `personal`, `private`, or `confidential` (derived from `sensitivity`). |

---

## Using the CLI Client

The CLI client is the simplest way to call the API from an agent. It handles pipe connection, serialization, and error mapping.

### Syntax

```
OpenClaw.MailBridge.Client.exe <command> [--param value ...]
```

### Commands

| Command | Maps to method | Required flags |
|---|---|---|
| `status` | `get_status` | (none) |
| `list-messages` | `list_recent_messages` | `--since <ISO-8601>` `--limit <n>` |
| `get-message` | `get_message` | `--id <bridgeId>` |
| `list-meeting-requests` | `list_recent_meeting_requests` | `--since <ISO-8601>` `--limit <n>` |
| `list-calendar` | `list_calendar_window` | `--start <ISO-8601>` `--end <ISO-8601>` `--limit <n>` |
| `get-event` | `get_event` | `--id <bridgeId>` |

### Optional flags

| Flag | Description |
|---|---|
| `--pipe-name <name>` | Override the pipe name (default: reads from bridge.settings.json, then falls back to `openclaw_mailbridge_v1`). |

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (`ok: true`). |
| 2 | Connection/IO error (bridge not running, timeout). |
| 3 | Unauthorized (pipe ACL denied). |
| 4 | Outlook unavailable. |
| 5 | Invalid request or missing parameter. |
| 6 | Other error. |

### Examples

**Check bridge status:**
```bash
OpenClaw.MailBridge.Client.exe status
```

**List emails from the last 24 hours:**
```bash
OpenClaw.MailBridge.Client.exe list-messages --since "2026-04-09T14:00:00Z" --limit 50
```

**Get a specific message:**
```bash
OpenClaw.MailBridge.Client.exe get-message --id "msg:dGVzdC1lbnRyeS1pZA"
```

**List meeting requests from the last week:**
```bash
OpenClaw.MailBridge.Client.exe list-meeting-requests --since "2026-04-03T00:00:00Z" --limit 20
```

**List calendar events for the next 7 days:**
```bash
OpenClaw.MailBridge.Client.exe list-calendar --start "2026-04-10T00:00:00Z" --end "2026-04-17T00:00:00Z" --limit 100
```

**Get a specific event:**
```bash
OpenClaw.MailBridge.Client.exe get-event --id "evt:Z2xvYmFsLWlk:2026-04-11T10:00:00.0000000Z"
```

All commands write the full JSON response to stdout. Errors are written to stderr.

---

## Calling the Pipe Directly (Any Language)

If your agent can't shell out to the CLI, connect to the named pipe directly. Here is the protocol:

### Step 1: Connect

Open a duplex named pipe client to `\\.\pipe\openclaw_mailbridge_v1` with message mode. Set a connection timeout (the CLI uses 2 seconds).

### Step 2: Send request

Serialize the `RpcRequest` JSON to UTF-8 bytes and write them to the pipe. Flush.

### Step 3: Read response

Read message-mode chunks until `IsMessageComplete` is true. Concatenate into a UTF-8 string and deserialize as an `RpcResponse`.

### Step 4: Disconnect

Close the pipe. Each request uses a fresh connection.

### Python example

```python
import json
import uuid
import win32pipe
import win32file

PIPE_NAME = r"\\.\pipe\openclaw_mailbridge_v1"

def call_bridge(method: str, params: dict | None = None) -> dict:
    handle = win32file.CreateFile(
        PIPE_NAME,
        win32file.GENERIC_READ | win32file.GENERIC_WRITE,
        0, None,
        win32file.OPEN_EXISTING,
        0, None
    )
    win32pipe.SetNamedPipeHandleState(
        handle, win32pipe.PIPE_READMODE_MESSAGE, None, None
    )

    request = json.dumps({
        "id": str(uuid.uuid4()),
        "method": method,
        "params": params
    }).encode("utf-8")

    win32file.WriteFile(handle, request)

    _, data = win32file.ReadFile(handle, 65536)
    win32file.CloseHandle(handle)

    return json.loads(data.decode("utf-8"))


# Check status
status = call_bridge("get_status")
print(status)

# List recent messages
messages = call_bridge("list_recent_messages", {
    "since": "2026-04-10T00:00:00Z",
    "limit": "50"
})
print(messages)
```

### C# example

```csharp
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

var pipeName = "openclaw_mailbridge_v1";
var request = JsonSerializer.Serialize(new {
    id = Guid.NewGuid().ToString(),
    method = "get_status",
    @params = (object?)null
});

await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
await client.ConnectAsync(2000);
client.ReadMode = PipeTransmissionMode.Message;

await client.WriteAsync(Encoding.UTF8.GetBytes(request));
await client.FlushAsync();

using var ms = new MemoryStream();
var buffer = new byte[65536];
do {
    var read = await client.ReadAsync(buffer);
    if (read == 0) break;
    ms.Write(buffer, 0, read);
} while (!client.IsMessageComplete);

var response = Encoding.UTF8.GetString(ms.ToArray());
Console.WriteLine(response);
```

---

## Agent Integration Guide

### Recommended call sequence

1. **Call `get_status`** to confirm `state` is `"ready"` and `outlookConnected` is `true`.
2. **List data** with `list_recent_messages`, `list_recent_meeting_requests`, or `list_calendar_window`.
3. **Drill into specifics** with `get_message` or `get_event` using `bridgeId` values from step 2.

### Handling safe mode

If `isRedacted` is `true` on returned items, the bridge is running in safe mode. The agent should:
- Tell the user that sender details and email body previews are not available.
- Explain that the administrator can switch to `"enhanced"` mode in `bridge.settings.json` to enable these fields.

### Handling degraded state

If `get_status` returns `state: "degraded"` and `cacheStale: true`:
- Data is still returned from the SQLite cache, but it may be outdated.
- Check `staleReason` for the cause (e.g. `"scan_failure"`, `"running_instance_unavailable"`).
- Present cached data with a caveat about freshness.

### Timestamps

All timestamps in requests and responses use ISO-8601 format with UTC offset (e.g. `"2026-04-10T14:30:00Z"` or `"2026-04-10T14:30:00.0000000+00:00"`). Always send UTC timestamps in requests.

### Bridge ID format

Bridge IDs are opaque identifiers. Do not parse or construct them. Always use values returned by list methods as inputs to get methods.

| Prefix | Type |
|---|---|
| `msg:` | Regular email message |
| `mtg:` | Meeting request (also a message) |
| `evt:` | Calendar event |

### Rate limiting

The bridge accepts up to 4 concurrent pipe connections. For an agent, sequential calls are recommended. The bridge does not impose rate limits, but frequent calls to list methods with high limits may slow response times.
