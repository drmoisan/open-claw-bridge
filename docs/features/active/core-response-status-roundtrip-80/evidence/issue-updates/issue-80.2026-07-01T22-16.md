# Issue Update Mirror — Issue #80

Timestamp: 2026-07-02T11-35
PostedAs: unknown

POSTING BLOCKED: The executing agent does not commit or post to GitHub per the delegation
directive ("Do not commit; the orchestrator handles commits"). This mirror records the exact
update text for the orchestrator to post to GitHub issue #80.

## Update Text (AC-to-Evidence Mapping)

All five acceptance criteria for issue #80 are satisfied and checked off in `issue.md`, `spec.md`,
and `user-story.md` under `docs/features/active/core-response-status-roundtrip-80/`.

| AC | Criterion (abbreviated) | Evidence |
|---|---|---|
| AC-1 | `response_status INTEGER NULL` in `CreateTablesSql` and idempotent guarded ALTER; double `InitializeAsync` raises no error | `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (DDL line 89, guarded ALTER lines 135-141); evidence/regression-testing/regression-pass-after.2026-07-01T22-16.md (test 3 passes: migration + idempotency) |
| AC-2 | `ResponseStatus` survives Core write/read round-trip for 4 (Declined) and null (not 0) | evidence/regression-testing/regression-pass-after.2026-07-01T22-16.md (tests 1 and 2 pass) |
| AC-3 | No behavior change for other `EventDto` fields; all existing `tests/OpenClaw.Core.Tests/` pass unchanged | evidence/qa-gates/final-test-coverage.2026-07-01T22-16.md (all 210 pre-existing Core tests pass; 590 passed / 0 failed solution-wide) |
| AC-4 | Regression test fails before the fix and passes after | evidence/regression-testing/regression-fail-before.2026-07-01T22-16.md (EXIT 1, tests 1 and 3 fail with null-vs-4) and regression-pass-after.2026-07-01T22-16.md (EXIT 0, 3/3 pass) |
| AC-5 | Full C# toolchain passes; line >= 85%, branch >= 75%, changed lines covered | evidence/qa-gates/final-csharpier.2026-07-01T22-16.md (EXIT 0), final-build.2026-07-01T22-16.md (0 warnings/errors), final-test-coverage.2026-07-01T22-16.md (pooled 90.26% line / 79.36% branch; OpenClaw.Core 98.61% / 91.68%), evidence/coverage/coverage-delta.2026-07-01T22-16.md (disposition PASS, changed-lines findings) |

Changed files: `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`,
`src/OpenClaw.Core/CoreCacheRepository.Events.cs`, new
`tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs`. No other production or test
file modified.
