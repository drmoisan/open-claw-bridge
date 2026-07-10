# Feature (Acceptance Criteria) Audit — Issue #137 (hostadapter-v1-basepath-mismatch)

- Reviewed: 2026-07-10T15-10
- Work mode: `full-bug`
- AC source (authoritative): `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/spec.md`, `## Acceptance Criteria` section
- Base: `main` @ `4ce19d186e98c2697eee07ba8a7866ce10af08a0`; Head: `b33db1867ce009a79a77df7e92363c86821ea764`

## Evaluation Table

| AC | Criterion | Verdict | Evidence |
|---|---|---|---|
| AC-1 | `.env.example`'s default `OpenClaw__HostAdapter__BaseUrl` (line 3) has no `/v1` segment. | **PASS** | Direct diff verification: `.env.example:3` now reads `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319` (confirmed via `git diff`). |
| AC-2 | Both `docker-compose.yml` occurrences (lines 27 and 73) and the `docker-compose.dev.yml` occurrence (line 14) default `OpenClaw__HostAdapter__BaseUrl` have no `/v1` segment. | **PASS** | Direct diff verification: all three occurrences confirmed changed to `...:-http://host.docker.internal:4319}` with no `/v1`; no other lines in either file differ. |
| AC-3 | `src/OpenClaw.Core/Program.cs`'s blank-config fallback (line 17) resolves to `http://host.docker.internal:4319/` (no `/v1`, trailing slash preserved). | **PASS** | Direct diff verification: line 17 now reads `"http://host.docker.internal:4319/"`, exact match to the required value; trailing slash preserved. |
| AC-4 | `scripts/Install.Preflight.psm1`'s default base URL (line 73) has no `/v1` segment. | **PASS** | Direct diff verification: line 73 now reads `$baseUrl = 'http://host.docker.internal:4319'`. |
| AC-5 | A PowerShell test asserts the `Install.Preflight` default preflight URL contains no `/v1` segment. | **PASS** | New `It` block in `tests/scripts/Install.Preflight.Tests.ps1` (`Describe 'Get-HostAdapterPreflightUri default base URL'`) asserts `$uri.AbsolutePath | Should -Not -Match '/v1'`. Confirmed fails pre-fix (`evidence/regression-testing/ps-expect-fail.2026-07-10T12-45.md`, exit 1) and passes post-fix (`evidence/regression-testing/ps-post-fix-pass.2026-07-10T13-10.md`, exit 0). |
| AC-6 | A C# test asserts `OpenClaw.Core`'s resolved `HostAdapter.BaseUrl` fallback (blank config) contains no `/v1` segment. | **PASS** | New file `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs`, method `BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment`, asserts `resolvedBaseUrl.Should().NotContain("/v1")`. Confirmed fails pre-fix (`evidence/regression-testing/csharp-expect-fail.2026-07-10T13-00.md`, exit 1, quoted failure message showing `.../v1/` present) and passes post-fix (`evidence/regression-testing/csharp-post-fix-pass.2026-07-10T13-15.md`, exit 0). |
| AC-7 | `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` continues to pass unchanged. | **PASS** | `git diff` for this file returns no output (byte-identical to base). Targeted run confirms all 19 tests pass (`evidence/regression-testing/hostadapterhttpclienttests-pass.2026-07-10T13-20.md`, exit 0). |
| AC-8 | Full PowerShell toolchain (PoshQC format → analyze → Pester) and C# toolchain (CSharpier → analyzers/nullable → xUnit/MSTest) pass with no coverage regression on changed lines. | **PASS** | All eight P5 toolchain steps report `EXIT_CODE: 0` in a single clean pass (one earlier restart was triggered by CSharpier flagging the new test file and was resolved per the mandatory-restart policy; the recorded final artifacts are from the subsequent clean pass) — see `evidence/qa-gates/ac8-toolchain-summary.2026-07-10T13-55.md`. No coverage regression: PowerShell 89.93% → 89.93% (`evidence/qa-gates/coverage-comparison-powershell.2026-07-10T13-50.md`); C# `OpenClaw.Core` 99.29%/92.28% → 99.29%/92.28% (`evidence/qa-gates/coverage-comparison-csharp.2026-07-10T13-50.md`). |

## Root-Cause / Non-Goal Verification

- Root cause matches spec: `OpenClaw.HostAdapter`'s `Program.cs` has never mapped any `/v1`-prefixed route; the six consumer-side defaults were the sole defect. Confirmed via `git diff` on `src/OpenClaw.HostAdapter/Program.cs` (empty) and the branch's own grep confirmation (zero `/v1` matches under `src/OpenClaw.HostAdapter/`).
- Non-goal 1 (do not add `/v1` routing to HostAdapter): honored — no changes to `src/OpenClaw.HostAdapter/Program.cs`.
- Non-goal 2 (do not modify `CoreOptions.cs`): honored — `git diff` empty for this file.
- Non-goal 3 (do not weaken `HostAdapterHttpClientTests.cs`): honored — file unchanged, all 19 tests still pass.
- Excluded system (operator-local `.env`, gitignored): correctly out of scope; not part of the diff, consistent with spec's stated rationale (fixed transitively via `.env.example`).

## Outstanding / Non-AC Follow-up (disclosed, not blocking)

- Manual/integration verification (publish a fresh bundle, run `Install.ps1` end-to-end through the Docker stage) is explicitly NOT performed in this pass, and is explicitly NOT one of the eight acceptance criteria in `spec.md`. It is disclosed transparently in `evidence/other/manual-verification-note.2026-07-10T13-25.md` and `evidence/other/pr-notes.2026-07-10T14-15.md` as a required future follow-up. This does not block the AC verdicts above since it is outside the AC set, but it should be tracked as a post-merge action item.

## AC Check-Off Verification

All eight `- [ ]` checkboxes in `spec.md`'s `## Acceptance Criteria` section were already changed to `- [x]` by the branch itself (`evidence/qa-gates/ac-summary.2026-07-10T14-00.md` documents this cross-reference), with criterion text unchanged. Independent re-verification against each item's supporting evidence (table above) confirms every check-off is warranted; no reviewer-side check-off changes were required.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/spec.md`
- Total AC items: 8
- Checked off (delivered): 8
- Remaining (unchecked): 0
- Items remaining: none

## Overall Feature Audit Verdict

**PASS.** All 8 acceptance criteria are verified PASS against independently reviewed evidence (direct diffs, targeted test runs, and coverage comparisons), all three explicit non-goals are honored, and the root cause matches the spec's root-cause analysis. No remediation is required.
