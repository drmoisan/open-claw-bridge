# Feature (Acceptance Criteria) Audit — Issue #144 (container-validation-stray-v1-and-env-target)

- Reviewed: 2026-07-11T00-45
- Work mode: `minor-audit`
- AC source (authoritative, per the persisted `- Work Mode: minor-audit` marker in `issue.md`): `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/issue.md`, `## Acceptance Criteria` (7 checkbox items, AC1–AC7). `spec.md`/`user-story.md`/`research.md` are correctly absent for this work mode (confirmed by directory listing).

## Scope and Baseline

- Base branch: `main` (merge-base `81debeb1d58dd7226e0eec1bc66aa154047e6a82`)
- Head: `bug/container-validation-stray-v1-and-env-target-144` @ `a79dee489b46e177f6b98d605f9ecd0c8e8f9f24`
- Diff scope: `git diff --stat 81debeb1..a79dee48` — 28 files changed (979 insertions / 39 deletions): 2 production PowerShell files + 1 module manifest, 5 test files, 2 Markdown documentation files (`README.md`, `docs/mailbridge-runbook.md`), and the remainder feature-folder plan/issue/evidence Markdown.
- PR-context artifacts (`artifacts/pr_context.summary.txt`, `artifacts/pr_context.appendix.txt`) verified fresh (head SHA matches exactly) and used as primary/appendix evidence sources.

## Acceptance Criteria Inventory

`issue.md` `## Acceptance Criteria` carries 7 checkbox items, all marked `- [x]` at the start of this review:

| # | Criterion (abbreviated) |
|---|---|
| AC1 | In-container HostAdapter probe requests root `/status` (no `/v1`), verified by Pester asserting the shell command |
| AC2 | `HostAdapterInContainer` result's `ExpectedCondition` text references `/status` (no `/v1`) |
| AC3 | Default `-EnvFilePath` resolves to the deployed operator `.env` when present, falls back to `./.env` otherwise; resolution is a testable pure helper, verified present/absent |
| AC4 | No regression: full `tests/scripts` Pester suite passes; PoshQC format + analyze clean, in a single pass |
| AC5 | No `src/OpenClaw.HostAdapter/**` change |
| AC6 | Dashboard-access documentation corrected (README + runbook), accurate upstream token-fragment/paste + device re-pair procedure documented |
| AC7 | Dashboard validation reports auth accurately; new in-container gateway-token check added; no WebSocket/device-pairing handshake added |

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|---|---|---|
| AC1 | **PASS** | `OpenClawContainerValidation.psm1:282` (now) reads `http://host.docker.internal:4319/status`, confirmed via `git diff`. `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1:74-81` asserts `$execRequest | Should -Not -Match '/v1/'` and `Should -Match '/status'`; independently re-run and passing (part of both the 416/416 coverage-mode run and the 414/416 plain run — this file is unaffected by the Blocking test-environment finding). Independently corroborated: `src/OpenClaw.HostAdapter/Program.cs:73-74` confirms HostAdapter actually serves `/status` at root, with no `/v1` route group registered anywhere in the service. |
| AC2 | **PASS** | `OpenClawContainerValidation.psm1:342` `ExpectedCondition` text now reads "...HTTP 200 for `/status` with bearer token" (was `/v1/status`). Test assertion `$probe.ExpectedCondition | Should -Match '/status'; ... Should -Not -Match '/v1'` (`HostAdapter.Tests.ps1:79-80`), independently re-run and passing. |
| AC3 | **PARTIAL** | The pure-helper claim is independently verified and PASSES reliably: `Get-OpenClawOperatorEnvFilePath`/`Resolve-OpenClawDefaultEnvFilePath` are genuinely I/O-free/testable (source read: the former does only string composition + a null/whitespace guard; the latter's only I/O is one guarded `Test-Path`), and `tests/scripts/powershell/modules/.../OpenClawContainerValidation.EnvFilePathDefault.Tests.ps1` (4 Its, direct unmocked calls to the pure helpers plus one correctly `-ModuleName`-scoped `Test-Path` mock) independently re-run and pass under a completely standard `Invoke-Pester` invocation. However, the criterion's implicit "verified... for both the present and absent cases" end-to-end claim, as delivered via the entry-script integration tests in `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` (2 Its), is **not reliably verified**: both of those tests fail deterministically under a standard `Invoke-Pester` run (see the Blocking finding in `policy-audit.2026-07-11T00-45.md` and `code-review.2026-07-11T00-45.md`), passing only via the specific MCP wrapper invocation the executor's evidence used. The underlying production resolution logic in `scripts/Invoke-OpenClawContainerPathValidation.ps1` (lines 250-253) is straightforward and, on source read, correctly implements the present/fallback behavior — the gap is in the test verification's environment-independence, not in the production logic itself. |
| AC4 | **FAIL** | The criterion's literal text ("the full `tests/scripts` Pester suite passes... in a single pass") is not substantiated under a standard invocation: this audit's independent `Invoke-Pester` re-run (plain, no coverage, no MCP wrapper) produced **414 passed / 2 failed**, both in `Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1`, reproduced standalone and full-suite. The executor's committed evidence (416/416) is accurate only for the specific `Invoke-PoshQCTest` MCP-wrapper invocation path, which this audit determined does not represent a standard/portable invocation of the suite (see Blocking finding). Format and analyze are independently confirmed clean (0 diffs / 0 findings on all 8 changed PowerShell files, using the repo's own PSScriptAnalyzer settings) — the format/analyze half of AC4 is satisfied; the full-suite-single-pass half is not. |
| AC5 | **PASS** | `git diff --name-only 81debeb1..a79dee48 -- src/OpenClaw.HostAdapter` returns zero output lines, independently confirmed. No path under `src/OpenClaw.HostAdapter/**` appears anywhere in the change set. |
| AC6 | **PASS** | Independently confirmed via direct read of both files: `README.md` (dashboard section, ~line 706) and `docs/mailbridge-runbook.md` (Dashboard access section) no longer state that the dashboard "reads the token without an operator paste step" or that the token is "the only credential the dashboard accepts" (zero matches for all three removed-claim patterns via `grep -rc`). Both now document the `#token=` URL-fragment procedure or Control-UI paste, the `openclaw devices clear` + browser-site-data device re-pair reset, and the `OPENCLAW_AGENT_IMAGE` floating-tag caveat. (Independently noted, out of AC6's literal scope: two other, unrelated lines in `docs/mailbridge-runbook.md`'s HostAdapter setup walkthrough — lines 445 and 457 — retain a stale `/v1/status`/`/v1`-suffixed reference; recorded as a non-blocking Minor finding in the code review, not a gap in AC6 itself.) |
| AC7 | **PASS** | `AgentDashboard.ExpectedCondition`/`$summary` in `scripts/Invoke-OpenClawContainerPathValidation.ps1` now state that a 200 confirms the Control UI is served and does not verify operator sign-in (requires the `#token=` fragment); independently confirmed by source read and by the passing `GatewayTokenInContainer.Tests.ps1` assertion (`ExpectedCondition | Should -Not -Match 'authenticat'; Should -Match 'Control UI'`). A new `Test-OpenClawGatewayTokenInContainer` probe checks `OPENCLAW_GATEWAY_TOKEN` presence/non-emptiness inside the running agent container via the existing docker seam, distinct from the `.env`-file presence check, verified present/absent by Pester with the docker seam mocked (independently re-run and passing; this test file is unaffected by the Blocking finding, as it uses `-ModuleName`-scoped/in-file mocks, not the vulnerable unscoped pattern). No WebSocket/device-pairing handshake code was added — independently confirmed via `grep -in "websocket|ws://|wss://|pairing.handshake|New-WebSocket|ClientWebSocket"` across both production files, which returns only one match: descriptive help-comment prose stating the *absence* of a handshake, not handshake code. |

## Root-Cause / Non-Goal Verification

- Root cause matches the issue: the in-container HostAdapter probe hardcoded a stray `/v1` segment (the same defect class fixed for other call sites in issue #137, which did not cover this validation module) — independently confirmed both in the pre-fix diff and by reading HostAdapter's actual route table (`Program.cs`), which has no `/v1` prefix anywhere. The `-EnvFilePath` default pointed at the repo-root template rather than the deployed operator `.env` — independently confirmed via the pre-fix diff (`'./.env'` literal default) and the new resolution logic's correct present/fallback behavior.
- Non-goal: no `src/OpenClaw.HostAdapter/**` change — honored (AC5, verified above).
- Non-goal: no WebSocket/device-pairing handshake — honored (AC7, verified above); the issue's stated rationale (the gateway exposes no auth-gated HTTP endpoint; operator auth is WebSocket + device pairing) is not independently re-verified against a live upstream gateway by this review (would require a live install), but the *documentation and probe-text claims* about this rationale are internally consistent with the delivered code and are accurately worded, which is what AC7 requires.
- Non-goal: tracked docker-compose files, Dockerfiles, and `Install.Helpers.psm1` untouched — independently confirmed via `git diff --name-only`, zero matches for any of these paths.

## Summary

6 of 7 acceptance criteria (AC1, AC2, AC5, AC6, AC7) are independently verified **PASS**. AC3 is **PARTIAL**: the pure-helper unit-level testability claim holds and is independently reproducible under a standard `Invoke-Pester` invocation, but the end-to-end entry-script integration-test claim does not hold reliably outside a specific MCP tool-wrapper invocation path. AC4 is **FAIL** on its literal "full suite passes... in a single pass" wording, independently reproduced as a genuine, root-caused, and fix-verified test-environment-dependency defect (not a flake) — see the Blocking finding in `policy-audit.2026-07-11T00-45.md` and `code-review.2026-07-11T00-45.md`. Remediation is required before this branch can be considered fully delivered against its own acceptance criteria.

## Acceptance Criteria Check-off

Per the acceptance-criteria-tracking protocol, this review does not uncheck the AC3/AC4 boxes that were already marked `- [x]` in `issue.md` prior to this review (the protocol directs reviewers to check off PASS items and leave unclear/FAIL items unchecked; it does not direct reviewers to revert an executor's prior check-off). Instead, this discrepancy is recorded here and in `remediation-inputs.2026-07-11T00-45.md` for correction as part of remediation. AC1, AC2, AC5, AC6, and AC7 were already correctly marked `- [x]` and remain so; no reviewer-side check-off changes were made to `issue.md`.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/issue.md`
- Total AC items: 7
- Checked off (delivered, independently verified PASS): 5 (AC1, AC2, AC5, AC6, AC7)
- Remaining (independently verified PARTIAL/FAIL, currently marked `[x]` in the source but not fully substantiated): 2 (AC3, AC4)
- Items remaining: AC3 ("...verified by Pester for both the present and absent cases" — end-to-end entry-script tests fail under standard invocation), AC4 ("the full `tests/scripts` Pester suite passes... in a single pass" — 2 failures under standard invocation)

## Overall Feature Audit Verdict

**FAIL — remediation required.** 5 of 7 acceptance criteria are fully and independently verified. AC3 is PARTIAL and AC4 is FAIL due to a single, root-caused, fix-verified test-environment-dependency defect affecting exactly two tests in one file; the underlying production logic for AC1, AC2, AC3's pure-helper claim, AC5, AC6, and AC7 is independently confirmed sound. See `remediation-inputs.2026-07-11T00-45.md` for the concrete remediation path.
