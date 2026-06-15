# Remediation Inputs: mailbridge-messagedto-resolved-fields (#73)

**Generated:** 2026-06-13T14-32
**Feature Folder:** `docs/features/active/mailbridge-messagedto-resolved-fields-73`
**Base Branch:** `main` (`be2ddbf6559febc4ddfcf14a098025d96647f772`)
**Head:** `feature/mailbridge-messagedto-resolved-fields-73` (`9658ee76b2a997287aa36a6d22f4a353f8bb9890`)
**Work Mode:** `full-feature` (fail-closed default; `issue.md` absent)

## Source Audit Artifacts

- `docs/features/active/mailbridge-messagedto-resolved-fields-73/policy-audit.2026-06-13T14-32.md`
- `docs/features/active/mailbridge-messagedto-resolved-fields-73/code-review.2026-06-13T14-32.md`
- `docs/features/active/mailbridge-messagedto-resolved-fields-73/feature-audit.2026-06-13T14-32.md`

## Remediation-Required Findings

### RF-1 (Major / Blocking for PASS) — New-file coverage below uniform threshold

- **File:** `src/OpenClaw.MailBridge/ComMessageSource.cs` (NEW)
- **Measured:** 80.1% line (121/151), 60.9% branch (28/46) — below uniform new-code thresholds (line >= 85%, branch >= 75% per `quality-tiers.md` / `general-unit-test.md`).
- **Uncovered lines:** 111-114, 150-154 (fail-soft catch handlers); 175-176, 199-200, 215-218 (SMTP fallback branches); 249-258 (PropertyAccessor `PR_SMTP_ADDRESS` path); 282-292 (`GetExchangeUser` path).
- **Triggers AC-11 FAIL.**
- **Required remediation:**
  - Add unit tests via the reflection-based COM doubles (`MailBridgeMessageSourceTestDoubles.cs`) that exercise the reachable pure-logic fallback branches: `ResolveOnBehalfOfSmtp` already-SMTP short-circuit (175-176), the `senderAddress` SMTP branch and `entryAddress` fallback (199-200, 215-218), and at least one catch path (a double member that throws).
  - For the genuinely live-COM-only paths (`ResolveViaPropertyAccessor`, `ResolveViaExchangeUser` via `InvokeMember`), either extend the doubles to simulate `PropertyAccessor.GetProperty` and `GetExchangeUser().PrimarySmtpAddress`, or record an operator-approved coverage-exclusion disposition documenting why these are unreachable without live COM.
  - Re-measure per-file coverage and confirm line >= 85% / branch >= 75% (or documented disposition).
- **Evidence:** `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/coverage-review/e309de69-9e3a-4b99-acb6-4b5dea06be13/coverage.cobertura.xml`

### RF-2 (Major) — Production file exceeds 500-line cap

- **File:** `src/OpenClaw.Core/CoreCacheRepository.cs`
- **Measured:** 699 lines (> 500 cap). Pre-existing (687 at base `be2ddbf`); this feature grew it by net +12 lines.
- **Triggers AC-11 FAIL (file-size sub-criterion).**
- **Required remediation:** Extract unrelated existing methods (e.g. `UpsertEventsAsync`/`ReadEvent`) into a partial file to bring `CoreCacheRepository.cs` to <= 500 lines, OR obtain and record a formal accepted-exception for the pre-existing over-cap file. `general-code-change.md` has no pre-existing-file exemption.
- **Evidence:** `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/other/file-size-check.md`

### RF-3 (Minor / non-blocking) — Silent fail-soft catch handlers

- **File:** `src/OpenClaw.MailBridge/ComMessageSource.cs` lines 111-114, 256-258, 290-292
- **Finding:** Broad `catch { }` handlers degrade fail-soft (per D-C) but log nothing, so live COM/config faults are unobservable.
- **Recommended remediation:** Add `debug`/`trace`-level logging on the catch paths without changing fail-soft behavior. Aligns with `general-code-change.md` (do not silently ignore errors). Not blocking for merge.

### RF-4 (Info / non-blocking) — Secondary AC source not authored

- **File:** `docs/features/active/mailbridge-messagedto-resolved-fields-73/user-story.md`
- **Finding:** Contains only unfilled template placeholders (Criterion 1/2/3); under `full-feature` it is a secondary authoritative AC source.
- **Recommended remediation:** Author the user-story acceptance criteria or remove the placeholder section. Not blocking; substantive ACs live in spec.md.

### RF-5 (Info / non-blocking) — Stale PR context summary file overview

- **File:** `artifacts/pr_context.summary.txt`
- **Finding:** "Changed files overview" reports `Core logic changes: 0 files` and lists only docs/tooling, inconsistent with the 18-file `.cs` diff.
- **Recommended remediation:** Regenerate PR context before opening the PR so the file overview reflects the source diff.

## Verification Commands (post-remediation)

```bash
dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true
dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
# format stage requires fixing the dotnet tool manifest (declares `csharpier`; package exposes `dotnet-csharpier`) before `dotnet csharpier check .` is runnable locally
```

## Disposition

Remediation is required because AC-11 is FAIL (RF-1 coverage, RF-2 file-size) and the policy audit contains FAIL results. RF-3/RF-4/RF-5 are non-blocking recommendations. Hand off to the atomic planner per `remediation-handoff-atomic-planner` to author the remediation plan addressing RF-1 and RF-2 as the blocking scope.
