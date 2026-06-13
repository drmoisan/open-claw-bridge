---
name: hostadapter-scheduling-design-a
description: HostAdapter scheduling track (#76->#74->#75) uses Graph-shaped HostAdapter HTTP endpoints, not Core-side cache computation; #75 should follow the same pattern.
metadata:
  type: project
---

For the HostAdapter scheduling track (Track H: #76 -> #74 -> #75), the operator approved **Design A**: scheduling capabilities (mailboxSettings, getSchedule/free-busy, and by extension sendMail) are exposed as Graph-shaped HostAdapter HTTP endpoints on `IHostAdapterClient`, and `OpenClaw.Core`'s `HostAdapterSchedulingService` implements `ISchedulingService` by delegating to `IHostAdapterClient` over loopback HTTP — mirroring `GetCalendarViewAsync`/`GetEventAsync`. Data is computed HostAdapter-side (free/busy from bridge calendar data via `IHostAdapterProcessRunner`); the Core scheduling path does NOT read `CoreCacheRepository`. DTOs live in `OpenClaw.HostAdapter.Contracts`.

**Why:** The whole track exists to keep the agent's request shapes portable to real Microsoft Graph in PI-1 (the #76 rationale). Design A preserves that portability; the rejected Design B (compute free/busy in Core from the SQLite cache, keep the method off `IHostAdapterClient`) optimizes for data-locality but breaks portability and couples the scheduling seam to the cache. During #74 the task-researcher recommended Design B; the operator overrode it in favor of Design A.

**How to apply:** When orchestrating #75 (sendMail backed by COM send), default to Design A — add the method to `IHostAdapterClient` + a HostAdapter route + `HostAdapterHttpClient` client method + `HostAdapterSchedulingService` delegation; do not re-litigate Design B. If a researcher again recommends Core-side/cache computation, treat it as already-decided unless the operator reopens it. See [[free-busy-and-mailboxsettings-placement]] recorded in the #74 checkpoint and the #74 spec.md.
