---
paths:
  - "**/*.cs"
  - "**/*.csproj"
description: Architecture boundary enforcement rules for the COM-based MailBridge solution.
---

# Architecture Boundaries

Architecture boundary enforcement is a uniform gate across all tiers (T1–T4). Violations block PRs.

This solution is a Windows-first .NET 10 system that reads Outlook through COM and exposes that data to a local agent over a named pipe and loopback HTTP. The boundaries below keep Outlook COM interop isolated to the bridge host and keep the dependency graph acyclic and layered.

## Enforcement Tools

- **Compile-time project graph (primary):** the `ProjectReference` edges in the `*.csproj` files are the enforced boundary. A reference that violates the rules below fails the build or is rejected in review.
- **`NetArchTest.Rules` (optional, when added):** automated assertions may be added in a `*.ArchitectureTests` test project to assert namespace/assembly dependency rules. There is no architecture-test project today; until one exists, reviewers verify the rules against the project graph.

There is no TypeScript frontend, Office.js layer, or `dependency-cruiser` configuration in this repository.

## Project Dependency Rules (enforceable assertions)

The solution projects and their allowed dependencies:

1. `OpenClaw.MailBridge.Contracts` is the shared contract core. It must depend on **no other solution project**.
2. `OpenClaw.MailBridge` (the COM bridge host) may depend only on `OpenClaw.MailBridge.Contracts`.
3. `OpenClaw.MailBridge.Client` may depend only on `OpenClaw.MailBridge.Contracts`. It communicates with the bridge host at runtime over the **named pipe**, not through a project reference.
4. `OpenClaw.HostAdapter.Contracts` may depend only on `OpenClaw.MailBridge.Contracts`.
5. `OpenClaw.HostAdapter` may depend only on `OpenClaw.HostAdapter.Contracts` and `OpenClaw.MailBridge.Contracts`. It invokes the client **executable** as a process and must **not** reference `OpenClaw.MailBridge` (the COM host) or perform Outlook COM itself.
6. `OpenClaw.Core` may depend only on `OpenClaw.HostAdapter.Contracts`. It reaches mail and calendar data at runtime over **loopback HTTP** to the HostAdapter and must not reference the bridge host, the client, or the COM layer.
7. No circular project references are permitted.

## COM Confinement Rules (enforceable assertions)

1. Outlook COM interop (late-bound Outlook automation, `Microsoft.Office.Interop.Outlook`, `Marshal` COM helpers, active-object resolution) must exist **only in `OpenClaw.MailBridge`**.
2. All Outlook COM calls run on a single dedicated STA thread within `OpenClaw.MailBridge`.
3. The web projects (`OpenClaw.HostAdapter`, `OpenClaw.Core`) must not pull Outlook COM into their dependency closure. This is preserved by routing through the client process and HTTP rather than linking the bridge host.
4. COM objects must be released deterministically; runtime callable wrappers must not accumulate.

## Data-Flow Direction

Mailbox data flows in one direction and must not be short-circuited:

```
Outlook  --COM-->  OpenClaw.MailBridge  --named pipe-->  OpenClaw.MailBridge.Client
       --process invocation-->  OpenClaw.HostAdapter  --loopback HTTP-->  OpenClaw.Core / OpenClaw agent
```

Downstream layers (Client, HostAdapter, Core, agent) consume cached, contract-shaped data; they do not call Outlook directly.

## Enforcement Outcome

Violations of any rule above are PR-blocking findings. The dependency rules are enforced by the build (a disallowed `ProjectReference` is a defect) and by review of the project graph; COM-confinement and data-flow rules are enforced by review, and by `NetArchTest.Rules` assertions if and when a `*.ArchitectureTests` project is added.
