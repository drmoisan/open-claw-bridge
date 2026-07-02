# Phase 0 Instructions Read — Issue #92 (Option B, plan v0.3)

Timestamp: 2026-07-01T20-30

Policy Order:
1. CLAUDE.md (standing instructions, always loaded)
2. .claude/rules/general-code-change.md (cross-language code change policy)
3. .claude/rules/general-unit-test.md (cross-language unit test policy)
4. Language-specific (C# in scope): .claude/rules/csharp.md
5. .claude/rules/quality-tiers.md (T1 tier obligations for this fix)
6. .claude/rules/architecture-boundaries.md (project-graph / COM-confinement enforcement)

Files Read:
- CLAUDE.md — read via harness-loaded standing instructions. No discrete `CLAUDE.md` file exists at the repo root; its content is delivered as always-loaded codebase/user instructions in this session context.
- .claude/rules/general-code-change.md — read (design principles, mandatory 7-stage toolchain loop, 500-line file limit, dependency policy: minimal change, use only approved libraries).
- .claude/rules/general-unit-test.md — read (five test properties; line >= 85% / branch >= 75% uniform; determinism infrastructure; no temp files in tests).
- .claude/rules/csharp.md — read (solution layout, toolchain order format -> lint/type -> architecture -> test, CSharpier, MSTest+Moq+FluentAssertions, coverage thresholds, no advisory suppression).
- .claude/rules/quality-tiers.md — read (T1-T4 tiers; uniform coverage thresholds; T1 = OpenClaw.Core and OpenClaw.HostAdapter critical).
- .claude/rules/architecture-boundaries.md — read (ProjectReference edges are the enforced boundary; OpenClaw.Core may depend only on OpenClaw.HostAdapter.Contracts; COM confined to OpenClaw.MailBridge).

Additional policy context read (relevant to this dependency-security fix):
- .claude/rules/benchmark-baselines.md — read (baseline provenance; not applicable, no benchmark baseline touched).
- .claude/rules/ci-workflows.md — read (pwsh exit-code rule; not applicable, no workflow YAML edited).
- .claude/rules/tonality.md — read (professional tone requirement).

Notes:
- Scope of this minor-audit fix (Option B): add a direct SQLitePCLRaw 3.x reference identically (lockstep) to both csproj to force transitive SQLitePCLRaw.lib.e_sqlite3 >= 3.50.3 and clear NU1903 (GHSA-2m69-gcr7-jv3q). No advisory suppression permitted. This is an unsupported combination with Microsoft.Data.Sqlite 8.0.11; the AC-4/AC-7 runtime gate is mandatory. Sole requirements source: issue.md AC-1..AC-7.
