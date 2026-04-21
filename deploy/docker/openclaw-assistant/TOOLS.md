# OpenClaw Assistant — Tool Definitions

These tools allow the OpenClaw assistant to retrieve mail and calendar data from the
Windows `OpenClaw.HostAdapter` HTTP API. All requests use authenticated HTTP calls
via `http://host.docker.internal:4319/v1`.

**Authentication:** Every request must include an `Authorization: Bearer <token>` header.
Read the token from the file at `/run/openclaw/hostadapter.token`.

**Date/time format:** All date/time parameters use ISO-8601 UTC format (e.g., `2026-04-15T00:00:00Z`).

**Limit parameter:** Must be a positive integer. The HostAdapter enforces a maximum of 250.

---

## Tool: Check Bridge Status

- **Endpoint:** `GET /v1/status`
- **URL:** `http://host.docker.internal:4319/v1/status`
- **Method:** GET
- **Headers:** `Authorization: Bearer <token>` (read from `/run/openclaw/hostadapter.token`)
- **Query parameters:** None
- **Expected response:** `200 OK` with JSON body containing bridge status fields (e.g., `{ "status": "ok", ... }`)
- **Error responses:** `401 Unauthorized` if the token is missing or invalid

**Purpose:** Health check. Use this to verify connectivity to the HostAdapter before issuing data requests.

---

## Tool: List Recent Messages

- **Endpoint:** `GET /v1/messages`
- **URL:** `http://host.docker.internal:4319/v1/messages?since=<utc>&limit=<n>`
- **Method:** GET
- **Headers:** `Authorization: Bearer <token>` (read from `/run/openclaw/hostadapter.token`)
- **Query parameters:**
  - `since` (required): ISO-8601 UTC datetime. Only messages received after this timestamp are returned.
  - `limit` (optional): Positive integer, maximum 250. Defaults to the HostAdapter configured default.
- **Expected response:** `200 OK` with JSON body `{ "items": [ { "bridgeId": "...", "subject": "...", ... } ] }`
- **Error responses:** `401 Unauthorized` if the token is missing or invalid

**Purpose:** Retrieve a list of recent mail messages for triage and summarization.

---

## Tool: Retrieve Single Message

- **Endpoint:** `GET /v1/messages/{bridgeId}`
- **URL:** `http://host.docker.internal:4319/v1/messages/{bridgeId}`
- **Method:** GET
- **Headers:** `Authorization: Bearer <token>` (read from `/run/openclaw/hostadapter.token`)
- **Path parameters:**
  - `bridgeId` (required): The unique bridge identifier for the message.
- **Expected response:** `200 OK` with JSON body containing the full message detail (e.g., `{ "bridgeId": "...", "subject": "...", "body": "...", ... }`)
- **Error responses:** `401 Unauthorized` if the token is missing or invalid; `404 Not Found` if the bridge ID does not match a known message

**Purpose:** Retrieve the full content of a specific message by its bridge ID for detailed review.

---

## Tool: List Recent Meeting Requests

- **Endpoint:** `GET /v1/meeting-requests`
- **URL:** `http://host.docker.internal:4319/v1/meeting-requests?since=<utc>&limit=<n>`
- **Method:** GET
- **Headers:** `Authorization: Bearer <token>` (read from `/run/openclaw/hostadapter.token`)
- **Query parameters:**
  - `since` (required): ISO-8601 UTC datetime. Only meeting requests received after this timestamp are returned.
  - `limit` (optional): Positive integer, maximum 250. Defaults to the HostAdapter configured default.
- **Expected response:** `200 OK` with JSON body `{ "items": [ { "bridgeId": "...", "subject": "...", ... } ] }`
- **Error responses:** `401 Unauthorized` if the token is missing or invalid

**Purpose:** Retrieve a list of recent meeting requests for scheduling analysis and triage.

---

## Tool: List Calendar Events

- **Endpoint:** `GET /v1/calendar`
- **URL:** `http://host.docker.internal:4319/v1/calendar?start=<utc>&end=<utc>&limit=<n>`
- **Method:** GET
- **Headers:** `Authorization: Bearer <token>` (read from `/run/openclaw/hostadapter.token`)
- **Query parameters:**
  - `start` (required): ISO-8601 UTC datetime. Start of the calendar window.
  - `end` (required): ISO-8601 UTC datetime. End of the calendar window.
  - `limit` (optional): Positive integer, maximum 250. Defaults to the HostAdapter configured default.
- **Expected response:** `200 OK` with JSON body `{ "items": [ { "bridgeId": "...", "subject": "...", "start": "...", "end": "...", ... } ] }`
- **Error responses:** `401 Unauthorized` if the token is missing or invalid

**Purpose:** Retrieve calendar events within a specified time window for scheduling conflict analysis.

---

## Tool: Retrieve Single Calendar Event

- **Endpoint:** `GET /v1/events/{bridgeId}`
- **URL:** `http://host.docker.internal:4319/v1/events/{bridgeId}`
- **Method:** GET
- **Headers:** `Authorization: Bearer <token>` (read from `/run/openclaw/hostadapter.token`)
- **Path parameters:**
  - `bridgeId` (required): The unique bridge identifier for the calendar event.
- **Expected response:** `200 OK` with JSON body containing the full event detail (e.g., `{ "bridgeId": "...", "subject": "...", "start": "...", "end": "...", ... }`)
- **Error responses:** `401 Unauthorized` if the token is missing or invalid; `404 Not Found` if the bridge ID does not match a known event

**Purpose:** Retrieve the full detail of a specific calendar event by its bridge ID.
