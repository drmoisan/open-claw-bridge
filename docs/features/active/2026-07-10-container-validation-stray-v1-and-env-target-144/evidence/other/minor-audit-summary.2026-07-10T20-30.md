# Minor-Audit Summary — Issue #144 (P2-T9)

- Timestamp: 2026-07-10T20-30
- Work Mode: minor-audit (AC source: `issue.md` `## Acceptance Criteria`, AC1–AC7)
- Overall disposition: PASS (all 7 acceptance criteria delivered and verified with evidence)

## AC → Task → Evidence Mapping

| AC | Status | Delivering tasks | Supporting evidence |
|---|---|---|---|
| AC1 — probe URL `/v1/status` -> `/status` | PASS | P1-T2, P1-T9, P1-T10 | `evidence/qa-gates/final-poshqc-test.2026-07-10T20-30.md` (HostAdapter assertions `$execRequest` matches `/status`, not `/v1/`); production diff in `OpenClawContainerValidation.psm1` |
| AC2 — `HostAdapterInContainer` `ExpectedCondition` references `/status` | PASS | P1-T3, P1-T11 | `evidence/qa-gates/final-poshqc-test.2026-07-10T20-30.md` (`$probe.ExpectedCondition` matches `/status`, not `/v1`) |
| AC3 — default `-EnvFilePath` resolves to operator `.env` with `./.env` fallback (pure helper) | PASS | P1-T4, P1-T5, P1-T6, P1-T7, P1-T8, P1-T12, P1-T13 | `evidence/qa-gates/final-poshqc-test.2026-07-10T20-30.md` (5 module-level + 2 entry-script tests, present + absent cases) |
| AC4 — no regression; full suite + clean format/analyze single pass | PASS | P2-T1, P2-T2, P2-T3 | `final-poshqc-format`, `final-poshqc-analyze`, `final-poshqc-test` (416 passed / 0 failed), `final-poshqc-coverage` (all `.2026-07-10T20-30.md`) |
| AC5 — no `src/OpenClaw.HostAdapter/**` change | PASS | P2-T5 | `evidence/qa-gates/ac5-hostadapter-unchanged.2026-07-10T20-30.md` (zero changed HostAdapter paths) |
| AC6 — dashboard-access docs corrected (README + runbook) | PASS | P1-T14, P1-T15, P2-T7 | `evidence/qa-gates/ac6-doc-accuracy.2026-07-10T20-30.md` (removed claims = 0 matches; `#token=`, `openclaw devices clear`, floating-tag note present) |
| AC7 — dashboard validation reports auth accurately; new in-container token check; no handshake | PASS | P1-T16, P1-T17, P1-T18, P1-T19, P1-T20, P2-T8 | `evidence/qa-gates/ac7-dashboard-text.2026-07-10T20-30.md`; `final-poshqc-test` (GatewayTokenInContainer present/absent + dashboard-text tests) |

## Coverage

Line coverage 92.41% (>= 85%), command/instruction branch proxy 91.73% (>= 75%), no regression vs baseline (line 91.73%, command 91.08%); no new uncovered lines on changed code. Detail: `evidence/qa-gates/final-poshqc-coverage.2026-07-10T20-30.md`.

## Toolchain (PowerShell)

Format: pass, 0 files changed. Analyze: pass, 0 errors. Test: 416 passed / 0 failed (full `tests/scripts` suite).

## Deviations (recorded for auditor)

1. **Module manifest edit (`OpenClawContainerValidation.psd1`).** P1-T6 named only the `Export-ModuleMember` list in the `.psm1`, but the manifest's `FunctionsToExport` also gates exports; the three new functions were unresolved until added to the manifest. The manifest edit is mechanically necessary to achieve the tasks' stated outcome (export the new functions) and is a data/export-declaration file, not new executable logic.
2. **`Invoke-OpenClawContainerPathValidation.Tests.ps1` 'emits JSON' test.** P1-T19 named two full-run tests to update; a third full-run test (`'emits JSON when requested'`) also asserts `OverallResult = Expected` and `SupportingDiagnostics.Count = 14`, so it required the same mechanical token-exec handler + count `14 -> 15` to keep the full suite green (AC4/P2-T3). Applied the identical mechanical change the plan already prescribed for the other two full-run tests.
3. **AC7 dashboard wording "signed in" vs "authenticated".** P1-T20(c) asserts the `ExpectedCondition` must not match `authenticat` while P1-T16 asks the text to state it "does not verify operator authentication". To satisfy both (the plan explicitly permits "the exact wording chosen in P1-T16"), the ExpectedCondition/summary use "does not verify that an operator is signed in"; the intent (not implying authenticated access) is preserved.
4. **`docs/mailbridge-runbook.md` line ~494 `/v1/status` reference NOT changed.** The runbook's `HostAdapterInContainer` expectation bullet still reads `.../4319/v1/status`, which the AC1 code change makes factually stale. The plan's P1-T15 enumerated the runbook lines to edit (22, 492, 622, 636) and did not include this bullet, and no AC grep gate covers it. Left unchanged to respect the plan's explicit enumeration; flagged here as a recommended documentation follow-up.
