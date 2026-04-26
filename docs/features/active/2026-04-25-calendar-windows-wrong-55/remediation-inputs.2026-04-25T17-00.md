# Remediation Inputs — Issue #45: Calendar Windows Wrong (UTC Double-Shift)

Timestamp: 2026-04-25T17-00
Reviewer: feature-review-agent
Branch: feature/20260425175224-calendar-windows-wrong

---

## Remediation-Required Findings

### R-1 (BLOCKING): C# implementation not committed

**Severity**: Blocking — branch cannot be merged until resolved.

**Finding**: The core Issue #45 implementation exists only as uncommitted working-tree changes and untracked files. Merging the branch in its current state would deliver no fix for the calendar UTC double-shift defect.

**Files affected**:
- Unstaged modifications: `src/OpenClaw.MailBridge/OutlookComHelpers.cs`, `src/OpenClaw.MailBridge/OutlookScanner.cs`, `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`
- Untracked new files: `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs`, `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs`
- Untracked feature folder: `docs/features/active/2026-04-25-calendar-windows-wrong-45/`

**Required action**:
1. Stage all C# implementation files and the feature folder.
2. Run the full C# toolchain (CSharpier → dotnet build analyzers → dotnet build nullable → dotnet test) in a single clean pass from the staged state.
3. Commit the staged changes with an appropriate commit message referencing Issue #45.
4. Verify `git diff 26f0dd5..HEAD --name-only` includes all six C# files and the feature folder.

**Artifact paths**:
- Policy audit: `docs/features/active/2026-04-25-calendar-windows-wrong-45/policy-audit.2026-04-25T17-00.md`
- Code review: `docs/features/active/2026-04-25-calendar-windows-wrong-45/code-review.2026-04-25T17-00.md`
- Feature audit: `docs/features/active/2026-04-25-calendar-windows-wrong-45/feature-audit.2026-04-25T17-00.md`

---

### R-2 (BLOCKING): C# coverage artifact absent

**Severity**: Blocking — coverage verification is mandatory for all languages with changed files.

**Finding**: No `artifacts/csharp/coverage.xml` file exists. The binary `.coverage` files found under `tests/OpenClaw.Core.Tests/TestResults/` are not in parseable format and do not cover the MailBridge test project. The coverage numbers reported in `qa-coverage-delta.md` (94.2% repo-wide, 90.0% new method) cannot be independently verified from an artifact.

**Required action**:
1. Run `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"Code Coverage" --results-directory artifacts/csharp/` or equivalent to produce a machine-parseable coverage artifact.
2. Alternatively, use `dotnet-coverage collect` to produce `artifacts/csharp/coverage.xml` in Cobertura format.
3. Confirm repo-wide line coverage >= 80% and new-method coverage >= 90% from the artifact.
4. Store the artifact at `artifacts/csharp/coverage.xml` (canonical per coverage artifact table in the reviewer's policy).

**Artifact path**: `docs/features/active/2026-04-25-calendar-windows-wrong-45/policy-audit.2026-04-25T17-00.md` — section "Coverage Compliance (C#)"

---

### R-3 (BLOCKING): PowerShell coverage artifact absent

**Severity**: Blocking — coverage verification is mandatory for all languages with changed files.

**Finding**: No `artifacts/pester/powershell-coverage.xml` exists. `scripts/Uninstall.ps1` and `tests/scripts/Uninstall.Tests.ps1` are committed changes on this branch. Line coverage must be verified via Pester with coverage collection.

**Required action**:
1. Run Pester with coverage collection for the changed PowerShell files.
2. Produce `artifacts/pester/powershell-coverage.xml` (JaCoCo or Cobertura format).
3. Confirm repo-wide PowerShell line coverage >= 80% and that the changed `Uninstall.ps1` lines meet >= 80% (modified file) threshold.

**Artifact path**: `docs/features/active/2026-04-25-calendar-windows-wrong-45/policy-audit.2026-04-25T17-00.md` — section "Coverage Compliance (PowerShell)"

---

### R-4 (NON-BLOCKING): Runbook syntax error

**Severity**: Non-blocking for merge but must be fixed before the runbook is used operationally.

**Finding**: `docs/mailbridge-runbook.md` line 199 contains:
```
$versionNum = '1.0.1.3`
```
The string closes with a backtick instead of a single-quote. This is a PowerShell parse error.

**Required action**:
Replace line 199 with:
```
$versionNum = '1.0.1.3'
```

**Artifact paths**:
- Policy audit: `docs/features/active/2026-04-25-calendar-windows-wrong-45/policy-audit.2026-04-25T17-00.md` — section "Documentation Defect"
- Code review: `docs/features/active/2026-04-25-calendar-windows-wrong-45/code-review.2026-04-25T17-00.md` — section "File: docs/mailbridge-runbook.md"

---

## Summary Table

| ID | Description | Severity | Files |
|---|---|---|---|
| R-1 | C# implementation not committed | BLOCKING | OutlookComHelpers.cs, OutlookScanner.cs, MailBridgeRuntimeTestDoubles.cs, OutlookComHelpersDateTimeKindTests.cs, OutlookScannerCalendarUtcTests.cs, feature folder |
| R-2 | C# coverage artifact absent | BLOCKING | artifacts/csharp/coverage.xml (to be created) |
| R-3 | PowerShell coverage artifact absent | BLOCKING | artifacts/pester/powershell-coverage.xml (to be created) |
| R-4 | Runbook backtick syntax error | NON-BLOCKING | docs/mailbridge-runbook.md line 199 |
