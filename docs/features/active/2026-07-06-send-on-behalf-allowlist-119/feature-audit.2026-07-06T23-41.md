# Feature Audit: send-on-behalf-allowlist (#119)

**Audit Date:** 2026-07-06
**Auditor:** feature-review agent

## Scope and Baseline

- **Resolved base branch:** `epic/openclaw-vision-integration` (caller-supplied; reviewer-confirmed with `git merge-base`).
- **Merge-base SHA:** `d67dea0117984b980b093f1c942c9a4762b8b25f` (2026-07-06T22:49:06-04:00, merge of PR #121).
- **Branch head:** `03e80e25e9ba75cd463e2b32b46548f14dc416b5` (single commit "feat(core): gate send-on-behalf with principal-mailbox allowlist").
- **Diff:** 32 files, +2202/-34 — 4 production `.cs` (1 new), 6 test `.cs` (2 new), 20 feature docs/evidence/runbook `.md`, 2 agent-memory `.md`. C# is the only changed code language.
- **Work mode:** `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`). Acceptance-criteria sources per the acceptance-criteria-tracking contract: `spec.md` **and** `user-story.md`. (`issue.md`'s own "Acceptance Criteria (early draft)" section is unfilled template scaffolding; it is not an AC source in `full-feature` mode.)
- **Evidence basis:** reviewer-regenerated PR-context artifacts (`artifacts/pr_context.summary.txt`, `artifacts/pr_context.appendix.txt`), a fresh reviewer toolchain run at branch head (format, build, architecture subset, full solution test + coverage with deduped per-file cobertura parsing), executor evidence under `evidence/`, and direct code inspection. See `policy-audit.2026-07-06T23-41.md` for command references.

## Acceptance Criteria Inventory

### Source 1: `spec.md` — `## Acceptance Criteria` (10 items, all checked `[x]` by the executor)

| # | Criterion (abbreviated) |
|---|---|
| S1 | Additive `AllowedPrincipalMailboxUpns` collection property, empty default; indexed keys bind via in-memory ConfigurationBuilder test; whitespace-only entry fails `ValidateOnStart` |
| S2 | Pure static `Authorize` returning the three-value enum with trimmed `OrdinalIgnoreCase` comparison; unit tests cover all seven decision-table rows |
| S3 | Four CsCheck properties: case-invariance, deny-completeness, membership soundness, self-send dominance |
| S4 | Disallowed principal yields `UNAUTHORIZED` / `SendOnBehalfDenied` / `Retryable=false` with correct meta, zero handler invocations, strict token provider never called |
| S5 | Deny message names the config key with no UPN; deny path logs one warning with request id only |
| S6 | Allowlisted principal sends with `from = {p}`; case-differing entry permits; existing self-send test passes unmodified with empty allowlist (single decision source) |
| S7 | Validator rejects whitespace-only entries when Enabled, accepts empty list, ignores when disabled; unit tests plus CsCheck property |
| S8 | Existing architecture-boundary and contract-parity tests pass unchanged; D5 error-mapping and throttling tests pass with an allowlisted configuration |
| S9 | Seven-stage toolchain single pass; line >= 85% / branch >= 75% with changed lines covered; Stryker >= 75% in the pre-merge/nightly pipeline; files <= 500 lines; no live Graph calls; no temp files |
| S10 | Runbook exists, cross-references F11, covers reconciliation/live sends/deny verification/rendering; checkpoint records the `human_interaction` exception with the runbook path |

### Source 2: `user-story.md` — `## Acceptance Criteria` (7 items, all checked `[x]` by the executor)

| # | Criterion (abbreviated) |
|---|---|
| U1 | Allowlisted differing principal (exact/case/whitespace entry) posts to `users/{a}/sendMail` with `from = principal`, 202 -> success |
| U2 | Non-allowlisted differing principal (incl. empty default) -> `ok:false`, `UNAUTHORIZED`/`SendOnBehalfDenied`/`Retryable=false`, zero I/O (handler zero invocations, strict token provider never called) |
| U3 | Self-send (case-insensitive) proceeds with no `from` for any allowlist; existing self-send test unmodified; CsCheck dominance + case-invariance properties |
| U4 | Deny message names the key, contains no UPN; deny warning log carries request id only |
| U5 | Indexed-key configuration binds; whitespace-only entry fails startup validation naming the key |
| U6 | Authorization decision and from-injection share the single `SendOnBehalfAuthorizer.Authorize` source; all decision-table rows + four properties satisfy the T1 obligation |
| U7 | Tenant-side validation documented in the runbook and recorded as a `human_interaction` exception — not claimed as automated verification |

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|----|---------|----------|
| S1 | **PASS** | `GraphAdapterOptions.cs` adds `public IList<string> AllowedPrincipalMailboxUpns { get; } = new List<string>();` (get-only, binder-compatible, empty default). `AddGraphHostAdapterClient_BindsIndexedAllowlistKeysToTheCollection` binds `:0`/`:1` keys via in-memory configuration and asserts order; `AddGraphHostAdapterClient_WhitespaceAllowlistEntry_FailsValidateOnStart` asserts `OptionsValidationException` through the real DI/`ValidateOnStart` path. Both pass in the reviewer run. |
| S2 | **PASS** | `SendOnBehalfAuthorizer.Authorize` is `internal static`, pure (no I/O/clock/logging), returns `SendAuthorizationDecision`; comparisons are `Trim()` + `OrdinalIgnoreCase` (lines 80-92). `SendOnBehalfAuthorizerTests` (10 tests) covers rows 1-5 and 7 directly plus dominance and padded-principal variants; row 6 (whitespace-only entry) is correctly validator-territory per the spec ("never reaches the authorizer") and is covered under S7. Reviewer coverage: 100.00% line, 87.50% branch (single partial arm is the defensive null-entry guard — Minor CR-119-01, non-gating). |
| S3 | **PASS** | `SendOnBehalfAuthorizerPropertyTests` implements exactly the four named properties at `iter: 1000` with seeded CsCheck (failing seed printed): `Authorize_CaseInvariance_RandomCasingNeverChangesTheDecision`, `Authorize_DenyCompleteness_AllowlistExcludingDifferingPrincipal_AlwaysDenies`, `Authorize_MembershipSoundness_InsertingThePrincipal_AlwaysAllowsOnBehalf` (any casing/padding, any insert position), `Authorize_SelfSendDominance_PrincipalEqualsAssistant_AlwaysAllowsSelf`. All pass in the reviewer run. |
| S4 | **PASS** | `SendMail_NonAllowlistedPrincipal_DeniesBeforeAnyIo` and `SendMail_EmptyAllowlist_DeniesBeforeAnyIo` assert all envelope fields (`UNAUTHORIZED`, `SendOnBehalfDenied`, `Retryable=false`, `Meta.RequestId`, `AdapterVersion "cloudgraph"`, `Data` null), handler invocation count 0, and `Times.Never` on a `MockBehavior.Strict` no-setup `IAppTokenProvider` (any call would throw). Code inspection confirms the gate returns `Task.FromResult` before `executor.ExecuteAsync` (`SendMail.cs:43-66`), and token acquisition lives inside the executor. Fail-before/pass-after: EXIT 1 (exactly these deny tests failing) -> EXIT 0 (`evidence/regression-testing/send-on-behalf-gate-fail-before.2026-07-06T23-13.md`). |
| S5 | **PASS** | `SendMail_Denied_MessageNamesKeyAndLogsOneWarningWithRequestIdOnly` asserts the message contains `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` and neither UPN, and via `RecordingLogger` that exactly one warning is logged containing the request id and no UPN. Production message/log template inspection confirms no UPN interpolation (`SendMail.cs:46-63`). |
| S6 | **PASS** | `SendMail_AllowlistedPrincipal_InjectsFromAndSucceeds` asserts the POST path `/v1.0/users/amy%40contoso.com/sendMail` and `message.from.emailAddress.address == paula@contoso.com` with 202 -> ok/null; `SendMail_CaseDifferingAllowlistEntry_PermitsTheSend` covers the case-differing entry; `SendMail_SelfSendEmptyAllowlist_SucceedsWithoutFrom` proves self-send with an empty allowlist omits `from`. Diff-hunk analysis (executor record, reviewer-confirmed) shows the pre-existing `SendMail_PrincipalEqualsAssistant_OmitsFrom` body is textually unmodified. Single source verified in code: `ComposeSendMailBody(request, decision)` injects iff `AllowedOnBehalf`; the inline comparison was deleted. |
| S7 | **PASS** | Validator rule `options.AllowedPrincipalMailboxUpns.Any(string.IsNullOrWhiteSpace)` under the `Enabled` guard, full-violation-list style, message names the key and echoes no value. Tests: DataRow matrix (empty/space/tab, exactly one violation), empty-list validity, clean-list validity, disabled-mode pass-through, plus the CsCheck property `Validate_AllowlistEntryProperty_WhitespacePresenceDrivesTheViolation` (`iter: 1000`). Reviewer coverage: 100.00% line / 100.00% branch on the validator file. |
| S8 | **PASS** | Reviewer isolation run of `CloudGraphArchitectureBoundaryTests`: 3/3 pass. `CloudGraphContractParityTests` pass with a fixture-only helper change (allowlist seeded; assertions unchanged — executor diff-scope record, reviewer-confirmed). Pre-existing D5 error-mapping (`SendMail_TerminalStatus_MapsPerTheD5Matrix`) and throttling (`SendMail_ThrottledExhaustion_...`) tests pass with the helper-allowlisted configuration inside the 745/745 run. |
| S9 | **PASS** (mutation component pipeline-deferred per policy) | Reviewer single-pass toolchain at head: CSharpier 325 files EXIT 0; build 0 warnings/0 errors (analyzers + nullable as errors); architecture 3/3; 1192 solution tests passed, 0 failed; contract surfaces unchanged (empty diffs); integration = handler-seam contract tests (live tenant out of scope per D7). Coverage: Core.Tests report 94.61% line / 86.08% branch; new file 100.00%/87.50%; modified logic files 100.00%/100.00% — every changed executable line covered. All 10 touched `.cs` files <= 500 lines (max 489). No live Graph calls, no temp files (reviewer grep + executor test-hygiene gate). The Stryker >= 75% clause is scoped by the AC itself to the pre-merge/nightly pipeline; per `.claude/rules/general-unit-test.md` mutation tests run in pipelines, not the per-commit loop (same disposition as the #80-#117 T1 audits). Note: no Stryker configuration currently exists in the repository's workflows — a pre-existing repo-wide pipeline gap, recorded here for transparency, not a failure of this branch. |
| S10 | **PASS** | Runbook exists at `runbooks/send-on-behalf-validation.runbook.md`: cross-references the F11 runbook for the grant procedure (Prerequisite 6, Step 1) instead of duplicating it, covers allowlist/tenant-grant bidirectional reconciliation (Step 3), one allowed live send with rendered-appearance verification in Outlook and OWA (Step 4), deny-envelope expectations for the non-allowlisted direction (Step 3 checklist naming `SendOnBehalfDenied`), and the Send As absence check with the precedence hazard (Step 5), with cited Microsoft Learn sources. The orchestrator checkpoint (`artifacts/orchestration/orchestrator-state.json`) records requirement `HI-119-01` with `response: "exception"` and that exact `runbook_path` — reviewer-verified against all three `human_interaction` invariants in `.claude/rules/orchestrator-state.md`. |
| U1 | **PASS** | Same evidence as S6 (exact and case-differing entries; whitespace-padded membership covered by `Authorize_WhitespacePaddedMember_ReturnsAllowedOnBehalf` at the unit layer and the membership-soundness property with padded variants). |
| U2 | **PASS** | Same evidence as S4 (both deny tests, zero-I/O proofs, fail-before/pass-after). |
| U3 | **PASS** | Same evidence as S6 self-send plus the dominance and case-invariance properties (S3). |
| U4 | **PASS** | Same evidence as S5. |
| U5 | **PASS** | Same evidence as S1 binding/`ValidateOnStart` tests and S7 validator message assertions. |
| U6 | **PASS** | Single-source verified in code (S6); all seven decision-table rows tested (S2); four CsCheck properties (S3) plus the validator property satisfy the T1 property-per-pure-function obligation for both pure functions this branch adds. |
| U7 | **PASS** | Same evidence as S10; the spec, user-story, and human-interaction record all state the tenant items are not claimed as automated verification. The code/tenant boundary is correctly drawn (policy audit Section 8, caller-requested evaluation). |

**Verdict totals:** 17 PASS, 0 PARTIAL, 0 FAIL, 0 UNVERIFIED.

## Summary

All seventeen acceptance criteria across the two authoritative sources pass with reviewer-independent evidence. The security contract the feature exists to establish is proven at three levels: pure-function decision semantics (10 directed tests over the full decision table + 4 seeded CsCheck properties), the client seam (deny-before-any-I/O proven with a zero-invocation handler and a strict never-called token provider, plus fail-before/pass-after evidence), and configuration shape (validator rule + real `ValidateOnStart` path). The fail-closed default is structural (empty get-only collection) and generalized by the deny-completeness property. The from-injection predicate and the authorization decision demonstrably share one source. The tenant-side residue is correctly carved out into a complete, source-cited runbook recorded as a structurally valid checkpoint exception (`HI-119-01`).

Non-blocking observations are recorded in `code-review.2026-07-06T23-41.md`: the untested null-entry defensive arm (CR-119-01, Minor), the baseline-evidence staleness relative to the merge-base (CR-119-02, Minor, reviewer-neutralized), the pre-existing untrimmed from-injection value (Info), the intentional trimming strengthening of the D7 comparison (Info), and the auto-property instrumentation exclusion with behavioral coverage (Info). None affects any acceptance criterion or gate.

**Go/no-go recommendation: Go — ready for PR.** The PR description must call out the intended behavior change for existing deployments with `{p} != {a}` and no allowlist (silent representation becomes denial), per spec Versioning.

## Acceptance Criteria Check-off

All 17 AC items were already checked `[x]` in the source files by the executor during plan execution. The reviewer independently verified each as PASS above; per the acceptance-criteria-tracking protocol (reviewers check off PASS items not already checked), **no new check-offs were required** and no source file was modified by this review. The `spec.md` "Definition of Done" and "Seeded Test Conditions" checklists remain unchecked; they are planning scaffolding, not acceptance criteria, and were not modified per the no-phantom-criteria and preserve-text rules.

### Acceptance Criteria Status
- Source: `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/spec.md` and `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/user-story.md`
- Total AC items: 17 (10 spec + 7 user-story)
- Checked off (delivered): 17
- Remaining (unchecked): 0
- Items remaining: none
