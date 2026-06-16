# Remediation Inputs — Cycle 1 — Issue #73

- **Canonical issue:** #73
- **Feature folder:** docs/features/active/mailbridge-messagedto-resolved-fields-73
- **Cycle entry timestamp:** 2026-06-14T09-00
- **Base branch / merge-base:** main / be2ddbf6559febc4ddfcf14a098025d96647f772
- **Head at cycle entry:** 9658ee76b2a997287aa36a6d22f4a353f8bb9890
- **Source review artifacts:**
  - policy-audit.2026-06-13T14-32.md
  - code-review.2026-06-13T14-32.md
  - feature-audit.2026-06-13T14-32.md
  - remediation-inputs.2026-06-13T14-32.md (feature-review handoff)

The end-of-cycle re-audit (feature-review) determines exit; `blocking_count == 0` is required to exit.
This cycle carries exactly two blocking findings. Operator approved the remediation approach for both
(2026-06-14). No scope narrowing of the original review is permitted at re-audit.

## RF-1 (Blocking) — New-file coverage below threshold: `ComMessageSource.cs`

- **Finding:** `src/OpenClaw.MailBridge/ComMessageSource.cs` measures 80.1% line / 60.9% branch,
  below the uniform new-code thresholds (line >= 85%, branch >= 75%). The per-project aggregate
  (90.90% / 80.39%) masked the new-file shortfall.
- **Detail:** Uncovered surface is (a) fail-soft fallback branches reachable via the existing test
  doubles — code-review cited lines ~175-176, 199-200, 215-218 — and (b) the COM-only SMTP-resolution
  chain (`PropertyAccessor` PR_SMTP_ADDRESS, `GetExchangeUser().PrimarySmtpAddress`) that cannot
  execute in a unit test without live Outlook COM.
- **Approved remediation approach:**
  1. Add MSTest unit tests (Moq + FluentAssertions, deterministic, no temp files, no real COM) that
     exercise every reachable fallback/branch in `ComMessageSource.cs` via the existing reflection-based
     fakes, including the SMTP fallback chain branches and the recipient enumeration paths.
  2. For the irreducible COM-only interop members that genuinely cannot be reached without live COM
     (the PropertyAccessor / GetExchangeUser hard COM calls), isolate them into the smallest possible
     members and apply a narrow, justified `[ExcludeFromCodeCoverage]` to those COM-interop-only members
     only — with an in-code justification comment naming the live-COM dependency. Do not exclude any
     member that is reachable by a unit test. Do not lower any threshold.
- **Exit condition:** `ComMessageSource.cs` measured new-code coverage >= 85% line / >= 75% branch on
  the testable surface, with exclusions limited to COM-only-unreachable members.

## RF-2 (Blocking) — File-size cap exceeded: `CoreCacheRepository.cs`

- **Finding:** `src/OpenClaw.Core/CoreCacheRepository.cs` is 699 lines, exceeding the 500-line cap in
  `.claude/rules/general-code-change.md`. Pre-existing at 687 lines at the merge-base; this feature
  added +12 mandatory in-method lines. The rule has no pre-existing exemption.
- **Approved remediation approach:** Extract a cohesive group of existing members (the message
  read/write/upsert/parameter-binding methods) from `CoreCacheRepository.cs` into a new partial file
  `src/OpenClaw.Core/CoreCacheRepository.Messages.cs`, following the file's existing partial-split
  convention (`CoreCacheRepository.Schema.cs` etc.). The extraction is behavior-preserving (same
  partial class, same members, same signatures); no logic change. Bring `CoreCacheRepository.cs` under
  500 lines and keep the new partial under 500 lines.
- **Constraint:** No public API change; no behavior change; existing tests must continue to pass
  unchanged. Do not push any other file over the cap.
- **Exit condition:** `CoreCacheRepository.cs` <= 500 lines; the new partial <= 500 lines; full test
  suite green; no regression on changed lines.

## Cycle exit gate

Re-audit (feature-review) at cycle exit must report 0 blocking findings across code-review,
feature-audit, and policy-audit, and AC-01..AC-11 all PASS (AC-11 coverage + file-size sub-criteria
resolved). Only then is `exit_condition_met = true`.
