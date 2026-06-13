# Acceptance Criteria Verification Map (issue #74)

Timestamp: 2026-06-13T10-30
AC source: docs/features/active/hostadapter-mailboxsettings-getschedule-74/user-story.md
Work mode: full-feature (AC source = user-story.md, 7 acceptance criteria)
Overall verdict: PASS

| AC | Criterion (abridged) | Satisfying tasks | Tests / evidence | Verdict |
|---|---|---|---|---|
| AC1 | mailboxSettings returns valid TZ + working hours from config; defaults UTC/Mon-Fri/09:00-17:00 | P3-T1, P3-T2, P4-T2, P4-T4 | HostAdapterEndpointTests: MailboxSettings_returns_configured_values, MailboxSettings_returns_documented_defaults_when_section_absent, MailboxSettings_returns_configuration_error_* | PASS |
| AC2 | getSchedule returns deterministic free/busy from CLI-fetched calendar data; computed via injected TimeProvider, never wall-clock | P4-T1, P4-T3, P4-T5, P4-T7, P6-T3 | FreeBusyProjectionTests (5 cases); HostAdapterEndpointTests.GetSchedule_returns_busy_intervals_for_non_free_events / empty-window / downstream-failure; HostAdapterSchedulingServiceTests free/busy delegation uses FakeTimeProvider-derived window | PASS |
| AC3 | IHostAdapterClient + HostAdapterHttpClient expose both methods returning the two envelopes; additive | P2-T1, P2-T2, P5-T1, P5-T2, P5-T3 | HostAdapterHttpClientTests: GetMailboxSettingsAsync_SendsGetToMailboxSettingsPath, GetFreeBusyAsync_SendsGetToGetSchedulePathWithEncodedWindow, plus envelope round-trip tests | PASS |
| AC4 | HostAdapterSchedulingService two methods implemented by delegation; their stubs removed; SendMailAsync stub remains; pipeline consumes them | P6-T1, P6-T2, P6-T3, P7-T3 | HostAdapterSchedulingServiceTests: delegation + failure-path for both; SendMailAsync_Throws retained; SchedulingWorkerTests verifies pipeline consumption (evidence/regression-testing/pipeline-consumption.md) | PASS |
| AC5 | Three DTOs relocated to OpenClaw.HostAdapter.Contracts; references updated; contract round-trip tests pass | P1-T1..P1-T7, P7-T1, P7-T2 | SchedulingContracts.cs created; old Core DTO files deleted; SchedulingDtoContractTests round-trips for the DTOs and ApiEnvelope<MailboxSettingsDto>/ApiEnvelope<FreeBusyScheduleDto> | PASS |
| AC6 | Free/busy uses TimeProvider; tests use FakeTimeProvider and mock the data source (IHostAdapterProcessRunner HostAdapter side, IHostAdapterClient Core side); no temp files; no real Outlook/network | P4-T5, P4-T7, P6-T3 | HostAdapter route tests mock IHostAdapterProcessRunner via HostAdapterProcessRunnerStub; Core delegation tests mock IHostAdapterClient with Moq and derive windows from FakeTimeProvider; no temp files; no real network (WebApplicationFactory in-memory + FakeHttpHandler) | PASS |
| AC7 | Line >= 85%, branch >= 75% on changed code; no regression on changed lines | P9-T5, P9-T6 | coverage-delta.md: every changed file 100% line / 100% branch; no regression (stub/absent -> fully covered); whole-project line >= 85%, branch improved from baseline | PASS |

## Notes
- Design A (locked) implemented: free/busy computed in OpenClaw.HostAdapter from bridge calendar
  data via IHostAdapterProcessRunner; OpenClaw.Core consumes both routes over loopback HTTP via
  IHostAdapterClient; CoreCacheRepository is not consulted for free/busy.
- SendMailAsync remains a NotSupportedException stub (deferred to #75), as required.
- No new ProjectReference edges; OpenClaw.HostAdapter does not reference OpenClaw.Core or the COM
  host; OpenClaw.HostAdapter.Contracts depends only on OpenClaw.MailBridge.Contracts.
- AC2 wording note: the spec/user-story phrase "cached calendar data ... computed via the
  injected TimeProvider" is realized under Design A as HostAdapter-fetched (not cached) calendar
  data; the window boundaries are supplied by the caller, which derives them from the injected
  TimeProvider (verified by the FakeTimeProvider-driven Core delegation tests). The projection
  itself reads no wall-clock time.
