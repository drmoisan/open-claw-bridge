# Phase 0 — Policy Instructions Read

- Timestamp: 2026-04-22T10:50:00Z
- Plan: `plan.2026-04-22T10-45.md`
- Phase: 0 (Baseline Capture)
- Feature Folder: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none-43/v2`

## Policy Order

All mandatory policy files were read in full in the following compliance order:

1. `AGENTS.md` — Primary instructions file for this repository (`.github/copilot-instructions.md` does not exist; `AGENTS.md` at the repo root is the designated substitute per plan task P0-T1 instruction). Content confirmed: general code change policy, bugfix workflow, design principles, error handling, module/file structure, naming conventions, and toolchain requirements.

2. `.github/instructions/general-code-change.instructions.md` — Baseline code change policy covering: design principles (simplicity, reusability, extensibility, separation of concerns), classes/functions/APIs, error handling and logging, module file structure (500-line limit), naming and documentation, performance/I/O/dependencies, and the mandatory toolchain loop (format → lint → type-check → test).

3. `.github/instructions/general-unit-test.instructions.md` — Baseline unit test policy covering: core principles (independence, isolation, fast execution, determinism, readability), coverage requirements (≥80% repo-wide, ≥90% for new modules), scenario completeness (positive/negative/edge/error), Arrange–Act–Assert pattern, external dependency avoidance, and the prohibition on temporary files in tests.

## Confirmation

- All three files were read in full.
- No conflicts with plan scope were detected. The plan's scope constraint (single-field JSON change, no C#/PowerShell/Dockerfile/docker-compose.yml modifications, no toolchain pass required) is consistent with general policy Section 6 (performance/I/O separation) and the bugfix workflow (minimal targeted fix).
- `.github/copilot-instructions.md` is absent from this repository. `AGENTS.md` (repo root) is confirmed as the primary agent instructions aggregate per repository convention.
- Work Mode confirmed as `full-bug` per `issue.md`. AC source file for this plan is `spec.md`.
