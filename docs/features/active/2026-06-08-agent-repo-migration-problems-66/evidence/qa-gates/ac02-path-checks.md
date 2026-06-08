# AC-02 — Referenced-Path Existence / Qualification (Issue #66)

Timestamp: 2026-06-08T09-46
Command: `Test-Path mailbridge.runsettings`; `rg -n "pester.runsettings.psd1" .claude .github AGENTS.md`; `rg -n "Test-BaselineProvenance.ps1" .claude`
EXIT_CODE: 0

Output Summary: AC-02 PASS.

- `mailbridge.runsettings` = True (present and referenced by the corrected `dotnet test` commands).
- `pester.runsettings.psd1` references are all qualified as not-yet-present:
  - `AGENTS.md:1248` — qualified ("not yet present in this repository").
  - `.claude/rules/powershell.md:18` — qualified (verified with `--no-ignore`; file is gitignored).
  - `.github/instructions/powershell-unit-test.instructions.md:22` — qualified.
- `Test-BaselineProvenance.ps1` reference in `.claude/rules/benchmark-baselines.md:29` — qualified
  ("is not yet present in this repository").

Every absent-path reference carries an explicit not-yet-present qualification; the one present path
(`mailbridge.runsettings`) resolves.
