# Docs Refinement Artifact (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: `git diff --stat -- README.md docs/mailbridge-runbook.md`
EXIT_CODE: 0
Output Summary:
- `README.md` — 9 line deltas: "What It Does" scripted-bundle bullet rewritten to the `cd artifacts/publish/<version>; .\Install.ps1` flow; "Repository Layout" scripts/ row noted that `Install.ps1`/`Uninstall.ps1`/`Install.Helpers.psm1` are additionally staged into every bundle.
- `docs/mailbridge-runbook.md` — 31 line deltas: Path D prose rewritten to describe `$PSScriptRoot` self-location; all `.\scripts\Install.ps1` invocations replaced with `cd artifacts/publish/<version>; .\Install.ps1`; `-Version '1.2.3.0'` example removed; `-SourcePath` annotated as dev/test override; new troubleshooting row added for "`manifest.json not found at '<path>\manifest.json'`".
- Install Path headings preserved: Path A (line 46), Path B (line 166), Path C (line 363), Path D (line 471) — zero removals of Path A/B/C sections.
- No unrelated files modified.

Acceptance: PF-T7 passes.
