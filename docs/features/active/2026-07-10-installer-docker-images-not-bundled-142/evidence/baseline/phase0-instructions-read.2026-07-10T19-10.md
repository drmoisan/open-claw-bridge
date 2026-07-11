# Phase 0 — Instructions Read Evidence (Issue #142)

Timestamp: 2026-07-10T19-10

Policy Order:
1. CLAUDE.md (standing instructions)
2. .claude/rules/general-code-change.md
3. .claude/rules/general-unit-test.md
4. .claude/rules/powershell.md

Files read:
- CLAUDE.md — No physical `CLAUDE.md` file exists at the repository root (`Glob **/CLAUDE.md` returned no files). The standing instructions are auto-loaded into the agent session context and were provided verbatim in the session system reminder; they were reviewed there. Recorded as a factual observation, not a deviation from the plan.
- .claude/rules/general-code-change.md — read (cross-language code change policy: design principles, mandatory 7-stage toolchain loop, 500-line file cap, error handling, naming, I/O boundaries).
- .claude/rules/general-unit-test.md — read (five core test properties, coverage >= 85% line / >= 75% branch, no coverage exclusion of production files, AAA structure, no temp files in tests, determinism infrastructure).
- .claude/rules/powershell.md — read (PoshQC toolchain order format -> analyze -> test, PowerShell 7+, ShouldProcess, wrapper-function design seam `Invoke-<Tool>Exe -<Tool>Args <string[]>` with parameter not named `Args`, mock-the-wrapper mocking rules, change budget and per-batch cap <= 3 prod / <= 3 test).

Notes:
- Work Mode for this feature is `full-bug`; `spec.md` is the sole acceptance-criteria source (AC1–AC12); `user-story.md` is intentionally absent.
- Change budget: 5 production PowerShell files change, so execution is batched per-phase (each batch <= 3 prod, <= 3 test), consistent with `.claude/rules/powershell.md`.
