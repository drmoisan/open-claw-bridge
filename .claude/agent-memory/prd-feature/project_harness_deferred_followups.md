---
name: harness-migration-deferred-followups
description: Three harness scripts/manifests deliberately deferred out of Issue #66 scope — generator script, benchmark validator, dotnet-tools manifest
metadata:
  type: project
---

Three referenced-but-absent harness artifacts were deliberately deferred out of the Issue #66 harness-migration fix and recorded as follow-ups:

1. `scripts/dev-tools/sync-agents-from-instructions.ps1` — the `AGENTS.md` generator referenced by the AGENTS.md header. Has never existed. Issue #66 hand-edits AGENTS.md and source instructions together instead.
2. `scripts/benchmarks/Test-BaselineProvenance.ps1` — benchmark baseline validator referenced by `.claude/rules/benchmark-baselines.md`. Issue #66 qualifies the rule text as "not yet present" rather than authoring it.
3. `.config/dotnet-tools.json` — CSharpier local-tool manifest. Issue #66 deliberately does NOT create it; all skills/instructions invoke global `csharpier`.

**Why:** Each adds a new deliverable beyond the documentation correction; the operator chose the lowest-risk path (correct docs now, author tooling later).

**How to apply:** If asked to create any of these, treat it as new follow-up work, not part of #66. The `.claude/rules/*` and harness docs intentionally state these paths are not yet present. See [[harness-canonical-policy]].
