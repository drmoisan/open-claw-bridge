# negative-scope-smoke-test (Issue #120)

- Date captured: 2026-07-06
- Author: drmoisan
- Status: Promoted -> docs/features/active/negative-scope-smoke-test/ (Issue #120)
- Epic: openclaw-vision (Epic C, F17); depends on F11 (exchange-rbac-scripts, #111) and F13 (graph-backed-adapter, #115)

- Issue: #120
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/120
- Last Updated: 2026-07-07
- Work Mode: full-feature

## Problem / Why

Master `docs/open-claw-approach.master.md` §13 Step 2-3 requires that, before any business logic runs, the application proves its Exchange Online Application RBAC scope boundary: a harmless read against an in-scope mailbox must succeed and the same read against an out-of-scope mailbox must be denied, and the result must be logged as part of startup validation. Gap analysis `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` records this as F17: "No test exercises an in-scope-succeeds / out-of-scope-fails assertion" on the application-runtime side.

F11 delivered the administrator-side PowerShell boundary probe (`Test-OpenClawScopeBoundary` using `Test-ServicePrincipalAuthorization`). F13 delivered the Graph-backed mailbox adapter the running application uses. F17 delivers the application-runtime counterpart: a startup validation command that exercises the Graph read path against a configured in-scope and out-of-scope mailbox and asserts the expected authorization split, with mocked-Graph contract tests in the per-commit suite and a live-tenant human runbook recorded as a `human_interaction` exception (no Azure/Exchange credentials exist in this environment or CI).

## Proposed Behavior

- A host-neutral, deterministic negative-scope validator that, given a mailbox-read port (the F13 Graph adapter), performs a harmless read against a configured in-scope mailbox and against a configured out-of-scope mailbox, then evaluates the pair: success on in-scope AND authorization-denied (403/ErrorAccessDenied) on out-of-scope => boundary holds; any other combination => boundary fails with a precise reason.
- A startup validation entry point (command) that resolves the in-scope and out-of-scope test mailboxes from configuration, invokes the validator, logs the structured result, and returns a deterministic pass/fail signal so startup can fail-fast when the boundary is wrong.
- All tenant-dependent verification is delivered as mocked-Graph contract tests for the decision logic plus a human runbook for live-tenant verification (recorded as a `human_interaction` exception, mirroring the F11 HI-1 precedent).

## Acceptance Criteria (early draft)

- [ ] A host-neutral negative-scope validator evaluates the in-scope/out-of-scope read outcomes and returns a structured result (in-scope allowed, out-of-scope denied, succeeded, precise failure reason) without touching the network directly.
- [ ] A startup validation command wires the validator to the F13 Graph adapter, reads test-mailbox config, logs the structured result, and yields a deterministic pass/fail signal.
- [ ] Mocked-Graph contract tests cover the pass case and every failure combination (in-scope denied, out-of-scope allowed, both) with coverage thresholds held (line >= 85%, branch >= 75%).
- [ ] A human runbook documents live-tenant verification and is recorded in orchestrator state as a `human_interaction` exception with a valid runbook_path.

## Constraints & Risks

- No live tenant: every tenant-dependent step ships mocked; no Azure/Exchange credentials in this environment or CI.
- Must build on the F13 Graph adapter surface without breaking its contract; auth/token handling path (floor signal `auth_or_token_handling`).
- Distinguishing "authorization denied" (expected out-of-scope outcome) from other error classes must be precise so the boundary assertion is not fooled by unrelated failures.
- Adjacent latent gap flagged by F14 (#117): MessagePollingWorker.PersistPollResultAsync requires envelope.Meta.Bridge non-null, which the Graph backend never sets — queued follow-up; include only if research/spec deems it in-scope.

## Test Conditions to Consider

- [ ] Unit: validator pass matrix — (in-scope allowed, out-of-scope denied) => succeeded; (denied, denied), (allowed, allowed), (denied, allowed) => failed with precise reason.
- [ ] Unit: out-of-scope denial classification distinguishes authorization-denied from other errors.
- [ ] Integration/contract: startup command logs the structured result and returns the correct pass/fail signal against a mocked Graph adapter.
- [ ] Human runbook: live-tenant verification procedure (out of CI).

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/negative-scope-smoke-test/` folder from the template
