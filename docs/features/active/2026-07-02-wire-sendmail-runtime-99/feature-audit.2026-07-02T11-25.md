# Feature Audit: wire-sendmail-runtime (#99)

- **Audit Date:** 2026-07-02
- **Branch:** `feature/wire-sendmail-runtime-99` @ `d8a08879cacf23e8376e9ad4ed64c9b1a421a7d5`
- **Work Mode:** `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`)
- **AC Sources:** `docs/features/active/2026-07-02-wire-sendmail-runtime-99/spec.md` (`## Acceptance Criteria`) and `docs/features/active/2026-07-02-wire-sendmail-runtime-99/user-story.md` (`## Acceptance Criteria`); mirrored in `issue.md`. The six criteria are textually identical across all three files.

## Scope and Baseline

- Resolved base branch: `main` (resolved to `origin/main`; the local `main` ref is stale per caller inputs and the executor's worktree memory note).
- Merge-base SHA: `13f6f9390cbb634abca0c36eb7cdabe4acc2830e`; head SHA: `d8a08879cacf23e8376e9ad4ed64c9b1a421a7d5`; range: `13f6f93..d8a0887` (single commit).
- Evidence sources: `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` (fresh — appendix head SHA matches the current branch head), the authoritative `git diff` file list (31 files: 7 `.cs`, 24 `.md`; +1117/-28), executor evidence under `evidence/{baseline,qa-gates,regression-testing}/`, and the reviewer's independent toolchain re-run and cobertura re-parse (`evidence/qa-gates/coverage-review.2026-07-02T11-25.md`).
- The audit scope is the full feature-vs-base branch diff. No caller narrowing was attempted or accepted (see the policy audit's Rejected Scope Narrowing section).

## Acceptance Criteria Inventory

| # | Criterion (abbreviated) | Source(s) | Format |
|---|---|---|---|
| AC-1 | `SendMailAsync` maps agent request to wire request, awaits `IHostAdapterClient.SendMailAsync`, forwards the caller's token; `NotSupportedException` and stale "#74/#75" doc comments removed from both named files | spec.md, user-story.md (issue.md mirror) | `- [x]` checkbox (already checked by executor) |
| AC-2 | `Ok: false` envelope raises an exception carrying the API error code and message; client exceptions including `OperationCanceledException` propagate unwrapped | spec.md, user-story.md | `- [x]` checkbox |
| AC-3 | Worker gating end-to-end in tests: send exactly once when `SendEnabled=true`; never when `false` (default); send failure isolated and cycle continues; cancellation stops the cycle | spec.md, user-story.md | `- [x]` checkbox |
| AC-4 | `SendMailAsync_Throws_DeferredNotSupported` replaced by delegation tests (success, envelope failure, exception propagation, cancellation, mapping) in suite conventions; worker send-failure isolation case added; property-based test for the pure mapping function (T1) | spec.md, user-story.md | `- [x]` checkbox |
| AC-5 | No changes to HostAdapter routes, MailBridge, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.MailBridge.Contracts`, or schemas; diff confined to `OpenClaw.Core` runtime wiring and its tests | spec.md, user-story.md | `- [x]` checkbox |
| AC-6 | Full C# toolchain passes; coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered | spec.md, user-story.md | `- [x]` checkbox |

## Acceptance Criteria Evaluation

| # | Verdict | Evidence |
|---|---|---|
| AC-1 | **PASS** | Diff: `HostAdapterSchedulingService.SendMailAsync` now null-guards, maps via `mapper.MapSendMailRequest(request)`, awaits `hostAdapterClient.SendMailAsync(wireRequest, cancellationToken: ct).ConfigureAwait(false)`. Token forwarding verified by `SendMailAsync_Success_DelegatesToClientOnceWithCallerToken` (`Times.Once` with the exact `cts.Token`). The `NotSupportedException` block is deleted; the stale "#74/#75" sentences are removed from both `HostAdapterSchedulingService.cs` (class doc now describes read + send delegation) and `SendMailRequest.cs` (kill-switch sentence retained). Grep of branch head for "deferred to issues #74/#75" in `src/` matches only the spec-sanctioned out-of-scope worker catch comment in `SchedulingWorker.Pipeline.cs`, which the spec explicitly excludes. |
| AC-2 | **PASS** | Implementation: `envelope is not { Ok: true }` throws `InvalidOperationException` containing `Error.Code` and `Error.Message` with explicit fallbacks; no catch block wraps the client call. Tests: `SendMailAsync_EnvelopeNotOk_ThrowsInvalidOperationWithCodeAndMessage` (asserts both `BRIDGE_UNAVAILABLE` and `bridge offline` in the message), `SendMailAsync_ClientThrows_PropagatesExceptionUnwrapped` (`ThrowExactlyAsync<HttpRequestException>`), `SendMailAsync_CanceledToken_PropagatesOperationCanceled`. All pass in the reviewer run. |
| AC-3 | **PASS** | `RunCycle_SendDisabled_NeverInvokesSendMail` (pre-existing, unchanged — default-off gate), `RunCycle_SendEnabled_InvokesSendMail` (`Times.Once` plus composed-request capture: `Re: Project sync` subject, single normalized recipient `colleague@contoso.com`, `Proposed times:` body — the spec's integration-style condition), `RunCycle_SendFailure_LogsAndContinues` (first send throws; cycle does not throw; second candidate hydrated; send attempted twice — per-message isolation via unchanged `ProcessMessageSafelyAsync`), `RunCycle_SendCancellation_StopsCycle` (`OperationCanceledException` propagates; second candidate never hydrated). Worker production code unchanged in diff, as spec requires. Log emission is structurally entailed rather than content-asserted (Info finding in the code review; does not weaken the isolation verdict). |
| AC-4 | **PASS** | `SendMailAsync_Throws_DeferredNotSupported` removed (visible in diff); replaced by exactly the five mandated delegation tests using MSTest + FluentAssertions + Moq. Worker suite gains the send-failure isolation case (plus the cancellation case). `SchedulingDtoMapperPropertyTests.cs` (new) provides the T1 property test for `MapSendMailRequest` (CsCheck 4.7.0, pre-existing dependency; 1000 iterations; recipient sequence preservation, `SaveToSentItems` invariant, input immutability; failing seed printed per suite convention). Fail-before evidence EXIT 1 (`expect-fail-sendmail-delegation.2026-07-02T10-58.md`), pass-after EXIT 0 (`pass-after-sendmail-delegation.2026-07-02T11-08.md`). |
| AC-5 | **PASS** | Authoritative diff file list: the only `src/` changes are the three `OpenClaw.Core` files (`Agent/Contracts/SendMailRequest.cs` doc-only, `Agent/Runtime/HostAdapterSchedulingService.cs`, `Agent/Runtime/SchedulingDtoMapper.cs`); the only `tests/` changes are the four `OpenClaw.Core.Tests` files. Zero changes under `src/OpenClaw.HostAdapter*`, `src/OpenClaw.MailBridge*`, contract projects, route files, or schema files. Remaining diff entries are feature docs/evidence and agent-memory Markdown. Matches executor evidence `evidence/qa-gates/diff-scope.2026-07-02T11-16.md`. |
| AC-6 | **PASS** | Reviewer re-ran the full toolchain at branch head: `csharpier check .` EXIT 0 (205 files); `dotnet build` 0 warnings / 0 errors (analyzers + nullable as errors); NetArchTest 2/2; full solution tests 671 passed / 0 failed / 5 pre-existing env-gated skips; fresh coverage pooled 90.56% line / 80.05% branch (thresholds 85%/75%), T1 `OpenClaw.Core` package 98.63%/91.82%, changed mapper file 96.30%/87.23% with all new lines and both new branch points covered; no regression vs baseline (90.51%/79.95%). The new async `SendMailAsync` body is uninstrumented under the pre-existing runsettings attribute exclusion and is behaviorally covered by the five delegation tests (documented in policy audit Section 8). |

## Summary

All six acceptance criteria evaluate to **PASS** against the full feature-vs-base diff, with each verdict backed by direct diff inspection, the reviewer's independent toolchain re-run, and executor regression evidence. The policy audit (`policy-audit.2026-07-02T11-25.md`) is FULLY COMPLIANT with no Blocking findings; the code review (`code-review.2026-07-02T11-25.md`) records three Info findings requiring no action on this branch. Remediation is not required. Recommendation: **Go for PR** targeting `main` (merge-commit policy). Operational note carried from the spec: `SendEnabled` remains default-off; the F6 idempotency feature should be sequenced immediately after merge to close the accepted duplicate-send interim risk.

## Acceptance Criteria Check-off

All six criteria were already checked off (`- [x]`) by the executor in all three source files (`spec.md`, `user-story.md`, and the `issue.md` mirror) at the time of this review. The reviewer independently verified each criterion as PASS above; per the acceptance-criteria-tracking protocol, no check-off edits were needed and none were made. No criteria were left unchecked; no phantom criteria were added.

### Acceptance Criteria Status
- Source: `docs/features/active/2026-07-02-wire-sendmail-runtime-99/spec.md`, `docs/features/active/2026-07-02-wire-sendmail-runtime-99/user-story.md` (mirror: `issue.md`)
- Total AC items: 6
- Checked off (delivered): 6
- Remaining (unchecked): 0
- Items remaining: none
