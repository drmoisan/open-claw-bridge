---
name: pr-context-artifacts
description: 'PR context artifact locations and refresh rules. Use when generating, reading, or inlining pr_context summary/appendix artifacts.'
---

# PR Context Artifacts

Canonical locations and usage rules for PR context artifacts.

## When to Use This Skill

Use this skill when:
- You generate or refresh PR context artifacts.
- You reference PR context summary/appendix as evidence.
- You inline PR context artifacts into remediation handoffs.

## Canonical Artifact Locations

- Summary: `artifacts/pr_context.summary.txt`
- Appendix: `artifacts/pr_context.appendix.txt`

## Refresh Rule

If the artifacts are missing or stale relative to the current branch state, re-generate them using the repo’s PR context collector.

- When `PRBaseBranch` is supplied explicitly, use that exact branch for refresh.
- When `PRBaseBranch` is missing or ambiguous, resolve it first with `pr-base-branch-merge-base` before running the collector.
- Do not infer the refresh base from the repository default branch unless merge-base resolution fails for all candidates.
- Treat an already-fresh artifact pair as authoritative; do not refresh solely because no explicit `PRBaseBranch` input was provided.
