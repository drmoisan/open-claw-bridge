# Acceptance Criteria to Evidence Mapping

Timestamp: 2026-06-12T23-17

AC source of record: `docs/features/active/evolve-hostadapter-graph-surface-76/user-story.md` (7 criteria). All 7 boxes checked after evidence confirmed PASS.

| AC | Criterion (abbreviated) | Verifying evidence | Verdict |
|---|---|---|---|
| AC1 | Graph-shaped routes; no `/v1/*` except `/status` | `evidence/qa-gates/route-surface-check.2026-06-12T23-17.md` (zero `/v1/`, 5 Graph routes + /status); `HostAdapterEndpointTests`, `HostAdapterAuthTests`, `HostAdapterValidationTests`, `HostAdapterMappingTests`, `HostAdapterEnvelopeTests` (all pass) | PASS |
| AC2 | `IHostAdapterClient`/`HostAdapterHttpClient` call Graph endpoints, envelope-wrapped; `ListMeetingRequestsAsync` retained | `evidence/qa-gates/contract-compat.2026-06-12T23-17.md`; `HostAdapterHttpClientTests` (Graph-path assertions pass) | PASS |
| AC3 | `OpenClawOptions.HostAdapter.BaseUrl` default has no `/v1/` | P2-T1 (`src/OpenClaw.Core/CoreOptions.cs` default `http://host.docker.internal:4319/`); `HostAdapterHttpClientTests` base-url + `GetStatusAsync_SendsGetRequestToStatusPath` (`/status`) pass | PASS |
| AC4 | Adapter version reports `1.0.0` via `meta.adapterVersion` | P1-T10 (`<Version>1.0.0</Version>` in csproj); P1-T18 `HostAdapterVersionTests` (`DefaultAdapterVersion == "1.0.0"`, pass); `evidence/qa-gates/contract-compat.2026-06-12T23-17.md` | PASS |
| AC5 | `MailboxId` default `"me"` configurable on `HostAdapterOptions`; renders `{id}` | Adapter: P1-T1/P1-T2 (`HostAdapterOptions.MailboxId`, post-configure normalize). Core: P2-T2/P2-T3 (`CoreOptions.HostAdapterOptions.MailboxId`, `HostAdapterHttpClient` sources `{id}`). `/users/me/...` assertions in `HostAdapterHttpClientTests` pass | PASS |
| AC6 | Existing contract/endpoint tests pass against new routes, not weakened | `evidence/qa-gates/phase1-test`, `phase2-test`, `final-test.2026-06-12T23-17.md` (428 passed, 0 failed); assertions retained at equal strength | PASS |
| AC7 | Line >= 85%, branch >= 75% on changed code; no regression | `evidence/qa-gates/coverage-delta.2026-06-12T23-17.md` (changed-code lines 98.85%, branches 79.41%; no changed-line regression) | PASS |

All 7 acceptance criteria are checked off in `user-story.md`.
