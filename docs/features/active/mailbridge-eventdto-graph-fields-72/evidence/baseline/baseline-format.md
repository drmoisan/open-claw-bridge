# Baseline — CSharpier Format Check

Timestamp: 2026-06-13T03-03

Command: `csharpier check .`

Note on command form: The plan lists `dotnet csharpier --check .`. The repo has no tracked `.config/dotnet-tools.json`; a stale parent-directory manifest pins csharpier 0.16.0 with a command-name mismatch that makes `dotnet csharpier` fail to restore. CSharpier 1.3.0 is installed as a global tool (`csharpier`), and the 1.x CLI uses `csharpier check .` (the `--check` flag was renamed to the `check` subcommand in 1.x). Same tool, same purpose; this is a tooling-invocation adaptation, not a plan change.

EXIT_CODE: 0

Output Summary: PASS. Checked 148 files in ~348ms. 0 files needed formatting.
