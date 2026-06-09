# AC-09 — CSharpier and Analyzer-Config Claims (Issue #66)

Timestamp: 2026-06-08T09-50
Command: `rg -n "dotnet csharpier check|dotnet tool run csharpier|Directory\.Build\.props" .claude/skills/csharp-qa-gate/SKILL.md .claude/skills/invoke-csharp-engineer/SKILL.md .github/instructions/csharp-code-change.instructions.md`; `rg -n "csharpier" <same files>`
EXIT_CODE: 0

Output Summary: AC-09 PASS.

- Forbidden patterns absent across the three files: no `dotnet csharpier check`, no
  `dotnet tool run csharpier`, and no literal `Directory.Build.props` (EXIT 1).
- Global csharpier invoked:
  - `.claude/skills/csharp-qa-gate/SKILL.md:32` — `csharpier check .`; L30 states there is no repo-wide
    MSBuild props file centralizing analyzer configuration and that CSharpier is the global tool.
  - `.github/instructions/csharp-code-change.instructions.md:25-32` — formatting step uses `csharpier .`.
  - `.claude/skills/invoke-csharp-engineer/SKILL.md` references CSharpier in toolchain prose without any
    non-global invocation form.

No file claims `Directory.Build.props`-centralized analyzer configuration; analyzer behavior is
described as SDK/analyzer defaults.
