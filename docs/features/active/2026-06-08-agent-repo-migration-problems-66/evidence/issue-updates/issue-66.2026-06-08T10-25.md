# Issue #66 Update Mirror

Timestamp: 2026-06-08T10-25
PostedAs: unknown

POSTING NOTE: This is a local mirror of the intended issue update. The executor does not post to
GitHub; the orchestrator/PR-author posts the comment or updates the issue body. URL to be filled when posted.

---

## Intended update text

Harness migration correction for Issue #66 is complete. This is a documentation/policy/config-only
change (no product/test source modified).

Delivered:
- Removed 18 TypeScript/Python residue harness files (4 `.claude/rules`, 2 `.claude/agents`,
  12 `.github/agents/*`).
- Corrected `.claude/rules/*`, `.claude/agents/*`, `.claude/skills/*`, `.github/agents/orchestrator.agent.md`,
  `.github/instructions/csharp-*`/`general-unit-test`/`powershell-unit-test`, and `AGENTS.md` to the real
  stack: MSTest + Moq + FluentAssertions, `OpenClaw.MailBridge.sln`, `dotnet build`/`dotnet test`, global
  `csharpier`, coverage line >= 85% / branch >= 75%.
- Created `quality-tiers.yml` (all 9 solution projects classified T1–T4), `docs/ci.research.md`
  (section 1 tier system), and `.claude/agent-memory/orchestrator/remediation-loop-strict-handoff.md`.
- Qualified absent paths (`pester.runsettings.psd1`, `Test-BaselineProvenance.ps1`) as not-yet-present.

Verification: AC-01..AC-10 all PASS (qa-gate artifacts under
`docs/features/active/2026-06-08-agent-repo-migration-problems-66/evidence/qa-gates/`). Representative
smoke `dotnet build OpenClaw.MailBridge.sln` (0 warnings/errors) and
`dotnet test ... --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (298 passed,
3 skipped, 0 failed) confirm the corrected commands resolve.

Deferred follow-ups:
1. `scripts/dev-tools/sync-agents-from-instructions.ps1` (AGENTS.md generator) — AGENTS.md was hand-edited
   alongside its source instructions this pass (Option C).
2. `scripts/benchmarks/Test-BaselineProvenance.ps1` (benchmark validator) — qualified as absent, not authored.
3. `.config/dotnet-tools.json` (CSharpier local-tool manifest) — global csharpier used instead.
4. `.github/agents/*` REVIEW-classified personas (beast-mode set, hlbpa, mentor, commentary-remediation) —
   separate human decision.
5. OUT-OF-SCOPE residual: `.github/agents/csharp-typed-engineer.agent.md` L173-175 still has unqualified
   `msbuild TaskMaster.sln` / `vstest.console.exe`; recommend a follow-up cycle to correct it.
