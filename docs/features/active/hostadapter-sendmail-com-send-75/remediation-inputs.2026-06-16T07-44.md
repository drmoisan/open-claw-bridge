# Remediation Inputs: hostadapter-sendmail-com-send (#75)

**Entry Timestamp:** 2026-06-16T07-44
**Feature Folder:** `docs/features/active/hostadapter-sendmail-com-send-75`
**Base Branch:** `main` (merge-base `0cb7de6`)
**Head:** `feature/hostadapter-sendmail-com-send-75` (`269d3bba`)

## Source Audit Artifacts

- `docs/features/active/hostadapter-sendmail-com-send-75/policy-audit.2026-06-16T07-44.md`
- `docs/features/active/hostadapter-sendmail-com-send-75/code-review.2026-06-16T07-44.md`
- `docs/features/active/hostadapter-sendmail-com-send-75/feature-audit.2026-06-16T07-44.md`

## Findings Requiring Remediation

### R-1 (Blocking / FAIL) — Test file exceeds 500-line limit

- **File:** `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.cs` (573 lines).
- **Rule violated:** `.claude/rules/general-code-change.md` File Size Limit (no production, test, or reusable script file may exceed 500 lines; test code is not exempt). Also fails spec.md **AC-11** ("no file exceeds 500 lines").
- **Context:** baseline was 518 lines (already over limit); this branch added +55 lines without splitting.
- **Expected behavior:** split into focused test files or partial-class files, each under 500 lines, preserving all existing test methods and behavior. Suggested split: extract the newly added send_mail program-level tests into a sibling file (e.g., `MailBridgeProgramTests.SendMail.cs`) and, if the remaining file is still over 500, split the pre-existing tests by concern. Do not delete or weaken any test.
- **Verification commands:**
  - `for f in $(git diff --name-only 0cb7de6..HEAD | grep -E '\.cs$'); do wc -l "$f"; done | sort -rn` — every changed `.cs` file must report ≤ 500 lines.
  - `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --filter "TestCategory!=Integration"` — EXIT_CODE 0, no test count regression (587 passing baseline).
  - `csharpier check .` — EXIT_CODE 0.

### R-2 (Minor) — Inaccurate file-size claim in evidence

- **File:** `docs/features/active/hostadapter-sendmail-com-send-75/evidence/other/acceptance-criteria-map.md`.
- **Issue:** states "No file exceeds 500 lines" while `MailBridgeProgramTests.cs` is 573 lines.
- **Expected behavior:** after R-1, correct the statement to reflect the split result so the evidence is accurate.
- **Verification:** the corrected claim matches the `wc -l` output for all changed files.

### R-3 (PARTIAL → verify) — Live-COM integration evidence not produced

- **Files/tests:** `tests/OpenClaw.MailBridge.Tests/OutlookComMailSenderIntegrationTests.cs` (the two `[TestCategory("Integration")]` tests); the `[ExcludeFromCodeCoverage]` members of `src/OpenClaw.MailBridge/OutlookComMailSender.cs`.
- **Issue:** AC-06 and AC-10(a,b) are covered-by-design only; the integration tests `Assert.Inconclusive`-skipped on the review host (no live Outlook).
- **Expected behavior:** execute the integration suite on a live-Outlook host and capture a passing run (Sent Items entry created; COM send path exercised) to `docs/features/active/hostadapter-sendmail-com-send-75/evidence/regression-testing/`.
- **Verification command:** `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"` on a live-Outlook host — EXIT_CODE 0 with the two tests actually running (not Inconclusive).
- **Note:** if no live-Outlook host is available in the remediation environment, record a fail-before/coverage exception dossier per `evidence-and-timestamp-conventions` rather than marking the criteria PASS.

## Do Not Do

- Do not weaken, delete, or skip any existing test to satisfy the file-size limit; split only.
- Do not modify policy documents under `.claude/rules/` or `.github/instructions/`.
- Do not narrow scope or mark C# coverage out of scope.
- Do not add new `[ExcludeFromCodeCoverage]`, `#pragma warning`, `SuppressMessage`, or `#nullable disable` suppressions.
- Do not introduce temporary files in tests.
- Do not alter the additive contract surface (no breaking changes to existing routes, RPC verbs, or members).

## Target Remediation Plan

- `docs/features/active/hostadapter-sendmail-com-send-75/remediation-plan.2026-06-16T07-44.md`

Hand off plan authoring to `atomic-planner` per `.claude/skills/remediation-handoff-atomic-planner/SKILL.md`. The plan file already exists as a conforming scaffold; the planner updates it in place per the plan-path continuity contract.
