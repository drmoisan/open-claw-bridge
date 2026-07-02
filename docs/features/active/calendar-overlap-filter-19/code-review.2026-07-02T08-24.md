# Code Review: calendar-overlap-filter (#19)

**Review Date:** 2026-07-02
**Branch:** `bug/calendar-overlap-filter-19` @ `d7fc69a31b441c9a5d98abf693ef6d00916134e1`
**Base:** `main` @ merge-base `1bc4148867bd757b724af503b59a3a19bc6f37b4`
**Scope:** Full feature-vs-base branch diff. C# surface: `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs` (modified, one expression body), `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs` (new, 186 lines). Markdown surface: feature folder docs/evidence for issue #19 and two `prd-feature` agent-memory files.

---

## Executive Summary

This is a minimal, correctly targeted bugfix. The defective start-only Restrict predicate (`[Start] >= windowStart AND [Start] < windowEnd`) is replaced with the standard interval-overlap predicate (`[Start] < windowEnd AND [End] > windowStart`) in a single expression body. The strict `<`/`>` operators encode the required boundary semantics directly (an event with `End == windowStart` or `Start == windowEnd` is excluded), so no additional logic was needed. The `LocalDateTime` conversion and `MM/dd/yyyy hh:mm tt` format established by the issue #55 fix are preserved character-for-character, and the surrounding scan orchestration in `OutlookScanner.cs` (`Sort("[Start]")`, `IncludeRecurrences = true`, then `Restrict`) is untouched — verified by diff inspection, not just by evidence assertion.

The new test class is well constructed. It captures the actual filter string emitted through `ScanCalendarAsync` using the established fake-COM doubles (no live Outlook dependency), pins the exact string with invariant formatting, and then evaluates event membership against the emitted filter with a small clause parser covering all five interval-boundary scenarios. The evaluator deliberately supports `>=`/`<=` in addition to `<`/`>`, which is what allowed the fail-before run to evaluate the old predicate and prove that exactly the in-progress and window-spanning rows fail under it — a high-quality discriminating regression design.

The reviewer independently re-ran the full toolchain at branch head (format, build with analyzers/nullable as errors, architecture tests, full solution test suite with coverage): all clean, 596/596 runnable tests passing. Per-file coverage was re-measured from fresh cobertura: the modified file is at 100% line / 100% branch with the changed line covered 56 times.

No Blocking or Major findings. One Minor pre-existing observation (host-culture sensitivity of Restrict-boundary formatting) and two Informational notes are recorded below; none require action on this branch.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|----------|------|----------|---------|----------------|-----------|----------|
| Minor | src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs | `BuildCalendarFilter` (line 49) and pre-existing `BuildInboxFilter` (line 45) | Restrict boundaries are rendered via string interpolation, which formats with the current culture; on a non-en-US host the `MM/dd/yyyy hh:mm tt` custom format would emit culture-specific separators/AM-PM designators, producing a filter Outlook may not parse and diverging from the test's `CultureInfo.InvariantCulture` expectation. | No action on this branch (the spec explicitly required preserving the existing formatting exactly, and the defect fix must stay minimal). File a follow-up to render Restrict boundaries invariantly (e.g. `FormattableString.Invariant`) in both builders and align the test expectation. | Pre-existing pattern in both builders that predates this branch; the branch neither introduced nor worsened it, and dev/CI hosts are en-US, so behavior and test determinism are unaffected in practice. | Production interpolation at Helpers.cs lines 45 and 49; test expected-string construction with `string.Create(CultureInfo.InvariantCulture, ...)` at test file lines 75-78. |
| Info | tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs | `EvaluateClause` (lines 156-185) | The clause evaluator supports `>=` and `<=` operators that the fixed filter never emits. | None — keep as is. | The extra operators are load-bearing for the regression contract: the fail-before run evaluates the pre-fix predicate, which uses `>=`. Removing them would break the fail-before discrimination property. | `evidence/regression-testing/regression-fail-before.2026-07-02T07-59.md` shows the DataRow tests evaluating the old `>=` predicate. |
| Info | .claude/agent-memory/prd-feature/MEMORY.md, .claude/agent-memory/prd-feature/project_test_framework_discrepancy.md | branch diff | Two agent-memory files from the `prd-feature` agent ride on this bugfix branch (recording the repo's MSTest-vs-xUnit rule divergence). | None — agent memory is version-controlled by design in this repo; the content is accurate bookkeeping. | The executor's scope-verification artifact explicitly identifies these as writes from a different agent, not part of the fix scope; they contain no code or policy changes. | `evidence/qa-gates/scope-verification.2026-07-02T08-04.md`; diff hunks for both files. |

No Blocking or Major findings.

---

## Implementation Audit

### C# implementation audit

#### What changed well

- **Correct predicate, minimal diff.** The interval-overlap membership test (`Start < windowEnd AND End > windowStart`) is the canonical form, and the strict operators yield the specified boundary semantics without extra conditionals. The production diff is exactly one line (+1/-1); `git diff` confirms no other production change anywhere in the branch.
- **Invariants preserved verbatim.** The `LocalDateTime` conversion, the `MM/dd/yyyy hh:mm tt` format string, the `private` method visibility, and the method signature are all unchanged; the issue #55 `GetOptionalUtcDateTimeOffset` normalization path is untouched. `ScanCalendarAsync`'s `Sort`/`IncludeRecurrences`/`Restrict` ordering is unchanged (read directly at `OutlookScanner.cs` lines ~278-284).
- **No scope creep.** `BuildInboxFilter`, the cache schema, `FreeBusyProjection`, and HostAdapter routes — all named non-goals in the spec — are untouched. The spec's optional `internal`-visibility seam was correctly declined in favor of asserting the captured filter through the existing fakes, keeping the public/internal surface identical.
- **Recurrence escape hatch handled by decision, not code.** The plan records an explicit stop-and-report rule if a recurring-occurrence edge case had surfaced, rather than speculatively adding a post-filter pass. None surfaced; none was added.

#### Type safety and API notes

- No public API surface change; `OutlookScanner` remains `internal sealed partial` and `BuildCalendarFilter` remains `private`.
- No suppressions, pragmas, or analyzer-configuration changes anywhere in the diff; build is clean with warnings-as-errors.

#### Error handling and logging

- No new production error paths; the existing null-check on the restricted items collection in `ScanCalendarAsync` is sufficient and unchanged, as the spec specified.

---

## Test Quality Audit

### Reviewed test and QA artifacts

- `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs` (new, 186 lines, 6 tests) — read in full.
- `evidence/regression-testing/regression-fail-before.2026-07-02T07-59.md` — EXIT 1; exactly the 3 predicted tests fail against the unfixed predicate (exact-string test, in-progress row, window-spanning row) while the fully-within and both boundary-exclusion rows pass. This is the correct discrimination signature: the old predicate genuinely satisfies those three cases, so their pre-fix passes are expected, and the artifact says so explicitly.
- `evidence/regression-testing/regression-pass-after.2026-07-02T08-00.md` — EXIT 0; 6/6 pass.
- `evidence/regression-testing/full-suite-after-fix.2026-07-02T08-00.md` — 596 passed / 0 failed / 5 skipped; no existing test file modified.
- Reviewer re-run at head: full solution 596 passed / 0 failed / 5 pre-existing environment-gated skips; architecture subset 2/2.

### Quality assessment

- **Assertion strength:** The primary test pins the exact filter string (`Should().Be(expected)`), not a substring — the strongest possible assertion for a string-builder fix. Membership rows assert both inclusion and exclusion with scenario messages.
- **Independence/isolation:** Each test call builds a fresh fake graph via `CaptureEmittedFilterAsync`; no static mutable state beyond the immutable `FixedNow`.
- **Determinism:** Fixed injected clock through the existing `() => FixedNow` constructor seam; no wall clock, sleeps, timers, network, filesystem, or temporary files. (See the Minor finding for the theoretical host-culture caveat, inapplicable to this repo's en-US environments.)
- **AAA structure and docs:** Explicit Arrange/Act/Assert comments in both tests; XML docs on the class, tests, and all three helpers, including the window-arithmetic explanation for the DataRow offsets (37-day window).
- **Fail-fast evaluator:** `EvaluateClause` throws on unsupported fields/operators instead of returning a default — a malformed future filter cannot silently pass the membership test.
- **Convention match:** Mirrors the `OutlookScannerCalendarUtcTests` pattern (fixed clock seam, `FakeComActiveObject`, default-folder wiring, `FakeOutlookItems.LastFilter` capture) as the plan required; MSTest + FluentAssertions per repo convention.

---

## Security / Correctness Checks

- **Filter-string injection:** Inputs to `BuildCalendarFilter` are `DateTimeOffset` values computed internally from settings; no user-controlled text enters the Restrict string. Unchanged risk posture.
- **Behavioral compatibility:** The corrected predicate's result set is a strict superset of the old one (every event with `Start` in the window also satisfies overlap), so no previously included event is lost — matching the spec's backward-compatibility claim; the full pre-existing suite passing unchanged corroborates it.
- **No secrets, no new dependencies, no configuration changes** in the diff.
- **Banned APIs:** No `DateTime.Now`/`UtcNow`, `Random.Shared`, `Thread.Sleep`, or `Task.Delay` introduced.
- **Architecture:** COM interop remains confined to `OpenClaw.MailBridge`; NetArchTest suite passes (2/2, reviewer run).

---

## Research Log

- Read the full production diff and the complete new test file; confirmed the production change is exactly the one-line expression-body replacement.
- Read `OutlookScanner.cs` around the `Restrict` call to confirm `Sort("[Start]")` and `IncludeRecurrences = true` ordering is unchanged.
- Confirmed `FakeOutlookItems.LastFilter` capture exists in `MailBridgeRuntimeTestDoubles.cs` (lines 74-80) and is the established capture point used by prior calendar tests.
- Re-measured per-file coverage from fresh cobertura at head: `OutlookScanner.Helpers.cs` 100% line / 100% branch; changed line 49 hits 56 (per-file re-measurement done deliberately rather than trusting project aggregates).
- Verified `mailbridge.runsettings` excludes test assemblies (`[*.Tests]*`) from coverage measurement per policy, and that no production path is excluded.
- Confirmed no suppression attributes, pragmas, or `.editorconfig`/props changes anywhere in the diff, and no workflow/benchmark paths (rule `modified-workflow-needs-green-run` not triggered).

---

## Verdict

**Go.** No Blocking or Major findings. One Minor pre-existing observation (culture-dependent Restrict formatting, recommended as a follow-up issue) and two no-action Informational notes. The fix is the smallest correct change, the regression tests discriminate the defect precisely, and all toolchain results were independently re-verified at branch head.
