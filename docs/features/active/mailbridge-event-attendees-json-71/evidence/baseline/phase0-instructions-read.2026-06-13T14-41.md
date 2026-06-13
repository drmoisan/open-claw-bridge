# Phase 0 — Policy Read Evidence (Issue #71)

Timestamp: 2026-06-13T14-41

Policy Order:
1. CLAUDE.md (standing instructions, auto-loaded)
2. .claude/rules/general-code-change.md (cross-language code change policy; seven-stage toolchain; 500-line file cap)
3. .claude/rules/general-unit-test.md (cross-language unit test policy; determinism; no temp files)
4. .claude/rules/csharp.md (C# toolchain: CSharpier -> SDK analyzers -> nullable -> architecture -> MSTest + coverage)
5. .claude/rules/architecture-boundaries.md (COM confinement to OpenClaw.MailBridge; deterministic COM release)
6. .claude/rules/quality-tiers.md (uniform coverage: line >= 85%, branch >= 75%)

Files Read (explicit list):
- C:\Users\DanMoisan\repos\open-claw-bridge-wt-2026-06-13-10-27\.claude\rules\general-code-change.md
- C:\Users\DanMoisan\repos\open-claw-bridge-wt-2026-06-13-10-27\.claude\rules\general-unit-test.md
- C:\Users\DanMoisan\repos\open-claw-bridge-wt-2026-06-13-10-27\.claude\rules\csharp.md
- C:\Users\DanMoisan\repos\open-claw-bridge-wt-2026-06-13-10-27\.claude\rules\architecture-boundaries.md
- C:\Users\DanMoisan\repos\open-claw-bridge-wt-2026-06-13-10-27\.claude\rules\quality-tiers.md
- CLAUDE.md content provided via auto-loaded system context (benchmark-baselines, ci-workflows, general-code-change, general-unit-test, quality-tiers, tonality)

Output Summary: All six policy sources read in required order prior to execution of [P0-T1]. Languages in scope: C# only. Toolchain order confirmed: CSharpier format -> dotnet build with analyzers -> nullable (TreatWarningsAsErrors) -> architecture/COM-confinement review -> dotnet test with coverage. Coverage gates: line >= 85%, branch >= 75%, no regression on changed lines. 500-line file cap applies to all production and test files. COM confined to OpenClaw.MailBridge with deterministic release.
