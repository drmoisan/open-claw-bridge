Timestamp: 2026-04-05T20:56:00.0513770-04:00
Command: Get-Content .github\instructions\general-code-change.instructions.md; Get-Content .github\instructions\general-unit-test.instructions.md; Get-Content .github\instructions\csharp-code-change.instructions.md; Get-Content .github\instructions\csharp-unit-test.instructions.md
EXIT_CODE: 0
Output Summary: Read the general code-change, general unit-test, C# code-change, and C# unit-test policies and recorded the applicable C# audit constraints.

Applicable C# commands:
- Formatting: `csharpier .`
- Analyzer build: `msbuild TaskMaster.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
- Nullable/type build: `msbuild TaskMaster.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true`
- Test runner: `vstest.console.exe <test-assembly-paths> /EnableCodeCoverage`

Applicable constraints:
- Execute the full C# toolchain loop in order: formatting, analyzer build, nullable/type build, testing.
- If formatting changes files or any later step fails, restart the loop from formatting.
- Use `csharpier` rather than `dotnet format`.
- Treat compiler and nullable diagnostics as first-class failures.
- Record auditable evidence for baseline, targeted verification, and final QA steps.
