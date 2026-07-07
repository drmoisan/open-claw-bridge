# send-on-behalf-allowlist - Plan

- **Issue:** #119
- **Parent (optional):** epic `docs/features/epics/openclaw-vision/epic-plan.md` (F15)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-06T22-16
- **Status:** Ready for Preflight
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata; `spec.md` and `user-story.md` both present and authoritative)

## Required References

Policy reading order (per `.claude/skills/policy-compliance-order/SKILL.md`):

1. `CLAUDE.md` / auto-loaded `.claude/rules/` standing instructions
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/quality-tiers.md`

**All work must comply with these policies; do not duplicate their content here.**

Authoritative feature documents (full-feature mode: `spec.md` + `user-story.md` are the acceptance-criteria sources):

- `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/spec.md` (10 AC; design decisions D1-D7; decision table rows 1-7)
- `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/user-story.md` (7 AC; scenarios 1-5)
- `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/issue.md` (mode marker)
- Research: `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/research/2026-07-06-send-on-behalf-allowlist-research.md`

## Global Constraints (apply to every task)

- **Diff scope (confined):** `src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs`, `src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs`, `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs`, new `src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs`, `tests/OpenClaw.Core.Tests/CloudGraph/` (two new files plus extensions to `GraphHostAdapterClientSendMailTests.cs`, `GraphAdapterOptionsValidatorTests.cs`, `GraphServiceCollectionExtensionsTests.cs`), and this feature folder. Zero changes to `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.*`, `OpenClaw.Core/Agent/**` production code, `HostAdapterHttpClient`, `GraphServiceCollectionExtensions.cs` production code, `Program.cs`, or `quality-tiers.yml`.
- **Toolchain loop (per phase batch):** `csharpier format .` then `csharpier check .` (global tool 1.3.0; not `dotnet csharpier`), `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Restart the loop from formatting if any step fails or changes files; a phase is complete only when all steps pass in a single pass.
- **Determinism:** no live Graph calls; all HTTP through the mocked `HttpMessageHandler` (`FakeHttpHandler` pattern); `MockBehavior.Strict` `IAppTokenProvider` mock; no temporary files; no `Task.Delay`/`Thread.Sleep`/wall-clock reads in tests (the deny path has no time dependency; use `FakeTimeProvider` where a clock is needed).
- **Test stack:** MSTest + FluentAssertions + Moq + CsCheck + `FakeTimeProvider` + `FakeHttpHandler` (repository-actual stack per the F13 spec note; `InternalsVisibleTo("OpenClaw.Core.Tests")` reaches `internal` production types).
- **File size:** every new or modified production and test file <= 500 lines.
- **UPN hygiene:** the deny `ApiError.Message` and the deny log template never contain a principal or assistant UPN; the message names the key `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns`.
- **Evidence:** all evidence artifacts under `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/<kind>/` (canonical scheme per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`; non-canonical `artifacts/` evidence paths are prohibited). Raw command intermediates (TRX, coverage XML) go to `artifacts/csharp/`. `<ts>` in artifact names is the ISO-8601 `yyyy-MM-ddTHH-mm` execution timestamp.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture and Policy Compliance

- [x] [P0-T1] Read the policy documents in the Required References order (items 1-5) and write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read
  - Acceptance: artifact exists with all three fields populated before any Phase 1 work begins
- [x] [P0-T2] Verify full-feature document preconditions: `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/issue.md` contains `- Work Mode: full-feature`, and `spec.md` + `user-story.md` exist in the feature folder with `## Acceptance Criteria` sections; record the check in `evidence/baseline/phase0-instructions-read.md` (append a `Mode Verification:` section)
  - Acceptance: appended section names the three files, the mode marker value, and a pass/fail verdict; fail closed (stop and report) on any mismatch
- [x] [P0-T3] Capture the C# formatting baseline: run `csharpier check .` from repo root and write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/baseline/csharp-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists with all four fields; `Output Summary:` states pass/fail and any offending file count
- [x] [P0-T4] Capture the C# build/analyzer/nullable baseline: run `dotnet build OpenClaw.MailBridge.sln` and write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/baseline/csharp-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists with all four fields; `Output Summary:` records warning/error counts (expected 0/0 on clean baseline)
- [x] [P0-T5] Capture the C# test, architecture-boundary, and coverage baseline: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (this run includes `CloudGraphArchitectureBoundaryTests` and `CloudGraphContractParityTests`), copy raw TRX/coverage output under `artifacts/csharp/`, and write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/baseline/csharp-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric baseline line-coverage percent and branch-coverage percent and total pass/fail test counts
  - Acceptance: artifact exists with all four fields and numeric coverage headline values (no placeholders); raw intermediates present under `artifacts/csharp/`

### Phase 1 — Pure Authorizer (SendOnBehalfAuthorizer)

- [x] [P1-T1] Create `src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs`: `internal` enum `SendAuthorizationDecision { AllowedSelf, AllowedOnBehalf, DeniedNotAllowlisted }` and `internal static` class `SendOnBehalfAuthorizer` with one pure function `Authorize(string principalMailboxUpn, string assistantMailboxUpn, IEnumerable<string> allowedPrincipalMailboxUpns)` — comparisons on `Trim()`ed values with `StringComparison.OrdinalIgnoreCase`; `AllowedSelf` when principal equals assistant (dominates every allowlist); `AllowedOnBehalf` when principal differs and is a trimmed case-insensitive member; `DeniedNotAllowlisted` otherwise (including empty allowlist); no I/O, no clock, no logging; XML docs state the purity contract and fail-closed-empty semantics
  - Acceptance: file compiles, <= 500 lines, namespace `OpenClaw.Core.CloudGraph`, total decision function (every input maps to exactly one enum value)
- [x] [P1-T2] Create `tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerTests.cs`: MSTest unit cases covering all seven spec decision-table rows — self-send equal UPNs -> `AllowedSelf` (row 1); self-send differing only by case -> `AllowedSelf`; allowlisted member -> `AllowedOnBehalf` (row 2); case-differing member -> `AllowedOnBehalf` (row 5); whitespace-padded member and whitespace-padded principal -> `AllowedOnBehalf` (row 5); empty allowlist with `{p} != {a}` -> `DeniedNotAllowlisted` (row 3); non-member principal -> `DeniedNotAllowlisted` (row 4); duplicate allowlist entries leave the decision unchanged (row 7)
  - Acceptance: all new tests pass; each of the seven decision-table rows has at least one named test
- [x] [P1-T3] Create `tests/OpenClaw.Core.Tests/CloudGraph/SendOnBehalfAuthorizerPropertyTests.cs`: four CsCheck property tests — (1) case-invariance: random casing of principal and allowlist entries never changes the decision; (2) deny-completeness: generated allowlists excluding the principal with `{p} != {a}` always yield `DeniedNotAllowlisted`; (3) membership soundness: inserting the principal in any casing/padding into any generated allowlist yields `AllowedOnBehalf`; (4) self-send dominance: `{p} == {a}` (any casing) yields `AllowedSelf` for every generated allowlist including lists containing or excluding the principal
  - Acceptance: all four properties pass; satisfies the T1 >= 1-property-per-pure-function obligation for `Authorize`; failing seeds reproducible (CsCheck default reporting)
- [x] [P1-T4] Run the mandatory C# toolchain loop for the Phase 1 batch (`csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`), restarting from formatting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 2 — Options Key, Validator Rule, and Binding

- [x] [P2-T1] Update `src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs`: add the additive string-collection property `AllowedPrincipalMailboxUpns` (get-only initialized list, compatible with the configuration binder and analyzer stack) defaulting to an empty collection; XML doc records the env-binding pattern (`OpenClaw__GraphAdapter__AllowedPrincipalMailboxUpns__0`, `__1`, ...) and the fail-closed-empty semantics (empty/absent = deny all on-behalf sends; self-send unaffected)
  - Acceptance: file compiles, <= 500 lines; no other property changed
- [x] [P2-T2] Update `src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs`: add the shape-only rule (applies only when `Enabled == true`, full-violation-list style) that every `AllowedPrincipalMailboxUpns` entry must be non-whitespace; an empty list is valid; the violation message names `AllowedPrincipalMailboxUpns` and echoes no configured values
  - Acceptance: file compiles, <= 500 lines; `Enabled == false` still yields zero errors for any allowlist contents
- [x] [P2-T3] Extend `tests/OpenClaw.Core.Tests/CloudGraph/GraphAdapterOptionsValidatorTests.cs`: unit cases — Enabled + whitespace-only entry produces exactly one violation naming `AllowedPrincipalMailboxUpns`; Enabled + empty list is valid; Disabled + malformed entries is valid (Enabled-only rule parity) — plus one CsCheck property test: generated lists of non-whitespace entries never produce the new violation, and any generated list containing a whitespace-only entry always does
  - Acceptance: all new tests pass; property test present for the extended pure validator function (T1 obligation)
- [x] [P2-T4] Extend `tests/OpenClaw.Core.Tests/CloudGraph/GraphServiceCollectionExtensionsTests.cs`: verify indexed configuration keys (`OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns:0`, `:1`) bind to the collection via an in-memory `ConfigurationBuilder` (consistent with existing binding tests), and a whitespace-only entry fails the `ValidateOnStart` startup-validation path
  - Acceptance: both new tests pass; no production change to `GraphServiceCollectionExtensions.cs` is required or made
- [x] [P2-T5] Run the mandatory C# toolchain loop for the Phase 2 batch (same four commands as P1-T4), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 3 — SendMail Gate (Fail-Before / Pass-After)

- [x] [P3-T1] [expect-fail] Extend `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs` with the authorization contract tests, then run the file once before any production change and record the failing run: (a) decisive deny contract — non-allowlisted principal with `{p} != {a}` yields `Ok == false`, `Error.Code == "UNAUTHORIZED"`, `Error.BridgeErrorCode == "SendOnBehalfDenied"`, `Error.Retryable == false`, correct `Meta.RequestId`/`AdapterVersion == "cloudgraph"`, with the mocked `HttpMessageHandler` invoked zero times and the `MockBehavior.Strict` `IAppTokenProvider` mock (no setups) never called; (b) empty-allowlist deny — same contract for the empty/absent-allowlist default; (c) deny message content — message names `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` and contains neither the principal nor the assistant UPN, and exactly one warning log entry carries the request id only; (d) allowlisted principal with `{p} != {a}` — POST reaches `users/{a}/sendMail` with `from = {p}` in the body and 202 maps to `ok: true, data: null`; (e) case-differing allowlist entry also permits the send; (f) self-send with an empty allowlist succeeds with no `from` in the body. Write the fail-before evidence to `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/regression-testing/send-on-behalf-gate-fail-before.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` naming the failing deny tests (a)-(c)
  - Acceptance: tests (a)-(c) fail before the gate exists (current code silently represents the principal); tests (d)-(f) pass; the fail-before artifact exists with all four fields
- [x] [P3-T2] Update `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs`: call `SendOnBehalfAuthorizer.Authorize(options.PrincipalMailboxUpn, options.AssistantMailboxUpn, options.AllowedPrincipalMailboxUpns)` at the top of `SendMailAsync`, before `executor.ExecuteAsync` (therefore before token acquisition and any HTTP); on `DeniedNotAllowlisted` return `Task.FromResult` of the failure envelope `ApiEnvelope<object?>(false, null, new ApiMeta(<ResolveRequestId result>, "cloudgraph", null), new ApiError("UNAUTHORIZED", <message naming OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns, echoing no UPNs>, "SendOnBehalfDenied", Retryable: false))` and emit exactly one `LogWarning` whose template carries the request id only; pass the decision into `ComposeSendMailBody` and inject `from = {p}` if and only if the decision is `AllowedOnBehalf`, deleting the inline `principalIsAssistant` re-derivation so the from-injection predicate and the authorization decision share one source
  - Acceptance: file compiles, <= 500 lines; `SendMailAsync` signature and envelope type unchanged; no change to `GraphRequestExecutor.cs`
- [x] [P3-T3] Rerun `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs`; all tests including (a)-(f) pass; append the pass-after record (`Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`) to `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/regression-testing/send-on-behalf-gate-fail-before.<ts>.md`
  - Acceptance: all send-mail tests green; the artifact contains both the fail-before and pass-after records
- [x] [P3-T4] Verify the regression surface is unmodified and green: confirm via `git diff` that the existing self-send test (`SendMail_PrincipalEqualsAssistant_OmitsFrom`), the existing D5 error-mapping tests, and the existing throttling tests are textually unmodified, and that the full test run (P3-T5) includes passing `CloudGraphArchitectureBoundaryTests` and `CloudGraphContractParityTests`; write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/regression-testing/regression-surface.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing the verified-unmodified test names and the green suites
  - Acceptance: artifact exists with all four fields; zero modifications to the named existing tests; architecture and parity suites pass with the new type in scope
- [x] [P3-T5] Run the mandatory C# toolchain loop for the Phase 3 batch (same four commands as P1-T4), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 4 — Tenant Validation Runbook and Human-Interaction Record

- [x] [P4-T1] Create `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md` covering, in order: (1) the Exchange `GrantSendOnBehalfTo` grant by cross-reference to `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` (do not duplicate the procedure); (2) reconciliation of `AllowedPrincipalMailboxUpns` with the tenant's documented `GrantSendOnBehalfTo` grants; (3) one allowed live on-behalf send (expect Graph 202 and the rendered "Assistant on behalf of Executive" appearance in Outlook and OWA) and one deliberately non-allowlisted send (expect the local deny envelope with `UNAUTHORIZED` / `SendOnBehalfDenied` / `retryable: false`); (4) confirmation that Send As is absent for the principal/assistant pair
  - Acceptance: runbook exists at the exact path with all four sections; professional tone per `.claude/rules/tonality.md`; grant procedure cross-referenced, not duplicated
- [x] [P4-T2] Write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/other/human-interaction-record.<ts>.md` documenting the tenant-validation `human_interaction` requirement for the orchestrator checkpoint, following the F11 HI-1 precedent shape: requirement description (tenant-side validation cannot be automated — no Azure/Exchange credentials in this environment or CI), `response: "exception"`, and `runbook_path: docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md`, satisfying the `.claude/rules/orchestrator-state.md` invariant that an `exception` carries a non-empty `runbook_path`
  - Acceptance: artifact exists with `Timestamp:`, the requirement text, the `response` value, and the `runbook_path`; the orchestrator (not this plan's executor) records the entry in `artifacts/orchestration/orchestrator-state.json`

### Phase 5 — Final QA, Coverage Comparison, and Reconciliation

- [x] [P5-T1] Verify the 500-line cap: measure line counts of every new/modified file (`src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs`, `GraphAdapterOptions.cs`, `GraphAdapterOptionsValidator.cs`, `GraphHostAdapterClient.SendMail.cs`, and the five touched test files under `tests/OpenClaw.Core.Tests/CloudGraph/`); write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/file-size-cap.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (per-file line-count table and maximum)
  - Acceptance: artifact exists; every listed file <= 500 lines
- [x] [P5-T2] Verify test hygiene: search the touched test files under `tests/OpenClaw.Core.Tests/CloudGraph/` for live-endpoint usage and banned APIs (patterns: `HttpClientHandler`, `GetTempFileName`, `GetTempPath`, `File.Write`, `Thread.Sleep`, `Task.Delay(` without a `TimeProvider` argument, `DateTime.UtcNow`, `DateTime.Now`, and `graph.microsoft.com` outside mocked-handler `BaseAddress` literals); write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/test-hygiene.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists; zero violations (or each match explained as a mocked-handler `BaseAddress` literal)
- [x] [P5-T3] Final QA formatting gate: run `csharpier format .` then `csharpier check .`; write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/csharp-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; if `format` changed any file, record the change and restart the Phase 5 QA loop from this task
  - Acceptance: artifact exists; `csharpier check .` exits 0
- [x] [P5-T4] Final QA build gate (lint + nullable + analyzers): run `dotnet build OpenClaw.MailBridge.sln`; write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/csharp-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; on failure, remediate and restart the Phase 5 QA loop from P5-T3
  - Acceptance: artifact exists; build exits 0 with zero warnings/errors
- [x] [P5-T5] Final QA test + coverage gate (includes architecture-boundary, unit, property, and contract-parity suites): run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; copy raw TRX/coverage to `artifacts/csharp/`; write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/csharp-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric post-change line and branch coverage percents and pass/fail counts; on failure, remediate and restart the Phase 5 QA loop from P5-T3
  - Acceptance: artifact exists with numeric coverage values (no placeholders); all tests pass
- [x] [P5-T6] Produce the coverage-comparison artifact: compare the P0-T5 baseline against the P5-T5 post-change coverage and compute changed/new-code coverage for `SendOnBehalfAuthorizer.cs` and the changed lines of `GraphAdapterOptions.cs`, `GraphAdapterOptionsValidator.cs`, and `GraphHostAdapterClient.SendMail.cs` from the coverage XML; write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/qa-gates/coverage-comparison.<ts>.md` reporting baseline coverage, post-change coverage, and new/changed-code coverage, with a threshold verdict (line >= 85%, branch >= 75%, no regression on changed lines)
  - Acceptance: artifact exists with all three numeric sections; verdict PASS only when all thresholds hold — otherwise the plan outcome is remediation-required, never PASS
- [x] [P5-T7] Verify zero production-code drift outside the allowed diff scope: run `git diff --name-only` against the branch base and confirm the changed-file set is confined to the four CloudGraph production files, the five touched test files, and this feature folder — with zero changes to `src/OpenClaw.HostAdapter.Contracts/**`, `src/OpenClaw.MailBridge*/**`, `src/OpenClaw.Core/Agent/**`, `src/OpenClaw.Core/HostAdapterHttpClient.cs`, `src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs`, `src/OpenClaw.Core/Program.cs`, and `quality-tiers.yml`; write `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/evidence/other/diff-scope-verification.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing the full changed-file set
  - Acceptance: artifact exists; changed-file set matches the Global Constraints diff scope exactly
- [x] [P5-T8] Reconcile the plan checklist against evidence on disk: mark every completed task, confirm every evidence-producing task's artifact exists with complete schema fields, verify each acceptance criterion in the Acceptance Criteria Mapping table below traces to a completed task, and record the reconciliation summary (task-to-artifact table plus AC verdict column) at the bottom of this plan's Open Questions / Notes section
  - Acceptance: no checked task lacks its named artifact; every AC row has a completed implementing/verifying task; any gap flips the outcome to INCOMPLETE

## Acceptance Criteria Mapping

Spec AC (S1-S10, `spec.md` "Acceptance Criteria") and user-story AC (U1-U7, `user-story.md` "Acceptance Criteria") map to plan tasks as follows:

| AC | Summary | Implementing / verifying tasks |
|---|---|---|
| S1 | Additive `AllowedPrincipalMailboxUpns` property; indexed-key binding; `ValidateOnStart` failure on whitespace entry | P2-T1, P2-T4 |
| S2 | Pure `Authorize` with trimmed `OrdinalIgnoreCase`; unit tests for all seven decision-table rows | P1-T1, P1-T2 |
| S3 | Four CsCheck properties (case-invariance, deny-completeness, membership soundness, self-send dominance) | P1-T3 |
| S4 | Decisive deny contract: deny envelope fields, zero handler invocations, strict token provider never called | P3-T1 (a)(b), P3-T2, P3-T3 |
| S5 | Deny message names the key, echoes no UPNs; one warning log with request id only | P3-T1 (c), P3-T2, P3-T3 |
| S6 | Allowlisted send with `from = {p}`; case-differing entry permits; self-send test passes unmodified (single decision source) | P3-T1 (d)(e)(f), P3-T2, P3-T3, P3-T4 |
| S7 | Validator whitespace rule + empty-list validity + disabled pass-through + CsCheck property | P2-T2, P2-T3 |
| S8 | Architecture-boundary and contract-parity suites pass unchanged; D5 error-mapping and throttling tests green | P3-T4, P3-T5, P5-T5 |
| S9 | Full toolchain single pass; coverage >= 85%/75% with changed lines covered; 500-line cap; no live calls or temp files; Stryker >= 75% in the pre-merge/nightly pipeline (not the per-commit loop — see Notes) | P1-T4, P2-T5, P3-T5, P5-T1..P5-T6 |
| S10 | Runbook exists with required content; `human_interaction` exception record with `runbook_path` | P4-T1, P4-T2 |
| U1 | Allowlisted on-behalf send (exact/case/whitespace entry) posts with `from = {p}`, 202 -> success | P3-T1 (d)(e), P3-T3 |
| U2 | Non-allowlisted principal denied with zero I/O (handler zero invocations, token provider never called) | P3-T1 (a)(b), P3-T2, P3-T3 |
| U3 | Self-send unaffected by any allowlist; self-send-dominance and case-invariance properties hold | P1-T3, P3-T1 (f), P3-T4 |
| U4 | Deny message names the key, contains no UPN; warning log carries request id only | P3-T1 (c), P3-T2 |
| U5 | Indexed-key binding works; whitespace entry fails startup validation | P2-T3, P2-T4 |
| U6 | Single decision source covered by decision-table unit tests and the four T1 properties | P1-T1, P1-T2, P1-T3, P3-T2 |
| U7 | Tenant-side validation documented in the runbook and recorded as a `human_interaction` exception, not claimed as automated | P4-T1, P4-T2 |

## Test Plan

- **Unit:** authorizer decision-table rows 1-7 (P1-T2); validator whitespace/empty/disabled cases (P2-T3); binding and `ValidateOnStart` (P2-T4); send-mail authorization contract at the mocked-handler seam — deny envelope, zero-I/O proof, message/log content, allowed on-behalf body shape, case-normalization at the composed layer, self-send regression (P3-T1/P3-T3).
- **Property-based (CsCheck, T1 obligation):** four authorizer properties (P1-T3); validator entry-list property (P2-T3).
- **Architecture:** existing `CloudGraphArchitectureBoundaryTests` cover the new type automatically via namespace prefix; verified green at P3-T4/P3-T5/P5-T5 — no new rules.
- **Contract parity:** existing `CloudGraphContractParityTests` pass unchanged (`IHostAdapterClient` surface untouched) — verified at P3-T4/P3-T5/P5-T5.
- **Regression:** existing D5 error-mapping, throttling, and self-send tests textually unmodified and green (P3-T4).
- **Integration:** none — no live Graph calls anywhere; tenant-side verification is human-runbook scope (P4-T1/P4-T2).
- **Coverage evidence:** baseline `evidence/baseline/csharp-test-coverage.<ts>.md` (P0-T5); post-change `evidence/qa-gates/csharp-test-coverage.<ts>.md` (P5-T5); comparison `evidence/qa-gates/coverage-comparison.<ts>.md` (P5-T6). Raw intermediates in `artifacts/csharp/`.

## Open Questions / Notes

- **CSharpier command form:** this repo uses the global `csharpier` 1.3.0 executable (`csharpier format .` / `csharpier check .`); there is no local tool manifest, so `dotnet csharpier` is not used despite the wording in `.claude/rules/csharp.md`.
- **Test stack note:** MSTest + FluentAssertions + Moq + CsCheck + `FakeTimeProvider` + `FakeHttpHandler` is the repository's actual stack (per the F13 spec note), notwithstanding `.claude/rules/csharp.md`'s xUnit/NSubstitute wording.
- **Mutation testing (S9 partial):** Stryker.NET mutation score >= 75% on the changed T1 surface runs in the pre-merge/nightly pipeline per `.claude/rules/general-code-change.md` ("Mutation testing and golden tests run in pre-merge or nightly pipelines, not the per-commit loop"); it is not a task in this per-commit plan and its result is consumed by the pipeline gate, not by P5.
- **Behavior change callout:** existing deployments with `{p} != {a}` and no allowlist change from silent representation to denial — this intended fail-closed hardening must be called out in the PR description (spec "Versioning / backward compatibility").
- **Orchestrator checkpoint:** the `human_interaction` requirement (P4-T2) is recorded in `artifacts/orchestration/orchestrator-state.json` by the orchestrator; the executor produces the evidence record only.
- **Deny path has no time dependency:** no clock or `FakeTimeProvider` is required for the new deny tests; the strict token-provider mock and zero-invocation handler assertion are the decisive fail-closed proof.

## Reconciliation Summary (P5-T8)

Reconciled 2026-07-06T23-21. All 24 tasks (P0-T1..P5-T8) are checked off. Every
evidence-producing task's artifact exists on disk with the required schema fields
(`Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` where applicable).

### Task-to-artifact map

| Task | Artifact / evidence | Present |
|---|---|---|
| P0-T1/T2 | `evidence/baseline/phase0-instructions-read.md` | yes |
| P0-T3 | `evidence/baseline/csharp-format.2026-07-06T22-45.md` | yes |
| P0-T4 | `evidence/baseline/csharp-build.2026-07-06T22-45.md` | yes |
| P0-T5 | `evidence/baseline/csharp-test-coverage.2026-07-06T22-45.md` (line 93.73%, branch 85.25%) | yes |
| P1-T1..T4 | production `SendOnBehalfAuthorizer.cs` + two test files; toolchain green | code on disk |
| P2-T1..T5 | options property, validator rule, validator + binding tests; toolchain green | code on disk |
| P3-T1 [expect-fail] / P3-T3 | `evidence/regression-testing/send-on-behalf-gate-fail-before.2026-07-06T23-13.md` (fail-before 3 fail; pass-after 14 pass) | yes |
| P3-T2 | `GraphHostAdapterClient.SendMail.cs` gate | code on disk |
| P3-T4 | `evidence/regression-testing/regression-surface.2026-07-06T23-19.md` | yes |
| P3-T5 | full loop green (embedded in P5-T5 rerun) | verified |
| P4-T1 | `runbooks/send-on-behalf-validation.runbook.md` (verified, not overwritten) | yes |
| P4-T2 | `evidence/other/human-interaction-record.2026-07-06T23-20.md` | yes |
| P5-T1 | `evidence/qa-gates/file-size-cap.2026-07-06T23-21.md` (max 489) | yes |
| P5-T2 | `evidence/qa-gates/test-hygiene.2026-07-06T23-21.md` (0 violations) | yes |
| P5-T3 | `evidence/qa-gates/csharp-format.2026-07-06T23-21.md` | yes |
| P5-T4 | `evidence/qa-gates/csharp-build.2026-07-06T23-21.md` (0/0) | yes |
| P5-T5 | `evidence/qa-gates/csharp-test-coverage.2026-07-06T23-21.md` (line 94.61%, branch 86.08%) | yes |
| P5-T6 | `evidence/qa-gates/coverage-comparison.2026-07-06T23-21.md` (PASS) | yes |
| P5-T7 | `evidence/other/diff-scope-verification.2026-07-06T23-21.md` | yes |

### AC verdict

All 10 spec AC (S1-S10) and all 7 user-story AC (U1-U7) are checked off in `spec.md` /
`user-story.md`; each traces to a completed implementing/verifying task per the Acceptance
Criteria Mapping table. Two deferrals are documented and are not per-commit-executor scope:
(1) the S9 Stryker.NET mutation-score clause runs in the pre-merge/nightly pipeline, not the
per-commit loop; (2) the S10/U7 tenant-side live validation is a `human_interaction`
exception recorded for the orchestrator, per the runbook.

### Scope deviation (documented)

`CloudGraphContractParityTests.cs` was modified beyond the three enumerated test-file
extensions. The change is a fixture-only update (its `Service` helper allowlists the
principal) within the allowed `tests/OpenClaw.Core.Tests/CloudGraph/` directory, required by
the fail-closed behavior change so the on-behalf send-mail parity test still reaches the
Graph 400 path. No prohibited path was touched. See
`evidence/other/diff-scope-verification.2026-07-06T23-21.md`.

Outcome: COMPLETE.
