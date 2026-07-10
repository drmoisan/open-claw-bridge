Timestamp: 2026-07-10T14-10

PostedAs: comment

GitHub URL: https://github.com/drmoisan/open-claw-bridge/issues/137#issuecomment-4936244260

Exact text posted:

---

Fix implemented for the HostAdapter `/v1` base-path mismatch.

**Summary:** Stripped the stray `/v1` path segment from six consumer-side defaults for `OpenClaw__HostAdapter__BaseUrl`, since `OpenClaw.HostAdapter` has never routed under a `/v1` prefix (it serves `/status`, `/users/{id}/messages`, etc. at root scope).

**Files changed (6 locations across 5 files):**
- `.env.example` (line 3)
- `docker-compose.yml` (lines 27 and 73)
- `docker-compose.dev.yml` (line 14)
- `src/OpenClaw.Core/Program.cs` (line 17, blank-config `PostConfigure` fallback)
- `scripts/Install.Preflight.psm1` (line 73, `Get-HostAdapterPreflightUri` default)

**New regression tests (2):**
- PowerShell: new `It` block in `tests/scripts/Install.Preflight.Tests.ps1` asserting the default preflight URL has no `/v1` segment (fails before fix, passes after).
- C#: new sibling file `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` asserting the resolved blank-config fallback has no `/v1` segment (fails before fix, passes after). Added as a new file rather than extending `HostAdapterHttpClientTests.cs`, which is already over the repository's 500-line cap and was left byte-identical (all 19 of its tests continue to pass unchanged).

**Toolchain status:** Full PowerShell toolchain (PoshQC format → analyze → Pester) and full C# toolchain (CSharpier → build/analyzers/nullable → dotnet test) both pass in a single clean pass. No coverage regression: PowerShell command/line coverage held at 89.93% (369→370 tests passed); `OpenClaw.Core` C# coverage held at 99.29% line / 92.28% branch (930→931 tests passed); `Program.cs` remains at 100%/100% line/branch.

**Not yet performed:** a manual/integration end-to-end verification (publish a fresh bundle, run `Install.ps1` through the Docker stage with an operator `.env` at corrected defaults) is documented as an outstanding follow-up, not automated in this pass.

Non-goals confirmed unchanged: `src/OpenClaw.Core/CoreOptions.cs` and `src/OpenClaw.HostAdapter/Program.cs` are byte-identical to their pre-fix state; no `/v1` routing was added to HostAdapter.

---
