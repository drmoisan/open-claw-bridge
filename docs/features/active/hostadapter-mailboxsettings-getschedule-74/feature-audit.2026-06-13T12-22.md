# Feature Audit: HostAdapter mailboxSettings and calendar/getSchedule (#74)

**Audit Date:** 2026-06-13
**Feature Folder:** `docs/features/active/hostadapter-mailboxsettings-getschedule-74`
**Base Branch:** `main`
**Head Branch:** `open-claw-bridge-wt-2026-06-13-10-28`
**Work Mode:** `full-feature`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (commit `48314bd6cb9c1a9f0bae4ca0c775a95ec52f3a61`)
- **Head branch/commit:** `open-claw-bridge-wt-2026-06-13-10-28` (commit `bbdce9f26ba3913de677f38a1a9db54fc1deddd6`)
- **Merge base:** `48314bd6cb9c1a9f0bae4ca0c775a95ec52f3a61`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/**`
  - Additional evidence: reviewer-regenerated `coverage.cobertura.xml` and full toolchain run
- **Feature folder used:** `docs/features/active/hostadapter-mailboxsettings-getschedule-74`
- **Requirements source:** `user-story.md` (and `spec.md`) per `full-feature` work mode
- **Work mode resolution note:** `issue.md` is absent at the feature root; the persisted marker
  `- **Work Mode:** full-feature` in `plan.2026-06-13T10-30.md` and
  `evidence/other/acceptance-criteria-map.md` is authoritative. The `## Acceptance Criteria`
  checkbox section lives in `user-story.md`, which is the AC source for `full-feature`.
- **Scope note:** Audit scope is the full branch diff against `main`. C# is the only language
  with changed files. The PR-context summary's "Core logic changes: 0 files" classification is
  inaccurate; the audit uses the actual `git diff --name-status` output.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/hostadapter-mailboxsettings-getschedule-74/user-story.md` — primary checkbox source
- `docs/features/active/hostadapter-mailboxsettings-getschedule-74/spec.md` — secondary (prose constraints; no separate checkbox AC list)

### Acceptance criteria (from user-story.md)

1. `mailboxSettings` returns a valid time zone and working hours, sourced from `HostAdapterOptions.MailboxSettings` configuration (defaults: `TimeZoneId` UTC, working days Monday–Friday, working hours 09:00–17:00).
2. `getSchedule` returns a free/busy grid computed deterministically from the cached calendar data the HostAdapter fetches through the CLI client process; the grid is computed via the injected `TimeProvider`, never wall-clock time.
3. `IHostAdapterClient` and `HostAdapterHttpClient` expose both new methods (`GetMailboxSettingsAsync`, `GetFreeBusyAsync`) returning `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>` respectively; the additions are additive to the interface.
4. `HostAdapterSchedulingService.GetMailboxSettingsAsync` and `GetFreeBusyAsync` are implemented by delegating to `IHostAdapterClient` (mirroring `GetCalendarViewAsync` and `GetEventAsync`); the `NotSupportedException` stubs for these two methods are removed (the `SendMailAsync` stub remains deferred to #75); the `propose_times` pipeline consumes them.
5. `MailboxSettingsDto`, `FreeBusyScheduleDto`, and `BusyIntervalDto` are relocated from `OpenClaw.Core.Agent` to `OpenClaw.HostAdapter.Contracts`; all references are updated; the contract/schema round-trip tests pass.
6. The free/busy computation uses `TimeProvider`; tests advance simulated time with `FakeTimeProvider` and mock the data source (`IHostAdapterProcessRunner` on the HostAdapter side, `IHostAdapterClient` on the Core side); no temporary files; no real Outlook or network.
7. Line coverage >= 85% and branch coverage >= 75% on changed code; no coverage regression on changed lines.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| 1 | mailboxSettings config-sourced with documented defaults | PASS | `SchedulingRoutes.HandleMailboxSettings` reads `IOptions<HostAdapterOptions>.MailboxSettings`; `MailboxSettingsOptions` defaults are UTC / Mon–Fri / 09:00–17:00; route does not call the bridge. Endpoint tests pass. | `dotnet test ... --collect:"XPlat Code Coverage"` | Invalid config returns a typed configuration-error envelope. |
| 2 | getSchedule deterministic free/busy from CLI-fetched calendar data | PASS | `HandleGetScheduleAsync` fetches via `IHostAdapterProcessRunner` + `HostAdapterCommandBuilder.BuildListCalendar`, then projects via pure `FreeBusyProjection`; the caller-supplied window is validated. The projection has no wall-clock dependency. | `dotnet test ...` (FreeBusyProjectionTests, HostAdapterEndpointTests) | Window "now" derivation occurs in the caller (TimeProvider); the route consumes the supplied window deterministically. |
| 3 | IHostAdapterClient + HostAdapterHttpClient expose both additive methods | PASS | `IHostAdapterClient.cs` diff adds `GetMailboxSettingsAsync`/`GetFreeBusyAsync` returning the typed envelopes; `HostAdapterHttpClient.cs` implements both. Additions are appended (additive). | `git diff` of both files | Keyword-style optional params; D2 portability documented. |
| 4 | HostAdapterSchedulingService delegates; stubs removed; pipeline consumes | PASS | Diff replaces both `NotSupportedException` bodies with delegating implementations; `SendMailAsync` stub retained for #75; `SchedulingWorker.Pipeline.cs`/`SlotProposer*` updated and pipeline-consumption evidence present. | `git diff`; `evidence/regression-testing/pipeline-consumption.md` | Adds graceful degradation on non-OK envelope, consistent with `GetCalendarViewAsync`. |
| 5 | DTOs relocated to HostAdapter.Contracts; references updated; contract tests pass | PASS | New `SchedulingContracts.cs` records are field-for-field identical to the deleted Core records (verified against baseline blobs); the two Core DTO files are deleted; four referencing files add `using OpenClaw.HostAdapter.Contracts;`; `SchedulingDtoContractTests` round-trips pass. | `git show 48314bd:...` vs new file; `dotnet test ...` | No new `ProjectReference` edge; Rule 6 already permits the edge. |
| 6 | TimeProvider/FakeTimeProvider; mocked data source; no temp files; no real Outlook/network | PASS | `HostAdapterSchedulingServiceTests` uses `FakeTimeProvider` to derive windows and Moq for `IHostAdapterClient`; HostAdapter tests stub `IHostAdapterProcessRunner` via the test web factory; no temp-file or network usage observed. | `dotnet test ...`; inspection of test files | Projection is clock-free; service-side window comes from FakeTimeProvider. |
| 7 | Line >= 85% / branch >= 75% on changed code; no regression on changed lines | PASS | Reviewer-regenerated cobertura: changed files 100% line / 100% branch (HostAdapterOptions.cs changed lines covered; class-level 50% branch pre-existing); whole-project line >= 85% both projects; branch held/improved. | `dotnet test ... --collect:"XPlat Code Coverage"` | Per-package figures confirm thresholds; depressed root figure in HostAdapter run is a cross-assembly attribution artifact for MailBridge.Contracts. |

---

## Summary

**Overall Feature Readiness:** PASS

**Criteria summary:**
- **PASS:** 7 criteria
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. None.

**Recommended follow-up verification steps:**

1. Optionally confirm with the author that the Core-side graceful-degradation behavior (defaults / empty grid on non-OK envelope) is the intended pipeline contract for Stage 0.
2. Optionally add the `HostAdapterOptions.cs` row to `evidence/qa-gates/coverage-delta.md` for completeness.

---

## Acceptance Criteria Check-off

All 7 criteria evaluate to PASS and were already checked (`- [x]`) in `user-story.md` by the
executor. The reviewer confirms the check-offs are warranted by the evidence; no changes to the
checkbox state were required.

### AC Status Summary

- Source: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/user-story.md`
- Total AC items: 7
- Checked off (delivered): 7
- Remaining (unchecked): 0
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `user-story.md` | 7 | 7 | 0 | Checkbox-backed; all confirmed PASS by reviewer |
| `spec.md` | 0 | 0 | 0 | Prose constraints only; no separate checkbox AC list |
