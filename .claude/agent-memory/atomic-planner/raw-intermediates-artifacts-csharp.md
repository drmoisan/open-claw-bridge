---
name: raw-intermediates-artifacts-csharp
description: Raw C# command intermediates (TRX, Cobertura XML, build logs) go under artifacts/csharp/; evidence markdown under <FEATURE>/evidence/<kind>/ summarizes them
metadata:
  type: project
---

Raw command intermediates (TRX files, Cobertura XML, build logs) are retained under `artifacts/csharp/`, while the evidence markdown artifacts that summarize them live in canonical `<FEATURE>/evidence/<kind>/` locations.

**Why:** The evidence-and-timestamp-conventions skill forbids most `artifacts/` sub-paths for *evidence output*, but the preflight-approved plan `docs/features/active/2026-07-03-graph-subscriptions-delta-117/plan.2026-07-03T01-03.md` (Global Conventions) established that `artifacts/csharp/` for raw intermediates is acceptable — the prohibition targets evidence artifacts, not raw tool output. This distinction passed executor preflight and hook enforcement.

**How to apply:** In C# plans, direct raw TRX/Cobertura/build output to `artifacts/csharp/` and require the summarizing markdown (with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`) under `<FEATURE>/evidence/<kind>/`. Related: [[csharpier-command-form]].
