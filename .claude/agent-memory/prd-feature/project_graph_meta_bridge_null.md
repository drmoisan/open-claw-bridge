---
name: graph-meta-bridge-null
description: Graph-adapter envelopes carry Meta.Bridge == null, so any consumer requiring BridgeStatusDto (poller persist path, UpsertMessagesAsync) needs a synthesized D2-shaped status
metadata:
  type: project
---

`GraphRequestExecutor`/`GraphHostAdapterClient` (F13, #115) synthesize `ApiMeta(requestId, "cloudgraph", null)` — `Meta.Bridge` is always null. But `MessagePollingWorker.PersistPollResultAsync` requires `envelope.Meta.Bridge is not null` to record success, and `CoreCacheRepository.UpsertMessagesAsync` requires a `BridgeStatusDto` parameter.

**Why:** Graph has no bridge-status analog; only `GetStatusAsync` synthesizes a `BridgeStatusDto("ready","graph",...)` (D2 probe). This means the existing pollers would record every Graph-backend poll as a failure — a latent integration gap not fixed by F14.

**How to apply:** Specs for features writing to the Core cache from the Graph path (F14 #117 spec Decision D-3, future F16 work) must state where the `BridgeStatusDto` comes from — the accepted pattern is synthesizing the D2 "ready"/"graph" shape after a successful Graph call, never fabricating status on failure. Flag the poller gap if a feature enables `GraphAdapter:Enabled` with pollers still running. Related: [[core-namespace-partition-convention]].
