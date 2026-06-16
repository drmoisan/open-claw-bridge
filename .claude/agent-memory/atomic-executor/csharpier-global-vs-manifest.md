---
name: csharpier-global-vs-manifest
description: In open-claw-bridge use the GLOBAL csharpier CLI (csharpier format . / csharpier check .); the local dotnet-tools manifest entry is broken
metadata:
  type: project
---

In the `open-claw-bridge` repo, format C# with the GLOBAL CSharpier CLI: `csharpier format .` (per-task
gate) and `csharpier check .` (baseline/final verification).

**Why:** The local `dotnet-tools` manifest entry for CSharpier is broken, so `dotnet csharpier ...`
fails. The global tool (confirmed version 1.3.0 during issue #75 execution) works and is the
repo-sanctioned path; CSharpier 1.x uses subcommand form (`format`/`check`), not the bare `csharpier .`.

**How to apply:** When running the C# toolchain loop (format -> lint -> nullable -> architecture ->
test), always invoke the global `csharpier` binary. Do not switch to `dotnet csharpier` or `dotnet
format`. Formatter output wins over hand-formatting; CSharpier reflows long method-arg lists and may
collapse/insert blank lines, which can affect 500-line-cap math on files already near the limit.
