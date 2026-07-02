---
name: core-namespace-partition-convention
description: New Core-adjacent logic folds into an OpenClaw.Core namespace (not a new project), enforced by namespace-scoped NetArchTest suites; confirmed for #74 (Agent) and #113 (CloudAuth)
metadata:
  type: project
---

New Core-adjacent logic in this repo goes into a new namespace inside `OpenClaw.Core` (e.g. `OpenClaw.Core.Agent`, `OpenClaw.Core.CloudAuth`), not a new project. Isolation is enforced by namespace-scoped NetArchTest suites in `tests/OpenClaw.Core.Tests/<Area>/` (pattern: `Types.InAssembly(...).ResideInNamespaceStartingWith(...)` bans plus a reflection walk over member signatures for finer-than-prefix assertions — see `AgentArchitectureBoundaryTests.cs`).

Related conventions confirmed 2026-07-02 while authoring the #113 spec:
- `.claude/rules/architecture-boundaries.md` has no project-isolation requirement for Core internals; its .NET layer rules name future `TaskMaster.*` projects only.
- Options POCOs are plain sealed classes with `get; set;` and primitive defaults bound via `AddOptions<T>().Bind(section)`; the repo had no `ValidateDataAnnotations`/`ValidateOnStart` usage before #113 introduced `ValidateOnStart` for `CloudAuthOptions` (justified by the fail-closed auth mandate).
- `OpenClaw.Core.csproj` already has `InternalsVisibleTo("OpenClaw.Core.Tests")` — internal test constructors are an available seam without config changes.
- No `Directory.Build.props`, `Directory.Packages.props`, or `BannedSymbols.txt` exists despite `csharp.md` describing them (see [[test-framework-mstest-not-xunit]] for the same rule-vs-reality pattern).

**Why:** Prior delegation prompts cite this as "prior architectural guidance"; the #113 spec recorded it as decision D1 with evidence. Re-deriving it each time risks a spec proposing a new project that reviewers would reject.

**How to apply:** When speccing new Core-adjacent modules, default to a namespace + a new namespace-scoped boundary test suite; verify the existing suites' banned lists before claiming a package addition is safe.
