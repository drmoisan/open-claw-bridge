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
   **How to apply:** mirror the structure of the most recent accepted artifact set — the #99 set
   `docs/features/active/2026-07-02-wire-sendmail-runtime-99/*.2026-07-02T11-25.md` (earlier
   accepted sets: #19 `calendar-overlap-filter-19/*.2026-07-02T08-24.md`, #80
   `core-response-status-roundtrip-80/*.2026-07-02T07-35.md`; check archive locations after
   merge) — combined with the
   exact rules in [[artifact-validator-quirks]]. Record the missing MCP resolution as a documented
   exception in section 8 of the policy audit (accepted on #80, #19, #18, #99, and #101 reviews,
   2026-07-02).

2. **`dotnet tool restore` fails** ("command csharpier ... package contains dotnet-csharpier").
   **How to apply:** use the globally installed `csharpier check .` (1.3.0) instead; record the
   accommodation in the audit. Same workaround was accepted in the #70 and #80 audits.

**Why:** both gaps otherwise stall the review; prior audits established the accepted fallbacks.

Also: no `validate_evidence_locations.py` exists in this repo — do the evidence-location scan with
`git diff --name-only <base>..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`.

Third recurring quirk — **PR-context summary misclassifies C# branches as docs-only.** On the
#99, #101, #103, and #105 reviews (2026-07-02) `artifacts/pr_context.summary.txt` "Changed files
overview" reported "Core logic changes: 0 files" while the authoritative git diff contained 3-7+
production and 3-6 test `.cs` files. Never scope from the summary's file categorization; always use
`git diff --stat <merge-base>..HEAD` and record the mismatch as an observation under Rejected
Scope Narrowing (accepted pattern on all four reviews). The summary's author-asserted autoclose
list can also contain non-issue tokens parsed from AC labels (`#AC-1`, `#ISO-8601` on #105) —
noise, note and ignore. Newest accepted artifact-set templates after #99: #103
`2026-07-02-ordinary-mail-candidates-103/*.2026-07-02T13-36.md` and #105
`2026-07-02-one-on-one-move-history-105/*.2026-07-02T14-35.md`.
