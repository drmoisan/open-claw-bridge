---
name: pr-base-branch-merge-base
description: 'Resolve the correct PR base branch using merge-base ancestry. Use when review or PR-context workflows need a deterministic base branch instead of a guessed default.'
---

# PR Base Branch (Merge-Base)

Deterministic branch-selection rules for review and PR-context workflows.

## Selection Contract

The base branch must be resolved from git ancestry, not guessed.

Definition:
- choose the candidate branch whose merge-base with `HEAD` has the most recent commit timestamp

## Deterministic Procedure

1. Enumerate candidate branches from local and remotes, excluding `HEAD` and detached refs.
2. For each candidate `B`, compute `M = git merge-base HEAD B`.
3. Compute `merge_base_epoch(B)` from `git show -s --format=%ct M`.
4. Select the branch with the highest `merge_base_epoch(B)`.
5. Tie-break in this order:
   - `development`
   - `main`
   - `master`
   - lexical ascending branch name

## Guardrails

- Do not default to `main` unless merge-base resolution fails for all candidates.
- Persist and reuse the selected base within the same review or orchestration run.
