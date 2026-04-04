---
name: feature-promotion-lifecycle
description: 'Deterministic promotion workflow from potential item to issue, branch, and active feature folder. Use when orchestration must initialize delivery state in Codex without duplicating host-specific automation logic.'
---

# Feature Promotion Lifecycle

Canonical variable model and promotion sequence for initializing active feature delivery.

## Required Shared Skills

Always use:
- `repo-automation-adapter`
- `evidence-and-timestamp-conventions`

## Canonical Variables

- `${promotion-type}`: `feature` or `bug`
- `${short-name}`: lowercase hyphenated slug
- `${relativeFile}`: workspace-relative path to the potential entry
- `${long-name}`: `${relativeFile}` filename without `.md`
- `${issue-num}`: promoted GitHub issue number
- `${feature-folder}`: active feature folder path
- `${plan-path}`: canonical plan file path
- `${work-mode}`: `minor-audit`, `full-feature`, or `full-bug`

## Workflow

1. Create the potential entry.
2. Promote the potential entry to an issue.
3. Create the branch.
4. Create the active feature folder.
5. Resolve and persist the canonical `plan-path`.
6. Delegate planning to `atomic-planner`.
7. Require `atomic-executor` preflight before execution.

## Canonical Branch Name

- `${promotion-type}/${short-name}-${issue-num}`

If the branch already exists, reuse it instead of inventing an alternate branch name.

## Plan Path Resolution

The canonical active plan path must use the timestamped form `plan.<timestamp>.md`.

Resolve `${plan-path}` in this order:

1. If one or more `plan*.md` files already exist under `${feature-folder}`, reuse the earliest existing timestamped `plan.<timestamp>.md`.
2. If no timestamped plan exists but a legacy `plan.md` exists, treat that as a migration defect to be corrected; do not keep both `plan.md` and a new timestamped plan as competing active-plan candidates.
3. If no plan file exists, create exactly one new `plan.<timestamp>.md` using the timestamp format from `evidence-and-timestamp-conventions` and persist that path as `${plan-path}`.

## Codex Execution Rule

Do not encode host-specific implementation details here.

For each lifecycle step:
- use `repo-automation-adapter` to choose the direct repo automation path,
- use a deterministic local fallback only when explicitly supported,
- otherwise stop and report the missing automation dependency.

## Mode-Aware Expectations

- `minor-audit`: `issue.md` is authoritative and `spec.md` / `user-story.md` are intentionally absent
- `full-feature`: `issue.md`, `spec.md`, and `user-story.md` are expected
- `full-bug`: `issue.md` and `spec.md` are expected
