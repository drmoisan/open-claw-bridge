Timestamp: 2026-07-10T14-15

## PR Draft: Fix HostAdapter `/v1` base-path mismatch (Issue #137)

### Summary
- Every configured default for `OpenClaw__HostAdapter__BaseUrl` (repo `.env.example`, both `docker-compose*.yml` files, `OpenClaw.Core`'s `Program.cs` fallback, and `Install.Preflight.psm1`'s hardcoded default) appended a stray `/v1` path segment, but `OpenClaw.HostAdapter` has never mapped any route under a `/v1` prefix — it serves `/status`, `/users/{id}/messages`, etc. at the root. This broke the scripted installer's HostAdapter preflight check with a 404 and would equally break `OpenClaw.Core`'s real runtime calls to HostAdapter.
- This PR strips the stray `/v1` segment from all six consumer-side default locations and adds two regression tests pinning the corrected behavior.

### Files changed (5 tracked production/config files)
- `.env.example` (line 3)
- `docker-compose.yml` (lines 27 and 73, two occurrences)
- `docker-compose.dev.yml` (line 14)
- `src/OpenClaw.Core/Program.cs` (line 17)
- `scripts/Install.Preflight.psm1` (line 73)

### Test files added/extended (2)
- `tests/scripts/Install.Preflight.Tests.ps1` — new `It` block: "resolves the default (no OpenClaw__HostAdapter__BaseUrl key in EnvMap) to a URI with no /v1 segment (issue #137)".
- `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` (new file) — `BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment`. Added as a new sibling file rather than extending `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` (already at 616 lines, over the repository's 500-line cap); that file is unmodified and continues to pass all 19 of its tests unchanged.

### Non-goals confirmed unchanged
- `src/OpenClaw.Core/CoreOptions.cs` — byte-identical (`git diff` empty).
- `src/OpenClaw.HostAdapter/Program.cs` — byte-identical (`git diff` empty); no `/v1` routing was added; grep for `v1` under `src/OpenClaw.HostAdapter/*.cs` returns zero matches.

### Risks
- A partial fix (correcting some but not all six locations) would leave a residual mismatch; all six were addressed in this PR and independently verified via `git diff` per-file.
- `.env` (gitignored, untracked) is not part of this PR; operators receive the corrected default by re-copying `.env.example`.

### Validation performed
- Regression tests fail before the fix and pass after (expect-fail evidence captured for both the PowerShell and C# tests).
- `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` passes unchanged (19/19).
- Full PowerShell toolchain (PoshQC format → analyze → Pester via corrected-runsettings workaround) passes clean: 370/370 tests, 89.93% command/line coverage (no regression from the 89.93% baseline).
- Full C# toolchain (CSharpier → `dotnet build` → `dotnet test --collect:"XPlat Code Coverage"`) passes clean: `OpenClaw.Core.Tests` 931/931, `OpenClaw.HostAdapter.Tests` 100/100, `OpenClaw.MailBridge.Tests` 347/352 (5 skipped, pre-existing and unrelated). `OpenClaw.Core` coverage unchanged at 99.29% line / 92.28% branch; `Program.cs` remains 100%/100%.
- Links to QA-gate evidence:
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/final-poshqc-format.2026-07-10T13-40.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/final-poshqc-analyze.2026-07-10T13-42.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/final-poshqc-test.2026-07-10T13-45.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/final-csharp-format.2026-07-10T13-40.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/final-csharp-build.2026-07-10T13-42.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/final-csharp-test-coverage.2026-07-10T13-45.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/coverage-comparison-powershell.2026-07-10T13-50.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/coverage-comparison-csharp.2026-07-10T13-50.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/ac8-toolchain-summary.2026-07-10T13-55.md`
  - `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/qa-gates/ac-summary.2026-07-10T14-00.md`

### Outstanding follow-up (not yet performed)
- Manual/integration verification: publish a fresh bundle (`scripts/Publish.ps1`) and run the full `Install.ps1` flow end-to-end through the Docker stage (not `-SkipDocker`) with an operator `.env` left at its corrected defaults, confirming the preflight probe succeeds and the Docker stack starts. See `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/other/manual-verification-note.2026-07-10T13-25.md`. This is explicitly called out here as NOT yet performed.
