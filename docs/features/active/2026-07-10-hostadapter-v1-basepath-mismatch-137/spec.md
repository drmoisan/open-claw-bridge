# hostadapter-v1-basepath-mismatch (Spec)

- **Issue:** #137
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-10T11-30
- **Status:** Spec Complete — Ready for Planning
- **Version:** 0.2

## Context
Every configured default for `OpenClaw__HostAdapter__BaseUrl` (repo `.env`, `.env.example`, both `docker-compose*.yml` files, `OpenClaw.Core`'s `Program.cs` fallback, and `Install.Preflight.psm1`'s hardcoded default) appends a `/v1` path segment, but `OpenClaw.HostAdapter`'s `Program.cs` has never mapped any route under a `/v1` prefix — it serves `/status`, `/users/{id}/messages`, etc. at the root. This breaks the scripted installer's HostAdapter preflight check with a 404, and would equally break `OpenClaw.Core`'s real runtime calls to HostAdapter once the Docker container is up, since `HostAdapterHttpClient.GetStatusAsync` requests the relative path `"status"` against that same `/v1`-suffixed base address.

Environment:
- OS/version: Windows 11 Pro
- .NET version: 10.0.201 (per `global.json`)
- Command/flags used: `.\Install.ps1 -DockerEnvFilePath (Join-Path $operatorConfig '.env') -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')` from a published bundle (e.g. `artifacts\publish\1.0.2.2\`)
- Data source or fixture: operator `.env` copied verbatim from the bundle's `docker\.env.example`, which mirrors the repo-root `.env.example`

Impact / Severity:
- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Blocks the entire scripted install (Step 3) for any operator who has not manually edited the operator `.env` to remove the stray `/v1` segment; also affects the live Docker stage's real HostAdapter calls, not just the installer.


## Repro & Evidence
Steps to Reproduce:
1. Publish and install per the README's Step 1-3 flow (`scripts/Publish.ps1`, then `Install.ps1` with `-DockerEnvFilePath`/`-AnthropicEnvFilePath`) with an operator `.env` whose `OpenClaw__HostAdapter__BaseUrl` is left at its default value.
2. `Install.ps1` starts `OpenClaw.HostAdapter` (confirmed listening on `http://127.0.0.1:4319`) and then runs its HostAdapter preflight probe before starting the Docker stage.
3. The preflight probe requests `GET http://127.0.0.1:4319/v1/status`.

Expected:
The preflight probe requests a URL that HostAdapter actually serves (`/status`), receives a 200/valid envelope, and the installer proceeds to the Docker stage.

Actual:
`GET http://127.0.0.1:4319/v1/status` returns HTTP 404 (confirmed in the HostAdapter's own request log: `HostAdapter request ... /v1/status completed with 404`). `Install.ps1` throws: "HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 404. ..." and the install stops before the Docker stage.

Logs / Screenshots:
- [x] Attached minimal logs or screenshot
- Snippet:
  ```
  info: OpenClaw.HostAdapter.RequestLoggingMiddleware[0]
        HostAdapter request c3f7364e-bcd4-4eb2-9d9b-51de56a32048 /v1/status completed with 404 in 2 ms. BridgeState=unknown; BridgeErrorCode=none; CliExitCode=(null)
  Exception: HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 404. Confirm
  OpenClaw.HostAdapter is running, the token is valid, and OpenClaw.MailBridge is running, then retry; or pass
  -SkipDocker to skip the container stage.
  ```


## Scope & Non-Goals
- In scope:
  - Strip the stray `/v1` path segment from the tracked consumer defaults for `OpenClaw__HostAdapter__BaseUrl`:
    - `.env.example` (line 3)
    - `docker-compose.yml` (two occurrences, lines 27 and 73)
    - `docker-compose.dev.yml` (line 14)
    - `src/OpenClaw.Core/Program.cs` hardcoded blank-config fallback (line 17)
    - `scripts/Install.Preflight.psm1` default base URL (line 73, inside `Get-HostAdapterPreflightUri`)
  - Add a PowerShell regression test (in `Install.Preflight.Tests.ps1` or `Install.Tests.ps1`) asserting the default preflight URL has no `/v1` segment.
  - Add a C# regression test (in `OpenClaw.Core.Tests`) asserting `OpenClaw.Core`'s resolved `HostAdapter.BaseUrl` fallback (blank config) contains no `/v1` segment.
- Out of scope / non-goals:
  - Do NOT add `/v1` routing to `OpenClaw.HostAdapter`. Its route surface (`/status`, `/users/{id}/messages`, etc., all root-scoped) is the correct, intended contract; the defect is in the six consumer-side defaults, not in HostAdapter's routing.
  - Do NOT modify `src/OpenClaw.Core/CoreOptions.cs`. Its class-level default (`"http://host.docker.internal:4319/"`) is already correct with no `/v1` segment.
  - Do NOT weaken or alter the existing assertions in `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`. That file already reflects the corrected contract (`BaseAddress` at host:port root, relative paths with no `/v1`) and must continue to pass unchanged.
- Explicitly excluded systems, integrations, or datasets:
  - The operator-local `.env` file. It is gitignored (`.gitignore:67-69`) and not a repository artifact, so it is out of PR scope. It is fixed transitively: operators provision `.env` by copying `.env.example`, so once the template is corrected, any re-copy of `.env.example` yields the fixed default. A pre-existing operator `.env` with the stray `/v1` is stale local data, not a defect this PR fixes directly.

## Root Cause Analysis
`src/OpenClaw.HostAdapter/Program.cs` has never registered a `/v1`-prefixed route (confirmed via `git log -S'"/v1'` against the file: no match, no controllers, purely minimal-API `app.MapGet` calls at root scope: `/status`, `/users/{id}/messages`, and others). The `/v1` segment is baked into six defaults instead:

- `.env:1` and `.env.example:3` — `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319/v1`
- `docker-compose.yml:27,73` and `docker-compose.dev.yml:14` — `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}`
- `src/OpenClaw.Core/Program.cs:17` — hardcoded `"http://host.docker.internal:4319/v1/"` fallback when config is blank (note: `CoreOptions.cs:16`'s class-level default, `"http://host.docker.internal:4319/"`, is already correct with no `/v1` — only the `Program.cs` fallback string is wrong)
- `scripts/Install.Preflight.psm1:73` — `$baseUrl = 'http://host.docker.internal:4319/v1'` (default when the operator `.env` map lacks the key)

`tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` uses a `BaseAddress` of `http://localhost:4319/` (no `/v1`) and asserts the captured path ends with `/status` — this is the tested, intended contract, confirming the `/v1` segment in the six defaults above is the stray value to remove, not a missing route to add to HostAdapter.


## Proposed Fix

### Design summary (what changes where):
Remove the stray `/v1` path segment from six consumer-side default values for `OpenClaw__HostAdapter__BaseUrl`, so every default resolves to HostAdapter's actual root-scoped route surface. No routing, DTO, or business-logic change is involved; this is a string-literal correction repeated across configuration templates, a hardcoded fallback, and an installer default, plus new regression coverage that pins the corrected values.

### Boundaries and invariants to preserve:
- `HostAdapterHttpClient` (`src/OpenClaw.Core/HostAdapterHttpClient.cs`) continues to send relative request paths with no leading slash and no `/v1` segment (e.g., `"status"`, `"users/{id}/messages"`); this behavior is unchanged and already correct.
- `CoreOptions.cs`'s `HostAdapterOptions.BaseUrl` class-level default remains `"http://host.docker.internal:4319/"` (trailing slash, no `/v1`) — unchanged.
- The trailing-slash convention on `BaseUrl` values is preserved wherever it currently exists (e.g., `Program.cs`'s fallback), matching the `EnsureTrailingSlash` invariant used elsewhere in that file.

### Dependencies or blocked work:
None. All six edits are independent string replacements; no sequencing dependency exists between them.

### Implementation strategy (what changes, not sequencing):

#### Files/modules to change:
- `.env.example` (line 3)
- `docker-compose.yml` (lines 27 and 73)
- `docker-compose.dev.yml` (line 14)
- `src/OpenClaw.Core/Program.cs` (line 17)
- `scripts/Install.Preflight.psm1` (line 73, inside `Get-HostAdapterPreflightUri`)
- New/extended test files: `tests/scripts/powershell/Install.Preflight.Tests.ps1` (or `Install.Tests.ps1`, whichever currently covers `Get-HostAdapterPreflightUri`), and a new test method in `tests/OpenClaw.Core.Tests/` covering the `Program.cs` blank-config fallback.

#### Functions/classes/CLI commands impacted:
- `Get-HostAdapterPreflightUri` (`scripts/Install.Preflight.psm1`) — default `$baseUrl` value only; no signature or behavior change beyond the literal.
- `OpenClaw.Core`'s `PostConfigure` fallback block in `Program.cs:16-18` — literal fallback string only; no signature or behavior change.
- No HostAdapter routes, controllers, or DTOs are touched.

#### Data flow and validation changes:
None. No new validation logic is introduced; the fix corrects a literal default value consumed identically before and after the change (still bound into `OpenClaw__HostAdapter__BaseUrl` / `HostAdapterOptions.BaseUrl`).

#### Error handling and logging updates:
None required. The 404 in the reported repro is a symptom of the wrong URL, not a gap in error handling; existing preflight error messaging (`Install.ps1`'s "HostAdapter preflight failed..." exception) already surfaces the failure clearly and needs no change.

#### Rollback/feature-flag considerations (if applicable):
No feature flag is applicable. Rollback is a plain revert of the six literal-value edits and their accompanying tests; no data migration or state is involved.

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:
- `OpenClaw__HostAdapter__BaseUrl` environment variable / config key: corrected default value changes from `http://host.docker.internal:4319/v1` to `http://host.docker.internal:4319` (config templates) and from `http://host.docker.internal:4319/v1/` to `http://host.docker.internal:4319/` (the `Program.cs` fallback and `Install.Preflight.psm1` default, preserving each location's existing trailing-slash convention).

#### Required configuration keys and defaults:
| Location | Corrected default |
|---|---|
| `.env.example:3` | `OpenClaw__HostAdapter__BaseUrl=http://host.docker.internal:4319` |
| `docker-compose.yml:27,73` | `OpenClaw__HostAdapter__BaseUrl: ${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319}` |
| `docker-compose.dev.yml:14` | `OpenClaw__HostAdapter__BaseUrl: ${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319}` |
| `src/OpenClaw.Core/Program.cs:17` | `"http://host.docker.internal:4319/"` |
| `scripts/Install.Preflight.psm1:73` | `$baseUrl = 'http://host.docker.internal:4319'` |
| `src/OpenClaw.Core/CoreOptions.cs:16` | No change — already `"http://host.docker.internal:4319/"` |

#### Backward-compatibility expectations:
An operator who has already manually removed the `/v1` segment from their local `.env` (working around this bug) is unaffected. An operator relying on the current (broken) default with `/v1` will see their preflight/runtime calls start succeeding once they re-copy the corrected `.env.example`; no operator-visible breaking change results from this fix, since the prior default never worked.

#### Performance constraints (latency/throughput/memory):
Not applicable — this is a configuration-literal correction with no runtime performance impact.

## Assumptions, Constraints, Dependencies
- Assumptions (environment, data, access): `.env.example` is the template operators copy to produce their local `.env` (confirmed in `issue.md`'s Environment section); correcting the template is sufficient to fix new/re-provisioned operator environments. `.env` itself is gitignored and cannot be edited as part of this PR.
- Constraints (budget, performance, compatibility): No compatibility break — the prior `/v1` default never resolved to a working HostAdapter route, so no consumer depends on the broken value. Fix must not alter `CoreOptions.cs`'s already-correct class-level default or the passing assertions in `HostAdapterHttpClientTests.cs`.
- External dependencies (services, libraries, releases): None. All six locations are repository-local literals; no third-party release or service dependency is involved.

## Data / API / Config Impact
- User-facing or API changes: None to HostAdapter's HTTP API surface. Operator-facing config templates (`.env.example`, `docker-compose.yml`, `docker-compose.dev.yml`) change their default value for `OpenClaw__HostAdapter__BaseUrl`.
- Data or migration considerations: None. No persisted data or schema is affected.
- Logging/telemetry updates (if any): None required. Existing HostAdapter request logging (`RequestLoggingMiddleware`) and `Install.ps1`'s preflight failure exception message already surface the relevant information; once the URL default is corrected, the preflight succeeds and no error is logged.
- Compatibility notes (CLI flags, config schemas, versioning): No CLI flag or config schema change. The config key name (`OpenClaw__HostAdapter__BaseUrl`) and its consumers (`HostAdapterOptions.BaseUrl`, `Get-HostAdapterPreflightUri`) are unchanged; only the literal default value changes.

## Test Strategy
Seeded from issue:

- [ ] Strip the stray `/v1` segment from all six locations listed above so every default resolves to HostAdapter's actual root-level route surface.
- [ ] Unit coverage: extend `Install.Preflight.Tests.ps1`/`Install.Tests.ps1` to assert the default preflight URL has no `/v1` segment; confirm `HostAdapterHttpClientTests.cs` continues to pass unchanged (it already reflects the corrected contract).
- [ ] Integration/manual verification: publish a fresh bundle and run the full `Install.ps1` flow end-to-end (through the Docker stage, not just `-SkipDocker`) with an operator `.env` left at its defaults, confirming the preflight probe succeeds and the Docker stack starts.

- Regression tests to add or update:
  - New PowerShell Pester test in `Install.Preflight.Tests.ps1` (or `Install.Tests.ps1`, whichever file currently exercises `Get-HostAdapterPreflightUri`) asserting the default preflight URL contains no `/v1` segment.
  - New C# xUnit test in `tests/OpenClaw.Core.Tests/` asserting `OpenClaw.Core`'s resolved `HostAdapter.BaseUrl` fallback (blank config, exercising the `Program.cs:16-18` `PostConfigure` branch) contains no `/v1` segment.
  - `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` — no change; must continue to pass unchanged as confirmation the existing tested contract is preserved.
- Unit tests for the fixed behavior and boundaries: covered by the two new tests above; both assert absence of `/v1` in the resolved default, not presence of any specific replacement string, so they remain valid if the host/port literal changes in the future.
- Edge cases and negative scenarios (invalid inputs, missing data, boundary values): operator `.env` missing the `OpenClaw__HostAdapter__BaseUrl` key entirely (exercises the PowerShell default path); config binding an explicit empty string for `HostAdapter.BaseUrl` (exercises the `Program.cs` `PostConfigure` fallback path).
- Error handling and logging verification: not applicable — no new error paths are introduced by this fix.
- Coverage impact and targets for changed lines/modules: no coverage regression on changed lines; new tests must cover the corrected default-value lines in `Program.cs` and `Install.Preflight.psm1`, keeping both files at or above the repository's uniform 85%/75% line/branch coverage thresholds (`.claude/rules/quality-tiers.md`).
- Toolchain commands to run (format → lint → type-check → test): PowerShell — PoshQC format → PSScriptAnalyzer → Pester; C# — CSharpier → analyzers/nullable → xUnit. Both toolchains must complete in a single clean pass per `.claude/rules/general-code-change.md`'s seven-stage loop.
- Manual validation steps (if required): publish a fresh bundle (`scripts/Publish.ps1`) and run the full `Install.ps1` flow end-to-end (through the Docker stage, not just `-SkipDocker`) with an operator `.env` left at its (corrected) defaults, confirming the preflight probe succeeds and the Docker stack starts.


## Acceptance Criteria
- [x] `.env.example`'s default `OpenClaw__HostAdapter__BaseUrl` (line 3) has no `/v1` segment.
- [x] Both `docker-compose.yml` occurrences (lines 27 and 73) and the `docker-compose.dev.yml` occurrence (line 14) default `OpenClaw__HostAdapter__BaseUrl` have no `/v1` segment.
- [x] `src/OpenClaw.Core/Program.cs`'s blank-config fallback (line 17) resolves to `http://host.docker.internal:4319/` (no `/v1`, trailing slash preserved).
- [x] `scripts/Install.Preflight.psm1`'s default base URL (line 73) has no `/v1` segment.
- [x] A PowerShell test asserts the `Install.Preflight` default preflight URL contains no `/v1` segment.
- [x] A C# test asserts `OpenClaw.Core`'s resolved `HostAdapter.BaseUrl` fallback (blank config) contains no `/v1` segment.
- [x] `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` continues to pass unchanged.
- [x] Full PowerShell toolchain (PoshQC format → analyze → Pester) and C# toolchain (CSharpier → analyzers/nullable → xUnit) pass with no coverage regression on changed lines.

## Risks & Mitigations
- Technical or operational risks: a partial fix (correcting some but not all six locations) would leave a residual mismatch between HostAdapter's real route surface and one or more consumer defaults, reproducing a variant of the original 404. A missed edit to `.env.example` specifically would continue to silently propagate the stray `/v1` to every newly provisioned operator environment, since it is the template operators copy.
- Mitigations and rollbacks: the two new regression tests (PowerShell and C#) pin the corrected defaults so a future accidental reintroduction of `/v1` fails CI rather than surfacing again as a field-reported installer failure. Rollback is a plain revert of the six literal-value edits and the two new tests; no data migration or state is involved.

## Rollout & Follow-up
- Release/rollout steps: merge the six corrected defaults and two new regression tests through the standard PR/CI flow; no phased rollout, feature flag, or migration is required since this is a default-value correction with no behavioral branch.
- Post-fix monitoring or clean-up tasks: after merge, confirm via a fresh `Publish.ps1` + `Install.ps1` end-to-end run (per the manual validation step above) that the scripted installer's HostAdapter preflight succeeds against the corrected defaults. No ongoing monitoring task is introduced.
- Links: Issue #137 (`docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/issue.md`); research: `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/research/2026-07-10T10-15-basepath-mismatch-confirmation-research.md`.
