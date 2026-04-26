# Policy Audit — Issue #45: Calendar Windows Wrong (UTC Double-Shift)

Timestamp: 2026-04-25T17-00
Reviewer: feature-review-agent
Work Mode: full-bug
AC Source: spec.md (full-bug) — no spec.md present; falling back to issue.md AC section per work-mode rule
Merge-Base SHA: 26f0dd5be8cbd80854a49911c85264f81cfe2eed
Branch: feature/20260425175224-calendar-windows-wrong

---

## Scope Determination

The full branch diff (working-tree uncommitted changes included) was used as the review scope. Files in scope:

**Committed changes** (26f0dd5..HEAD):
- `docs/mailbridge-runbook.md`
- `scripts/Uninstall.ps1`
- `tests/scripts/Uninstall.Tests.ps1`

**Uncommitted working-tree modifications** (unstaged):
- `src/OpenClaw.MailBridge/OutlookComHelpers.cs`
- `src/OpenClaw.MailBridge/OutlookScanner.cs`
- `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`

**Untracked new files** (never staged or committed):
- `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs`
- `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs`
- `docs/features/active/2026-04-25-calendar-windows-wrong-45/` (entire feature folder)

Languages with changed files: **C#** (3 source files), **PowerShell** (2 files), **Markdown** (2 files).

---

## CRITICAL FINDING: Implementation Not Committed

**FAIL** — The core Issue #45 implementation (all C# changes) is not committed to the branch.

Evidence:
- `git diff 26f0dd5..HEAD --name-only` returns only 3 files: `docs/mailbridge-runbook.md`, `scripts/Uninstall.ps1`, `tests/scripts/Uninstall.Tests.ps1`.
- `git status --short` shows `M src/OpenClaw.MailBridge/OutlookComHelpers.cs`, `M src/OpenClaw.MailBridge/OutlookScanner.cs`, `M tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs` as unstaged modifications.
- `OutlookComHelpersDateTimeKindTests.cs` and `OutlookScannerCalendarUtcTests.cs` are listed as `??` (untracked).
- The entire feature folder `docs/features/active/2026-04-25-calendar-windows-wrong-45/` is untracked.

This means merging the branch as-is would deliver only the uninstall bug fix (PR #51 content) and runbook edits. The calendar UTC fix would be lost. This is a blocking remediation finding.

---

## Policy Reading Order Compliance

| Policy File | Read | Notes |
|---|---|---|
| CLAUDE.md | PASS | Loaded from project instructions |
| .claude/rules/general-code-change.md | PASS | Loaded from project instructions |
| .claude/rules/general-unit-test.md | PASS | Loaded from project instructions |
| .claude/rules/csharp.md | PASS | Loaded; C# files are in scope |
| .claude/rules/powershell.md | PASS | Loaded; PowerShell files are in scope |

---

## Toolchain Compliance (C#)

Evidence source: `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/`

Note: All evidence artifacts are untracked (not committed), but the file contents were read and are evaluated at face value. The toolchain results were produced by the implementation agent and are accepted as working-tree evidence.

| Step | Command | EXIT_CODE | Verdict |
|---|---|---|---|
| Format (CSharpier) | `csharpier format .` + `csharpier check .` | 0 | PASS |
| Lint (.NET analyzers) | `dotnet build ... -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` | 0 | PASS |
| Nullable analysis | `dotnet build ... -p:Nullable=enable -p:TreatWarningsAsErrors=true` | 0 | PASS |
| Test | `dotnet test ... --collect:"Code Coverage"` | 0 | PASS (287 passed, 0 failed, 3 skipped) |

---

## Toolchain Compliance (PowerShell)

Evidence source: None present. No Pester test run artifact was produced for the PowerShell changes in this branch. The implementation agent's plan did not include a PowerShell toolchain phase because this feature targets the C# calendar bug. However, `scripts/Uninstall.ps1` and `tests/scripts/Uninstall.Tests.ps1` are committed changes on this branch and require PowerShell toolchain verification per policy.

**UNVERIFIED** — No `qa-format`, `qa-lint`, or `qa-test` artifacts exist for PowerShell in the feature evidence folder. PSScriptAnalyzer and Pester results are not on record for the Uninstall.ps1 and Uninstall.Tests.ps1 changes.

Context: These files were changed as part of an unrelated bug fix (PR #51 — uninstall-failure) that was merged into this worktree branch. The prior PR was merged to main before this feature branch was created. The question is whether the PowerShell toolchain was verified at the time of that PR. This cannot be confirmed from current evidence. The finding is recorded as UNVERIFIED rather than FAIL because the committed changes to these PowerShell files carry the merge-commit history of PR #51 which was separately reviewed.

---

## Coverage Compliance (C#)

| Metric | Required | Actual (qa-coverage-delta.md) | Verdict |
|---|---|---|---|
| Repo-wide line coverage >= 80% | >= 80% | 94.2% | PASS |
| `GetOptionalUtcDateTimeOffset` (new method) >= 90% | >= 90% | 90.0% (class-level proxy) | PASS (at threshold) |
| No regression vs baseline | >= 94.1% | 94.2% (+0.1%) | PASS |

Coverage artifact canonical path: `artifacts/csharp/coverage.xml`

**FAIL** — No `artifacts/csharp/coverage.xml` file exists. The only coverage artifacts found are binary `.coverage` files at:
- `tests/OpenClaw.Core.Tests/TestResults/277c2df8-.../DanMoisan_MEGALODON4_2026-04-25.19_09_21.coverage`
- `tests/OpenClaw.Core.Tests/TestResults/80d9fc79-.../DanMoisan_MEGALODON4_2026-04-25.19_04_43.coverage`

These are binary files from OpenClaw.Core.Tests only — they do not cover OpenClaw.MailBridge.Tests. No parseable coverage artifact exists from the post-change test run. Coverage metrics cited in `qa-coverage-delta.md` cannot be independently verified from a coverage artifact. This is a coverage artifact absence finding; however, it cannot override the face-value evidence from the QA gate documents. The coverage numbers (94.2%, 90.0%) are accepted as the implementation agent's reported values but cannot be confirmed from artifact.

---

## Coverage Compliance (PowerShell)

Coverage artifact canonical path: `artifacts/pester/powershell-coverage.xml`

**FAIL** — No `artifacts/pester/powershell-coverage.xml` file exists. PowerShell changed files (`Uninstall.ps1`, `Uninstall.Tests.ps1`) are in the branch diff and coverage verification is mandatory for all languages with changed files.

---

## File Size Limit (500 lines)

| File | Line Count | Verdict |
|---|---|---|
| `src/OpenClaw.MailBridge/OutlookComHelpers.cs` | 133 | PASS |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | 495 | PASS (under 500) |
| `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs` | 128 | PASS |
| `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs` | 79 | PASS |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs` | 400 | PASS |
| `scripts/Uninstall.ps1` | 97 | PASS |

No file exceeds the 500-line limit.

---

## Design Principles Compliance (C#)

- **Simplicity**: `GetOptionalUtcDateTimeOffset` is a direct extension of `GetOptionalDateTimeOffset` following the same pattern. PASS.
- **Separation of concerns**: Pure switch expression logic, no I/O. PASS.
- **Error handling**: Same `try { } catch { return null; }` boundary as the existing method. Broad catch-all is intentional at this COM boundary — consistent with the established pattern. PASS.
- **Naming**: `GetOptionalUtcDateTimeOffset` follows `PascalCase` public member convention. PASS.
- **XML docs**: `<summary>` doc comment present and describes the Unspecified-kind treatment. PASS.

---

## Evidence Location Compliance

Evidence location validation script (`validate_evidence_locations.py`) was not found in the repository. Manual scan performed.

Files checked for non-canonical evidence paths under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, `artifacts/coverage/`:
- No such files found in the branch diff or working tree under those paths.

The feature folder evidence is written under `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/` which is the canonical `<FEATURE>/evidence/<kind>/` scheme.

Evidence location compliance: PASS.

---

## Documentation Defect

**FAIL** — `docs/mailbridge-runbook.md` line 199 contains an unterminated string literal:

```
$versionNum = '1.0.1.3`
```

The string opens with a single-quote `'` but closes with a backtick `` ` `` instead of a single-quote `'`. This is a syntax error; executing this block in PowerShell will raise a parse error. The correct value should be:

```
$versionNum = '1.0.1.3'
```

---

## Summary Verdicts

| Category | Verdict |
|---|---|
| Implementation committed to branch | FAIL — C# changes are uncommitted/untracked |
| C# toolchain (format/lint/nullable/test) | PASS (face-value evidence; artifacts untracked) |
| PowerShell toolchain | UNVERIFIED — no PowerShell evidence artifacts |
| C# coverage artifact present | FAIL — `artifacts/csharp/coverage.xml` absent |
| C# coverage thresholds (face-value) | PASS (94.2% repo-wide, 90.0% new method) |
| PowerShell coverage artifact present | FAIL — `artifacts/pester/powershell-coverage.xml` absent |
| File size limits | PASS |
| Evidence location compliance | PASS |
| Documentation correctness | FAIL — runbook line 199 backtick syntax error |
