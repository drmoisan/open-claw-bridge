# AC-06 — AGENTS.md and Instructions C# Commands (Issue #66)

Timestamp: 2026-06-08T09-50
Command: `rg -n "msbuild TaskMaster|vstest\.console" AGENTS.md .github/instructions`; `rg -n "dotnet build|dotnet test|OpenClaw.MailBridge.sln" AGENTS.md`
EXIT_CODE: 0

Output Summary: AC-06 PASS.

- Stale commands absent: `rg "msbuild TaskMaster|vstest\.console" AGENTS.md .github/instructions`
  returns no matches (EXIT 1).
- Corrected commands present in AGENTS.md:
  - L442, L450 — `dotnet build OpenClaw.MailBridge.sln` (analyzer and nullable build steps).
  - L623 — `dotnet build OpenClaw.MailBridge.sln`.
  - L624 — `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.
- Source instructions edited in lockstep (Option C): `.github/instructions/csharp-code-change.instructions.md`
  and `.github/instructions/csharp-unit-test.instructions.md` use the same `dotnet build`/`dotnet test`
  forms against `OpenClaw.MailBridge.sln` (verified in P5-T2, P5-T3).

AGENTS.md and its source instructions agree on the corrected C# command set.
