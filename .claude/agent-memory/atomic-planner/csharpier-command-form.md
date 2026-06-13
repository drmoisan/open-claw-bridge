---
name: csharpier-command-form
description: C# format/check tasks in plans must use global CSharpier 1.3.0 subcommand form, not the dotnet csharpier driver
metadata:
  type: project
---

C# formatting tasks in atomic plans for this repo must use the global CSharpier 1.3.0 subcommand form: `csharpier format .` and `csharpier check .`. Do not use `dotnet csharpier .` or `dotnet csharpier --check .`.

**Why:** The `dotnet csharpier` local-tool driver is not runnable in this repo — there is no working local tool manifest. CSharpier 1.x replaced top-level flags with subcommands (`format`, `check`). Plans that used the `dotnet csharpier` driver form returned PREFLIGHT: REVISIONS REQUIRED from the executor.

**How to apply:** When generating or revising any plan for this repo, write C# format gate steps as `csharpier format .` (per-task gate) and baseline/final-QC format checks as `csharpier check .`. Record `Command: csharpier check .` in the acceptance evidence field for format-check tasks.
