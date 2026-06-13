---
name: csharpier-local-tool-manifest-broken
description: Local dotnet-tools csharpier entry is misconfigured; use global csharpier for the C# format gate.
metadata:
  type: project
---

The repo's local dotnet tool manifest references the csharpier command as `csharpier`, but the package exposes `dotnet-csharpier`, so `dotnet tool restore` fails for csharpier. Additionally, csharpier 1.3.0's bare `csharpier .` form prints help; use the `csharpier format .` / `csharpier check .` subcommands.

**Why:** Surfaced during issue #71 execution. The atomic-executor fell back to the verified global csharpier 1.3.0 (the plan permits this alternative) and the format gate passed. It is a pre-existing repo tooling defect, not introduced by feature work.

**How to apply:** When a C# toolchain run hits a csharpier restore failure, do not treat it as a regression in the current change. Use global csharpier with explicit `format`/`check` subcommands and record it in format evidence. A separate fix to `.config/dotnet-tools.json` (correct the command name to `dotnet-csharpier`) would remove the friction. Relates to [[harness-governance]].
