# Code Review: HostAdapter mailboxSettings and calendar/getSchedule (#74)

**Review Date:** 2026-06-13
**Reviewer:** feature-review agent
**Feature Folder:** `docs/features/active/hostadapter-mailboxsettings-getschedule-74`
**Feature Folder Selection Rule:** Suffix `-74` matches the canonical issue number and the primary changed scoping docs.
**Base Branch:** `main` (merge-base `48314bd6cb9c1a9f0bae4ca0c775a95ec52f3a61`)
**Head Branch:** `open-claw-bridge-wt-2026-06-13-10-28` (`bbdce9f26ba3913de677f38a1a9db54fc1deddd6`)
**Review Type:** Initial review

---

## Executive Summary

The branch adds two Microsoft Graph-shaped routes to `OpenClaw.HostAdapter`
(`GET /users/{id}/mailboxSettings`, config-sourced; and
`GET /users/{id}/calendar/getSchedule`, free/busy computed from bridge calendar data), extends
the `IHostAdapterClient` contract (T2) with two additive methods, implements them in
`HostAdapterHttpClient`, and replaces the two `NotSupportedException` stubs in
`HostAdapterSchedulingService` with delegating implementations. The three scheduling DTOs are
relocated from `OpenClaw.Core.Agent` to `OpenClaw.HostAdapter.Contracts` field-for-field, and
all referencing files updated with `using` additions. The change is additive and confined to
the scheduling read path; send-mail remains deferred to #75.

**What changed:** 16 C# source files (6 new, 8 modified, 2 deleted) plus 9 test files, README,
and api-reference. New pure projection (`FreeBusyProjection`), new route module
(`SchedulingRoutes`, extracted from `Program.cs` to respect the 500-line cap), and new config
binding (`MailboxSettingsOptions`). The reviewer re-ran the full toolchain: CSharpier clean,
build clean under analyzers + nullable-as-error, and 498/0/3 test pass/fail/skip with
changed-code coverage at 100% line/branch.

**Top 3 risks:**
1. The Core-side `HostAdapterSchedulingService` now silently degrades to defaults (mailbox) or
   an empty busy grid (free/busy) when the envelope is not OK. This is intentional and
   consistent with `GetCalendarViewAsync`, but it means a downstream outage is masked from the
   `SlotProposer` rather than surfaced. Low risk; documented in code and tested.
2. The architecture-boundary test was relaxed (prefix ban removed) and replaced with a
   reflection-based guard. Assessed as legitimate (Section 8), but it is a guard change and
   warrants the explicit record.
3. `getSchedule` window/limit validation depends on the shared
   `HostAdapterRequestValidation` helpers; correctness relies on those pre-existing helpers,
   which are exercised by `HostAdapterValidationTests`.

**PR readiness recommendation:** **Go** — All toolchain stages pass, coverage thresholds are
met on changed code with no regression, the relocation is exact and additive, and the
architecture-test change preserves the guard's protective intent.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Info | `tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs` | lines 1-162 (diff) | The outright `"OpenClaw.HostAdapter"` prefix ban was removed because it prefix-matched the permitted `OpenClaw.HostAdapter.Contracts` edge (architecture Rule 6); a new reflection-based test now bans only the host implementation. | None required. Keep the new positive guard. | The change is required by AC5/Design A and preserves the protective intent (host-impl/COM still banned). | `git diff` of file; passing run of `DeterministicSurface_DoesNotDependOnHostAdapterHostImplementation`; `.claude/rules/architecture-boundaries.md` Rule 6 |
| Info | `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` | `GetMailboxSettingsAsync`, `GetFreeBusyAsync` | Graceful degradation returns documented defaults / empty grid on non-OK envelope instead of propagating an error. | Confirm this matches the intended pipeline contract (it mirrors `GetCalendarViewAsync`). | A masked downstream failure could let the SlotProposer treat unavailable data as "fully free." Acceptable for Stage 0 and tested. | `HostAdapterSchedulingServiceTests.GetFreeBusyAsync_WhenEnvelopeNotOk_ReturnsEmptyIntervals` |
| Nit | `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/coverage-delta.md` | changed-code table | Table lists every changed file at 100% but omits `HostAdapterOptions.cs` (90.47% line / 50% branch at class level). | Optionally add the row for completeness. | Changed lines are covered and thresholds met; the omission is documentation-only. | Reviewer-regenerated `coverage.cobertura.xml` class-level rates |

No Blocker or Major findings.

---

## Implementation Audit

### C# implementation audit

#### What changed well

- `FreeBusyProjection` is a small, pure, deterministic static projection with explicit
  null-guards and a documented conservative default (null `BusyStatus` treated as busy). It has
  no clock or I/O dependency, which makes it trivially testable and Graph-portable.
- `SchedulingRoutes` was extracted from `Program.cs` specifically to keep that file under the
  500-line cap (435 lines post-change), and it reuses the established route lifecycle
  (`RequireReadyBridgeAsync`, `HostAdapterRequestValidation`, `HostAdapterResponses`,
  `ToHttpResult`). This follows the existing pattern rather than inventing a new one.
- The `IHostAdapterClient` additions are additive with keyword-style optional parameters and
  carry thorough XML docs, including the D2 portability rationale for the typed-window
  signature. The DTO relocation is field-for-field identical to the deleted Core records
  (verified against the baseline blobs), so it is a pure move plus namespace change.
- The mailbox route correctly does not call `RequireReadyBridgeAsync` or the process runner,
  because the data is config-sourced — matching the spec's stated behavior.

#### Type safety and API notes

- Builds clean under `-p:TreatWarningsAsErrors=true`; nullable flow is satisfied.
- DTOs are `public sealed record`; helpers are `internal`. Public surface is intentional and
  minimal.
- Config parsing (`TryParseWorkingDays`, `TryParseTime`) uses `TryParse` with
  `CultureInfo.InvariantCulture` and returns a typed configuration error envelope on failure
  rather than throwing.

#### Error handling and logging

- Downstream calendar-fetch failures are propagated as a re-typed failure envelope preserving
  the original error code/message/bridge metadata, rather than swallowed or rethrown as a raw
  exception.
- The Core-side service uses graceful degradation (Info finding above) consistent with the
  sibling `GetCalendarViewAsync`; `SendMailAsync` retains an explicit `NotSupportedException`
  for the #75-deferred path, with a test asserting it.

---

## Test Quality Audit

The reviewer re-ran the full suite with coverage. New and extended tests cover positive,
negative, and edge scenarios for the projection, the route handlers, the client path
construction, the Core delegation (including degradation), config parsing, window validation,
and the DTO JSON round-trips. Coverage on changed files is 100% line / 100% branch (except
`HostAdapterOptions.cs`, whose changed lines are covered).

### Reviewed test and QA artifacts

- `tests/OpenClaw.HostAdapter.Tests/FreeBusyProjectionTests.cs` — verifies the pure projection across busy/tentative/OOF/free/null/empty cases; deterministic, no clock.
- `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — verifies delegation and degradation; uses `FakeTimeProvider` to derive windows and Moq for the client seam.
- `tests/OpenClaw.HostAdapter.Tests/HostAdapterEndpointTests.cs` and `HostAdapterValidationTests.cs` — route happy-path and validation cases via `HostAdapterTestWebApplicationFactory` with a stubbed `IHostAdapterProcessRunner`.
- `tests/OpenClaw.Core.Tests/Agent/SchedulingDtoContractTests.cs` — `ApiEnvelope<MailboxSettingsDto>` / `ApiEnvelope<FreeBusyScheduleDto>` JSON round-trips.
- `evidence/qa-gates/coverage-delta.md` and reviewer-regenerated `coverage.cobertura.xml` — coverage evidence (independently confirmed).

### Quality assessment prompts

- **Determinism:** `FakeTimeProvider` for window derivation; projection has no wall-clock dependency; no `Thread.Sleep`/`Task.Delay`.
- **Isolation:** Each test targets one behavior with per-test mocks.
- **Speed:** HostAdapter.Tests 618 ms; Core.Tests ~1 s.
- **Diagnostics:** FluentAssertions provide actionable failure messages.

---

## Security / Correctness Checks

| Check | Status | Evidence |
|---|---|---|
| No secrets in code | ✅ PASS | Inspected new files; no credentials or tokens. |
| No unsafe subprocess or command construction | ✅ PASS | Calendar fetch uses the existing `HostAdapterCommandBuilder` + `IHostAdapterProcessRunner` seam; no shell string concatenation. |
| Input validation at boundaries | ✅ PASS | `getSchedule` validates start/end timestamps, window, and `$top` limit via `HostAdapterRequestValidation`; mailbox route validates working-day names and HH:mm times. |
| Error handling remains explicit | ✅ PASS | Downstream failures re-typed into failure envelopes; deferred path throws explicitly. |
| Configuration / path handling is safe | ✅ PASS | `Uri.EscapeDataString` applied to mailbox id and ISO-8601 query values in the client. |

---

## Research Log

No external research was required. Assessment is based on the branch diff, the repository
architecture and quality-tier rules, the feature scoping docs, the feature evidence artifacts,
and the reviewer's independent toolchain run.

---

## Verdict

The change is ready for normal PR flow. The implementation is simple, reuses established
patterns, and is well-tested with changed-code coverage at the policy thresholds and no
regression. The contract change is additive at the T2 boundary, and the DTO relocation is an
exact move consistent with architecture Rule 6. The architecture-boundary test relaxation is a
legitimate correction of an over-broad prefix ban and is replaced by a stricter reflection-based
guard that still forbids dependence on the HostAdapter host implementation and the COM layer.
The two Info findings (graceful degradation behavior, coverage-delta table omission) and one
Nit are non-blocking. Recommendation: **Go.**
