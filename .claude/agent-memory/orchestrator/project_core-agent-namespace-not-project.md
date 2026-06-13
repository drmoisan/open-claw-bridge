---
name: core-agent-namespace-not-project
description: New agent/deterministic logic for OpenClaw.Core must live in a namespace inside OpenClaw.Core, not a new project — architecture-boundaries rule 6.
metadata:
  type: project
---

New deterministic/agent logic that `OpenClaw.Core` must consume belongs in a **namespace inside the existing `OpenClaw.Core` project** (e.g. `OpenClaw.Core.Agent`), not a new `OpenClaw.Core.Agent` *project*.

**Why:** `.claude/rules/architecture-boundaries.md` rule 6 restricts `OpenClaw.Core` to depend **only on `OpenClaw.HostAdapter.Contracts`**. A new project would force a `OpenClaw.Core -> OpenClaw.Core.Agent` ProjectReference, which is a PR-blocking architecture violation. Policy under `.claude/rules/**` must not be edited to work around it. On issue #70 the research/spec adopted a new-project default (OR-1) and the plan failed preflight on exactly this; the fix was fold-in + a namespace-scoped `NetArchTest.Rules` assertion (in the existing `OpenClaw.Core.Tests`) to enforce the contract-parity invariant that the pure logic references no `OpenClaw.MailBridge`/`OpenClaw.HostAdapter`/COM types.

**How to apply:** When scoping work that puts new logic behind a seam consumed by `OpenClaw.Core`, instruct the planner up front to fold into `OpenClaw.Core` under a dedicated namespace and enforce isolation with a namespace-scoped architecture test — do not create a sibling project. This avoids a wasted plan→preflight→revise cycle. The 500-line limit is per-file, so many small files in one project is fine. See [[harness-governance]].
