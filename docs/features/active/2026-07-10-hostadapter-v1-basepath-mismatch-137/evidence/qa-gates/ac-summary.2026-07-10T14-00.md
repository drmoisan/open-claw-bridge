Timestamp: 2026-07-10T14-00

Command: (cross-reference of AC-1 through AC-8 against their supporting evidence artifacts; no new command executed)

EXIT_CODE: 0

Output Summary: All 8 acceptance criteria for Issue #137 confirmed PASS.

| AC | Status | Supporting evidence |
|---|---|---|
| AC-1: `.env.example` default has no `/v1` segment | PASS | `git diff -- .env.example` (P2-T1/P2-T2) — one-line change, `/v1` removed |
| AC-2: `docker-compose.yml` (x2) and `docker-compose.dev.yml` defaults have no `/v1` segment | PASS | `git diff docker-compose.yml docker-compose.dev.yml` (P2-T3–P2-T5) — three one-line changes, `/v1` removed |
| AC-3: `Program.cs` blank-config fallback resolves to `http://host.docker.internal:4319/` | PASS | `git diff src/OpenClaw.Core/Program.cs` (P2-T6) — one-line change, trailing slash preserved, no `/v1` |
| AC-4: `Install.Preflight.psm1` default base URL has no `/v1` segment | PASS | `git diff scripts/Install.Preflight.psm1` (P2-T7) — one-line change |
| AC-5: PowerShell test asserts default preflight URL has no `/v1` segment | PASS | `tests/scripts/Install.Preflight.Tests.ps1` new `It` block (P1-T1); fails pre-fix (P1-T2, `evidence/regression-testing/ps-expect-fail.2026-07-10T12-45.md`); passes post-fix (P3-T1, `evidence/regression-testing/ps-post-fix-pass.2026-07-10T13-10.md`) |
| AC-6: C# test asserts resolved `HostAdapter.BaseUrl` fallback has no `/v1` segment | PASS | `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` (P1-T3); fails pre-fix (P1-T4, `evidence/regression-testing/csharp-expect-fail.2026-07-10T13-00.md`); passes post-fix (P3-T2, `evidence/regression-testing/csharp-post-fix-pass.2026-07-10T13-15.md`) |
| AC-7: `HostAdapterHttpClientTests.cs` continues to pass unchanged | PASS | All 19 tests pass (P3-T3, `evidence/regression-testing/hostadapterhttpclienttests-pass.2026-07-10T13-20.md`); `git diff` empty confirming no edits (P3-T4) |
| AC-8: Full PowerShell and C# toolchains pass with no coverage regression | PASS | `evidence/qa-gates/ac8-toolchain-summary.2026-07-10T13-55.md` (P5-T9), backed by `coverage-comparison-powershell.2026-07-10T13-50.md` and `coverage-comparison-csharp.2026-07-10T13-50.md` (P5-T7/P5-T8) |

All eight `- [ ]` checkboxes in `spec.md`'s `## Acceptance Criteria` section are being changed to `- [x]` with criterion text unchanged, in the same edit that produces this artifact.
