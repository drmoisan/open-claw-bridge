---
name: pr-base-branch-merge-base
description: 'Resolve PRBaseBranch for mcp__drm-copilot__collect_pr_context using merge-base ancestry. Use when orchestrators or review workflows need the correct comparison base branch and must select the branch with the most recent common ancestor commit with HEAD.'
---

# PR Base Branch (Merge-Base)

Deterministic branch-selection rules for PR context collection.

## When to Use This Skill

Use this skill when:
- running `mcp__drm-copilot__collect_pr_context`,
- delegating post-implementation review that depends on `PRBaseBranch`,
- constructing PR context artifacts where the base must not be hard-coded.

## Selection Contract

`PRBaseBranch` MUST be resolved from git ancestry, not guessed.

Definition of correct base:
- choose the candidate branch whose merge-base with `HEAD` has the most recent commit timestamp,
- this implements “find the branch with the most recent commit in common.”

## Deterministic Procedure

1. Enumerate candidate branches from local and remotes, excluding `HEAD` and detached refs.
2. For each candidate `B`, compute merge-base commit `M = git merge-base HEAD B`.
3. Compute `merge_base_epoch(B)` from `git show -s --format=%ct M`.
4. Select branch with maximum `merge_base_epoch(B)`.
5. Tie-breakers (in order):
   - `development`
   - `main`
   - `master`
   - lexical ascending branch name

## Guardrails

- Do not default to `main` unless merge-base resolution fails for all candidates.
- If all candidates fail, surface explicit error context and use repository default branch only as last-resort fallback.
- Persist chosen `PRBaseBranch` in orchestration state and reuse it within the same run.

## Collector Invocation Rule

When invoking PR context collection, pass the resolved base explicitly:

- `mcp__drm-copilot__collect_pr_context` with `base=<resolved-PRBaseBranch>`

## Evidence Recommendation

For auditability, include in logs/checkpoint:
- selected branch name,
- selected merge-base SHA,
- selected merge-base timestamp,
- top competing candidates with timestamps when available.

