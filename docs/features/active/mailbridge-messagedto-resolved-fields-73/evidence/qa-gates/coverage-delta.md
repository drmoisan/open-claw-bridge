# Final QA — Coverage Delta and Changed-Line No-Regression (Cycle 1 Remediation)

Timestamp: 2026-06-15T08-56
Command: compare P0-T5 baseline cobertura vs P3-T5 post-change cobertura (line-rate / branch-rate)
EXIT_CODE: 0

## Baseline (P0-T5, evidence/remediation-baseline/baseline-test-coverage.md)
- OpenClaw.MailBridge.Tests closure: line=92.04%, branch=83.29%
- OpenClaw.Core.Tests closure: line=89.57%, branch=78.44%
- ComMessageSource.cs: line=80.13%, branch=60.86% (RF-1 shortfall confirmed)

## Post-change (P3-T5, evidence/qa-gates/final-test-coverage.md)
- OpenClaw.MailBridge.Tests: line=93.9%, branch=87.0%
- OpenClaw.Core.Tests: line=89.6%, branch=78.4%
- ComMessageSource.cs: line=94.7%, branch=93.5%

## New/changed-code coverage
RF-2 changes (CoreCacheRepository partial extractions):
- All moved members are covered by the existing CoreCacheRepository test suite (the tests exercise
  the repository through the public interface, indifferent to which partial file the method lives in).
- Core.Tests line=89.6%/branch=78.4% confirms no regression.

RF-1 changes (ComMessageSource new tests):
- ResolveSenderSmtp: covered (catch path via throwing sender, PropertyAccessor path, fallback paths)
- ResolveFromSmtp/ResolveOnBehalfOfSmtp: covered (empty on-behalf-of, SMTP-shaped, non-SMTP chain,
  normalized fallback)
- ResolveAddressEntrySmtp: covered (null sender, PropertyAccessor, ExchangeUser, Address paths)
- ResolveViaPropertyAccessor: covered (null addressEntry, null PropertyAccessor, throw path, success)
- ResolveViaExchangeUser: covered (null addressEntry, null exchange user, throw path, success)
- LooksLikeSmtp / NormalizeAddress: covered via data-driven tests
- ComMessageSource.cs measured: line=94.7% >= 85% / branch=93.5% >= 75%. PASS.

## Verdict: PASS
- RF-1: ComMessageSource.cs line=94.7% >= 85%, branch=93.5% >= 75%. PASS.
- RF-2: CoreCacheRepository files unchanged behavior; Core.Tests line=89.6% >= 85%, branch=78.4% >= 75%. PASS.
- No regression on changed lines. Both affected projects remain above thresholds.
- [ExcludeFromCodeCoverage] additions: none. Coverage passes without exclusions.
- Uncovered lines in ComMessageSource.cs (111-114, 150-160): outer catch blocks unreachable via fakes
  because all inner COM helpers (GetOptionalMemberValue, GetOptionalString) are fail-soft; not a
  coverage regression — these lines were uncovered in the baseline too (RF-1 shortfall).
- Out-of-scope finding: OpenClaw.HostAdapter.Tests branch=66.0% < 75% threshold — pre-existing
  condition not changed by this feature branch.
