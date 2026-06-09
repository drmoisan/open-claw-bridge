---
name: research-issue
description: Investigate the best implementation approach for a feature or bug by analyzing the codebase and external references, then writing structured findings to artifacts/research/.
allowed-tools:
  - Read
  - Grep
  - Glob
  - WebFetch
---

# Research Issue Skill

Research the best implementation approach for a feature or bug described in the provided issue and spec documents. Output is a structured research file, not implementation code.

## Inputs

Accept one or more feature documents as context:

- `docs/features/active/<feature>/issue.md` (primary)
- `docs/features/active/<feature>/user-story.md` (when present)
- `docs/features/active/<feature>/spec.md` (when present)

## Output

Create or update a single research file:

- Path: `artifacts/research/<timestamp>-<short-name>-research.md`
- Use the Task Researcher template from the repository exactly.
- Place rejected-alternatives summaries inside `## Recommended Approach`, not as a separate top-level header.

## Investigation Areas

### 1. Current State Analysis

- Identify implementation targets from the feature docs.
- Read relevant modules end-to-end.
- Document current behavior, key abstractions, extension points, and toolchain constraints.

### 2. Candidate Implementation Approaches

- Research and compare at least two viable approaches.
- Select one final recommendation with justification.
- For any new dependency, confirm whether it already exists in the repo and justify adding it.
- Gather authoritative external documentation to support the recommendation.

### 3. Behavior Semantics and Edge Cases

- Extract intended behavior from feature docs.
- Define success/failure conditions, ordering rules, cancellation behavior, and CI vs. local expectations.
- Research comparable tools that implement similar semantics.

### 4. Requirements Mapping to Design

- Map acceptance criteria into a concrete design.
- Propose state model, transitions, internal API boundaries, and required file changes.

### 5. Testing Implications

- Propose a test strategy consistent with repository policy (unit tests for pure logic, integration seams).
- Do not write test code; describe the strategy only.

## Constraints

- Research only. Do not implement changes.
- Ground all findings in verified evidence from the codebase and authoritative external sources.
- Keep discussion of non-selected approaches brief.
- Do not claim or perform nested worker delegation.
