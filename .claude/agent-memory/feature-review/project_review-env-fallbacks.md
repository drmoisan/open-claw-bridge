---
name: review-env-fallbacks
description: Fallbacks when MCP template/validator tools and dotnet tool restore are unavailable during feature review in this repo
metadata:
  type: project
---

Two environment gaps recur when running feature review in this repo's worktrees:

1. **MCP tools absent from the agent toolset.** `resolve_policy_audit_template_asset` and
   `validate_orchestration_artifacts` may not be exposed. No repo-local template files exist
   (glob for `*template*.md` finds only `.github/prompts/execute-plan-template.md`).
   **How to apply:** mirror the structure of the most recent accepted artifact set — the #19 set
   `docs/features/active/calendar-overlap-filter-19/policy-audit.2026-07-02T08-24.md` (or the #80
   set `docs/features/active/core-response-status-roundtrip-80/*.2026-07-02T07-35.md`; check
   archive locations after merge) — combined with the
   exact rules in [[artifact-validator-quirks]]. Record the missing MCP resolution as a documented
   exception in section 8 of the policy audit (accepted on #80, #19, and #18 reviews, 2026-07-02).

2. **`dotnet tool restore` fails** ("command csharpier ... package contains dotnet-csharpier").
   **How to apply:** use the globally installed `csharpier check .` (1.3.0) instead; record the
   accommodation in the audit. Same workaround was accepted in the #70 and #80 audits.

**Why:** both gaps otherwise stall the review; prior audits established the accepted fallbacks.

Also: no `validate_evidence_locations.py` exists in this repo — do the evidence-location scan with
`git diff --name-only <base>..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`.
