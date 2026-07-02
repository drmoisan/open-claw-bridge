# wire-sendmail-runtime - Plan

- **Issue:** #99
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T10-37
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** full-feature (per `issue.md` marker)

## Required References

- General Coding Standards: `.claude/rules/general-code-change.md`
- General Unit Test Policy: `.claude/rules/general-unit-test.md`
- C# Standards: `.claude/rules/csharp.md`
- Quality Tiers: `.claude/rules/quality-tiers.md` (`OpenClaw.Core` is T1 per `quality-tiers.yml`)
- Architecture Boundaries: `.claude/rules/architecture-boundaries.md`
- Authoritative requirements: `docs/features/active/2026-07-02-wire-sendmail-runtime-99/spec.md` (6 acceptance criteria)

**All work must comply with these policies; do not duplicate their content here.**

## Conventions Used Below

- `FEATURE` = `docs/features/active/2026-07-02-wire-sendmail-runtime-99`
- `<ts>` = ISO-8601 artifact timestamp in `yyyy-MM-ddTHH-mm` format, captured at execution time.
- Evidence artifacts live only under `FEATURE/evidence/<kind>/` (canonical kinds: `baseline`, `regression-testing`, `qa-gates`, `issue-updates`, `other`). Raw coverage intermediates (Cobertura XML, TRX) may stage under `artifacts/csharp/`, but every evidence artifact referenced by a task lives under `FEATURE/evidence/`.
- Every command-step evidence artifact records: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- Test suite conventions: MSTest + FluentAssertions + Moq; property tests use CsCheck (already referenced by `tests/OpenClaw.Core.Tests`, see `Agent/*PropertyTests.cs`) — no new dependency.
- Toolchain commands (C#): `csharpier format .` / `csharpier check .` (global CSharpier 1.3.0; no local tool manifest), `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.

## Production Diff Confinement (AC-5)

Production (`src/`) changes are confined to exactly these three files:

1. `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` — `SendMailAsync` delegation.
2. `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` — new pure `MapSendMailRequest`.
3. `src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs` — XML-doc-comment-only edit (stale "#74/#75; the runtime adapter throws" sentence removal required by AC-1; the record signature is unchanged).

No changes to `src/OpenClaw.HostAdapter.Contracts/**`, `src/OpenClaw.MailBridge*/**`, HostAdapter routes, `SchedulingWorker`/`SchedulingWorker.Pipeline.cs` production code, or schemas. Phase 5 verifies this mechanically.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Policy Compliance & Baseline Capture

- [x] [P0-T1] Read repository policies in the `policy-compliance-order` sequence: `CLAUDE.md`-loaded rules, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/architecture-boundaries.md`; write `FEATURE/evidence/baseline/phase0-instructions-read.md`.
  - Acceptance: artifact exists containing `Timestamp:`, `Policy Order:`, and the explicit list of files read, before any Phase 1 task starts.
- [x] [P0-T2] Capture formatting baseline: run `csharpier check .` at the repo root; write `FEATURE/evidence/baseline/baseline-format.<ts>.md`.
  - Acceptance: artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail signal); exit code 0 on the untouched tree.
- [x] [P0-T3] Capture build/lint/type-check baseline: run `dotnet build OpenClaw.MailBridge.sln`; write `FEATURE/evidence/baseline/baseline-build.<ts>.md`.
  - Acceptance: artifact contains the four required fields; `Output Summary:` records warning/error counts; exit code 0.
- [x] [P0-T4] Capture test-and-coverage baseline: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; stage raw coverage output under `artifacts/csharp/`; write `FEATURE/evidence/baseline/baseline-test-coverage.<ts>.md`.
  - Acceptance: artifact contains the four required fields; `Output Summary:` records the pass/total test count and numeric baseline line and branch coverage percentages (solution-wide and for `OpenClaw.Core`); exit code 0.

### Phase 1 — Expect-Fail Replacement Tests for SendMailAsync Delegation

- [x] [P1-T1] Replace `SendMailAsync_Throws_DeferredNotSupported` (currently line 269 of `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs`) with five delegation tests using the suite's MSTest + FluentAssertions + Moq conventions: (a) success — client mock returns `ApiEnvelope<object?>(true, null, Meta, null)`; call completes and `Verify` the client received a wire `OpenClaw.HostAdapter.Contracts.SendMailRequest` and the caller's `CancellationToken` exactly once via `IHostAdapterClient.SendMailAsync(request, requestId, cancellationToken)`; (b) envelope failure — `Ok: false` with `ApiError("BRIDGE_UNAVAILABLE", ...)` produces `InvalidOperationException` whose message contains both the code and the message; (c) exception propagation — client throws `HttpRequestException`; the same exception type surfaces unwrapped; (d) cancellation — client throws `OperationCanceledException` for a canceled token; it propagates as `OperationCanceledException`; (e) request mapping — capture the wire request with `Moq` `Callback`/`Capture` and assert subject, body content and content type, To/Cc recipient translation (`AttendeeDto(Name, Email)` to `SendMailRecipientDto(SendMailEmailAddressDto(Address, Name))`), empty CC list maps to `null`, and `SaveToSentItems == true`.
  - Acceptance: the old test method is gone; five new test methods exist; the file compiles against current production code (tests reference only existing types); file remains <= 500 lines.
- [x] [P1-T2] [expect-fail] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~HostAdapterSchedulingServiceTests"` and observe all five new tests failing against the current `NotSupportedException` implementation; write `FEATURE/evidence/regression-testing/expect-fail-sendmail-delegation.<ts>.md`.
  - Acceptance: artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero), and an `Output Summary:` naming the five failing tests and the `NotSupportedException` failure cause; all pre-existing tests in the class still pass.

### Phase 2 — Pure Outbound Mapper MapSendMailRequest

- [x] [P2-T1] Implement `MapSendMailRequest` in `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`: signature `public WireSendMailRequest MapSendMailRequest(SendMailRequest request)` using a file-level alias `using WireSendMailRequest = OpenClaw.HostAdapter.Contracts.SendMailRequest;`; throws `ArgumentNullException` on null input; mapping per spec table — `Subject` -> `Message.Subject`; `BodyContentType`/`BodyContent` -> `Message.Body = new SendMailBodyDto(ContentType, Content)`; `ToRecipients` -> `Message.ToRecipients` with `SendMailEmailAddressDto(Address: Email, Name: empty-or-whitespace name maps to null)`; `CcRecipients` -> `Message.CcRecipients` with an empty list mapping to `null`; `Message.BccRecipients = null`; `SaveToSentItems = true`; `InReplyToMessageId` dropped and the drop documented in the method's XML doc comment; pure (no input mutation, no I/O); refresh the class-level XML doc to mention the outbound (agent-to-wire) direction.
  - Acceptance: method compiles under `dotnet build OpenClaw.MailBridge.sln` with zero analyzer warnings; file remains <= 500 lines.
- [x] [P2-T2] Add example-based tests for `MapSendMailRequest` to `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperTests.cs`: full field mapping, empty-or-whitespace recipient name maps to null, empty CC list maps to `null`, `SaveToSentItems` is `true`, `BccRecipients` is `null`, and null argument throws `ArgumentNullException`.
  - Acceptance: tests follow Arrange-Act-Assert with descriptive names; file remains <= 500 lines (currently 341).
- [x] [P2-T3] Create `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperPropertyTests.cs` with at least one CsCheck property-based test for `MapSendMailRequest` (T1 obligation for a new pure function), following the deterministic seeded-generator convention of the existing `tests/OpenClaw.Core.Tests/Agent/*PropertyTests.cs` files (for example `RecurringMeetingClassifierPropertyTests.cs`): for arbitrary valid agent requests, recipient count and address multiset are preserved for To and Cc, `SaveToSentItems` is always `true`, and the input record is never mutated; the failing seed is printed on `Sample` failure per CsCheck default.
  - Acceptance: new file exists at the stated path, uses only the already-referenced CsCheck package, and is <= 500 lines.
- [x] [P2-T4] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingDtoMapper"`; write `FEATURE/evidence/regression-testing/mapper-tests-pass.<ts>.md`.
  - Acceptance: artifact contains the four required fields; `EXIT_CODE: 0`; `Output Summary:` records the passed test count including the property test.

### Phase 3 — Wire SendMailAsync Delegation and Doc Cleanup

- [x] [P3-T1] Replace the throwing `SendMailAsync` in `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` (lines 123-129) with the delegating implementation: `ArgumentNullException.ThrowIfNull(request)`; map via the injected `mapper.MapSendMailRequest(request)`; `await hostAdapterClient.SendMailAsync(wireRequest, cancellationToken: ct).ConfigureAwait(false)` per the file's existing pattern; when the returned envelope is not `{ Ok: true }`, throw `InvalidOperationException` whose message includes `ApiError.Code` and `ApiError.Message` (with a deterministic fallback text when `Error` is null); no catch blocks — client exceptions including `OperationCanceledException` propagate unwrapped; remove the `NotSupportedException` and its stale "#74/#75" doc comment.
  - Acceptance: `SendMailAsync` contains no `NotSupportedException` and no `catch`; builds with zero analyzer warnings; file remains <= 500 lines (currently 131).
- [x] [P3-T2] Refresh the class-level XML doc comment of `HostAdapterSchedulingService` in the same file: remove the stale "read methods available today" phrasing so the doc describes both read and send delegation.
  - Acceptance: class doc no longer contains the string "read methods available today"; no code change in this task.
- [x] [P3-T3] Remove the stale sentence "deferred to issues #74/#75; the runtime adapter throws until it is available" from the XML doc comment in `src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs`, keeping the `AgentPolicyOptions.SendEnabled` gating sentence.
  - Acceptance: doc-comment-only diff — the `SendMailRequest` record signature and parameter docs are byte-identical apart from the removed sentence; file no longer contains "#74/#75".
- [x] [P3-T4] Rerun `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~HostAdapterSchedulingServiceTests"`; write `FEATURE/evidence/regression-testing/pass-after-sendmail-delegation.<ts>.md`.
  - Acceptance: artifact contains the four required fields; `EXIT_CODE: 0`; `Output Summary:` confirms all five Phase 1 delegation tests now pass (fail-before evidence is P1-T2, pass-after is this artifact).

### Phase 4 — Worker Send-Failure Isolation and Composed-Request Tests

- [x] [P4-T1] Add `RunCycle_SendFailure_LogsAndContinues` to `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs`: with `SendEnabled=true` and two candidates, the mocked `ISchedulingService.SendMailAsync` throws `InvalidOperationException` for the first message; assert the cycle does not throw, the second candidate is still hydrated (`GetSchedulingMessageAsync` verified for the second id), and `SendMailAsync` was invoked for both candidates (per-message isolation via `ProcessMessageSafelyAsync`).
  - Acceptance: test passes without any production change to `SchedulingWorker`; file remains <= 500 lines.
- [x] [P4-T2] Add `RunCycle_SendCancellation_StopsCycle` to `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs`: with `SendEnabled=true` and two candidates, the mocked `SendMailAsync` throws `OperationCanceledException` for the first message; assert `RunSchedulingCycleAsync` propagates `OperationCanceledException` and the second candidate is never hydrated (AC-3 "cancellation stops the cycle").
  - Acceptance: test passes without any production change to `SchedulingWorker`; file remains <= 500 lines.
- [x] [P4-T3] Extend `RunCycle_SendEnabled_InvokesSendMail` (line 138 of `SchedulingWorkerTests.cs`) with argument capture on the mocked `ISchedulingService.SendMailAsync` asserting the composed agent request: subject is `Re: {original subject}`, exactly one To recipient equal to the normalized `MessageFrom`, and a non-empty plain-text body produced by the slot-proposal formatter (spec "Seeded Test Conditions", integration-style condition).
  - Acceptance: the existing `Times.Once` verification is preserved; `RunCycle_SendDisabled_NeverInvokesSendMail` (line 123) is byte-identical (worker gating tests survive unchanged).
- [x] [P4-T4] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerTests"`; write `FEATURE/evidence/regression-testing/worker-gating-tests-pass.<ts>.md`.
  - Acceptance: artifact contains the four required fields; `EXIT_CODE: 0`; `Output Summary:` lists the pass count and confirms both pre-existing gating tests passed unmodified.

### Phase 5 — Diff-Scope Verification, Final QA Loop, and Coverage Comparison

- [x] [P5-T1] Verify production diff confinement: run `git diff --name-only (git merge-base main HEAD) -- src/` and assert the output is exactly the three files listed in "Production Diff Confinement" above; additionally run `git diff (git merge-base main HEAD) -- src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs` and assert every changed line is inside the XML doc comment; write `FEATURE/evidence/qa-gates/diff-scope.<ts>.md`.
  - Acceptance: artifact contains the four required fields plus the verbatim file list; any extra `src/` path or non-doc change in `SendMailRequest.cs` is a failure requiring remediation before Phase 5 continues.
- [x] [P5-T2] Verify the 500-line cap on every touched file (`HostAdapterSchedulingService.cs`, `SchedulingDtoMapper.cs`, `SendMailRequest.cs`, `HostAdapterSchedulingServiceTests.cs`, `SchedulingDtoMapperTests.cs`, `SchedulingDtoMapperPropertyTests.cs`, `SchedulingWorkerTests.cs`) by counting lines; record the counts in `FEATURE/evidence/qa-gates/file-size-check.<ts>.md`.
  - Acceptance: artifact lists each file with its line count; all counts <= 500.
- [x] [P5-T3] Final QA step 1 (format): run `csharpier format .` then `csharpier check .`; write `FEATURE/evidence/qa-gates/final-qa-format.<ts>.md`.
  - Acceptance: artifact contains the four required fields; `csharpier check .` exits 0; if `csharpier format .` changed any file, note it and restart the Phase 5 QA loop from this task after the change is committed to the working tree.
- [x] [P5-T4] Final QA steps 2-3 (lint + type-check): run `dotnet build OpenClaw.MailBridge.sln`; write `FEATURE/evidence/qa-gates/final-qa-build.<ts>.md`.
  - Acceptance: artifact contains the four required fields; exit code 0 with zero warnings (warnings are errors solution-wide); on failure, fix and restart the loop from P5-T3.
- [x] [P5-T5] Final QA steps 4-5 (architecture + tests with coverage): run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (this executes `AgentArchitectureBoundaryTests` and the full unit suite); stage raw coverage output under `artifacts/csharp/`; write `FEATURE/evidence/qa-gates/final-qa-test-coverage.<ts>.md`.
  - Acceptance: artifact contains the four required fields; exit code 0; `Output Summary:` records the pass/total test count and numeric post-change line and branch coverage percentages (solution-wide and for `OpenClaw.Core`); on failure, fix and restart the loop from P5-T3.
- [x] [P5-T6] Confirm a single clean QA pass: verify P5-T3, P5-T4, and P5-T5 each exited 0 in one uninterrupted sequence with no file changes between them; if not, restart from P5-T3 until a clean pass completes; record the confirming statement (with the timestamps of the clean-pass artifacts) in `FEATURE/evidence/qa-gates/final-qa-clean-pass.<ts>.md`.
  - Acceptance: artifact names the three clean-pass artifacts; no `SKIPPED` outcomes exist for any Phase 5 command task.
- [x] [P5-T7] Compare coverage against baseline: using the P0-T4 baseline artifact and the P5-T5 post-change artifact plus the staged Cobertura files under `artifacts/csharp/`, verify line coverage >= 85% and branch coverage >= 75% (uniform T1-T4 thresholds), no regression relative to baseline, and that all changed production lines in `HostAdapterSchedulingService.cs` and `SchedulingDtoMapper.cs` are covered; write `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md`.
  - Acceptance: artifact reports three numeric sections — baseline coverage, post-change coverage, changed-line coverage — with the threshold verdict for each; if any required numeric value is unavailable or any threshold fails, the plan outcome is remediation-required, never PASS.

## Test Plan

- Unit (service): `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — five delegation tests replacing `SendMailAsync_Throws_DeferredNotSupported` (success, envelope failure with error code/message, unwrapped exception propagation, cancellation propagation, request mapping capture). Fail-before evidence: `FEATURE/evidence/regression-testing/expect-fail-sendmail-delegation.<ts>.md`; pass-after: `FEATURE/evidence/regression-testing/pass-after-sendmail-delegation.<ts>.md`.
- Unit (mapper): `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperTests.cs` — example-based `MapSendMailRequest` tests; `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperPropertyTests.cs` — CsCheck property test (T1 obligation, no new dependency).
- Unit (worker): `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs` — new send-failure isolation and send-cancellation tests; argument-capture extension of `RunCycle_SendEnabled_InvokesSendMail`; existing gating tests (`RunCycle_SendDisabled_NeverInvokesSendMail`, `RunCycle_SendEnabled_InvokesSendMail` verification) survive unchanged.
- Unchanged: `HostAdapterHttpClientSendMailTests.cs` (client not modified); no HostAdapter/MailBridge test changes.
- Coverage evidence: baseline `FEATURE/evidence/baseline/baseline-test-coverage.<ts>.md`; post-change `FEATURE/evidence/qa-gates/final-qa-test-coverage.<ts>.md`; comparison `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md`. Raw Cobertura/TRX intermediates stage under `artifacts/csharp/` (non-evidence staging only).

## Open Questions / Notes

- **Diff-scope reconciliation.** The delegation prompt confined production changes to the two Runtime files, but authoritative AC-1 (spec.md) requires removing the stale doc sentence in `src/OpenClaw.Core/Agent/Contracts/SendMailRequest.cs`. The spec governs: the confinement set is the three files listed in "Production Diff Confinement", and P5-T1 verifies the third file's diff is doc-comment-only.
- **Stale worker catch block is out of scope** per spec: `SchedulingWorker.Pipeline.cs` lines 87-96 (`NotSupportedException` catch around mailbox-settings/free-busy) remains unchanged; P5-T1 enforces that no `SchedulingWorker` production file appears in the diff.
- **Interim duplicate-send risk (F6)** is accepted and documented in the spec; no plan task addresses idempotency. Sequence F6 immediately after this feature.
- **Mutation testing (T1, Stryker.NET >= 75%)** runs in pre-merge/nightly pipelines per `.claude/rules/quality-tiers.md`, not in this per-commit plan.
- The property test lives in a new `SchedulingDtoMapperPropertyTests.cs` file (rather than inside `SchedulingDtoMapperTests.cs`, currently 341 lines) to match the suite's `*PropertyTests.cs` convention and preserve 500-line headroom.
