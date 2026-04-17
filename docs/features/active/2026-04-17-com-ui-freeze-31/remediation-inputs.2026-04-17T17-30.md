# Remediation Inputs: COM UI Freeze Bug Fix (Issue #31)

**Timestamp:** 2026-04-17T17-30
**Source Audit:** `policy-audit.2026-04-17T17-30.md`
**Feature Folder:** `docs/features/active/2026-04-17-com-ui-freeze-31`
**Work Mode:** minor-audit

---

## Enumerated Fix List

### Fix 1: Complete Phase 2 QA formatting artifact (P2-T1)

- **File to create:** `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-format.md`
- **Expected behavior:** Run `dotnet tool run csharpier .` from solution root. Record timestamp, command, exit code, and output summary. If files change, restart QC loop from P2-T1.
- **Verification command:** `dotnet tool run csharpier .`

### Fix 2: Complete Phase 2 QA analyzer artifact (P2-T2)

- **File to create:** `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-analyzers.md`
- **Expected behavior:** Run `dotnet build OpenClaw.MailBridge.sln -c Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`. Record timestamp, command, exit code, and output summary. If build fails, fix and restart from P2-T1.
- **Verification command:** `dotnet build OpenClaw.MailBridge.sln -c Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`

### Fix 3: Complete Phase 2 QA nullable artifact (P2-T3)

- **File to create:** `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-nullable.md`
- **Expected behavior:** Run `dotnet build OpenClaw.MailBridge.sln -c Debug /p:Nullable=enable /p:TreatWarningsAsErrors=true`. Record timestamp, command, exit code, and output summary. If build fails, fix and restart from P2-T1.
- **Verification command:** `dotnet build OpenClaw.MailBridge.sln -c Debug /p:Nullable=enable /p:TreatWarningsAsErrors=true`

### Fix 4: Complete Phase 2 QA test artifact (P2-T4)

- **File to create:** `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-test.md`
- **Expected behavior:** Run `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"XPlat Code Coverage"`. Record timestamp, command, exit code, output summary (passed/failed/skipped, line coverage percentage), and comparison to baseline (83.83%). If tests fail, fix and restart from P2-T1.
- **Verification command:** `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"XPlat Code Coverage"`

### Fix 5: Complete Phase 2 AC traceability artifact (P2-T5)

- **File to create:** `docs/features/active/2026-04-17-com-ui-freeze-31/final-qa-ac-check.md`
- **Expected behavior:** Map each of the 5 acceptance criteria to its evidence (test name, code location, or QA artifact). Mark each AC as satisfied with specific references.
- **Verification command:** Manual inspection of artifact completeness.

### Fix 6: Check off plan tasks P2-T1 through P2-T5

- **File to update:** `docs/features/active/2026-04-17-com-ui-freeze-31/plan.2026-04-17T13-00.md`
- **Expected behavior:** After each P2 task artifact is created, mark the corresponding plan task as `[x]`.
- **Verification command:** Grep for unchecked `- [ ]` items in Phase 2 of the plan.

---

## Do Not Do List

- Do not modify production code. The implementation is complete and correct.
- Do not modify or add tests. The test suite is complete.
- Do not change policy documents under `.github/instructions/`.
- Do not skip any Phase 2 QA step. If any step fails, fix and restart the QC loop from P2-T1.
- Do not silently suppress analyzer or nullable warnings.
- Do not weaken validation ranges or remove existing checks.
