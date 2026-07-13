# Phase 0 — Policy Read Evidence

Timestamp: 2026-07-12T09-00

Policy Order:
1. `AGENTS.md`
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/powershell.md`

Files read (in order):
- `AGENTS.md` (repo root) — read via Read tool; confirms Copilot-instructions -> general -> language-specific -> CI policy layering, general code-change policy (design principles, module/file structure 500-line cap, toolchain loop), general unit-test policy (coverage thresholds, scenario completeness), and PowerShell-specific coding standards contained within the generated file.
- `.claude/rules/general-code-change.md` — read (provided in-session as project instructions); design principles, module rigor tiers pointer, mandatory toolchain loop order, 500-line file cap, error handling/logging, naming, public API/compat, dependencies, I/O boundaries.
- `.claude/rules/general-unit-test.md` — read (provided in-session as project instructions); core test principles, coverage requirements (>=85% line / >=75% branch, uniform across tiers), coverage exclusion policy, scenario completeness, AAA structure, external-dependency mocking rules, test file location (mirror `tests/` tree), determinism infrastructure requirements.
- `.claude/rules/powershell.md` — read via Read tool; toolchain (PoshQC format -> analyze -> test via MCP), compatibility (PowerShell 7+), coding standards, change budget, design seams, testing standards, mocking rules, prohibited behaviors.

Additional Phase 0 confirmations (P0-T5):
- `docs/features/active/2026-07-11-installer-image-version-alignment-147/spec.md` contains an explicit `## Acceptance Criteria` section with 14 checkbox items (AC1-AC14), confirmed by direct read of the file (section starts at line 222, `- [ ]` items through AC14).
- `docs/features/active/2026-07-11-installer-image-version-alignment-147/research/research-findings.2026-07-11T20-15.md` exists, confirmed via directory listing of `docs/features/active/2026-07-11-installer-image-version-alignment-147/research/`.

All four policy files were read prior to any production or test file edits in this execution session.
