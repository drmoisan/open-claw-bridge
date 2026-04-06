---
name: evidence-and-timestamp-conventions
description: 'Evidence storage and timestamp naming conventions for audits and remediation. Use when storing baseline/regression/QA evidence or naming audit artifacts with ISO-8601 timestamps.'
---

# Evidence and Timestamp Conventions

Reusable conventions for evidence storage locations and ISO-8601 timestamped artifacts.

## When to Use This Skill

Use this skill when:
- You create audit or remediation artifacts that must be timestamped.
- You store baseline, regression, or QA evidence under canonical folders.
- Multiple agents need the same naming and evidence-location rules.

## ISO-8601 Timestamp Format

Use `yyyy-MM-ddTHH-mm` for all audit, remediation, and evidence artifacts.
Example: `2026-02-06T14-30`.

## Canonical Evidence Locations

- Baseline evidence: `evidence/baseline/`
- Regression testing evidence: `evidence/regression-testing/`
- Other evidence: `evidence/other/`
- QA gate evidence: `evidence/qa-gates/`
- Issue update mirrors: `evidence/issue-updates/`

Epic rollups may mirror these under the epic root when needed.

## Feature Scope (Versioned Features)

In this repo, a feature may be single-version (docs at feature root) or multi-version
(`v1/`, `v2/`, etc.). When a feature is versioned, treat evidence discovery as applying
to the **selected current version scope**, but also search the feature root canonical
evidence folders as a fallback.

Practical rule:
- Prefer evidence under `<FEATURE>/<CURRENT_VERSION>/evidence/...` when it exists.
- Also search `<FEATURE>/evidence/...` before concluding evidence is missing.

## Canonical Evidence Discovery Order

When locating evidence artifacts for audits or plan reconciliation, use this order:

1) `<FEATURE>/evidence/issue-updates/` (issue update mirrors)
2) `<FEATURE>/evidence/regression-testing/`
3) `<FEATURE>/evidence/other/`
4) `<FEATURE>/evidence/qa-gates/`
5) `<FEATURE>/evidence/baseline/`
6) `<FEATURE>/evidence/remediation-baseline/`
7) `<EPIC>/evidence/issue-updates/` (issue update mirrors)
8) `<EPIC>/evidence/regression-testing/` (optional rollup)
9) `<EPIC>/evidence/other/`
10) `<EPIC>/evidence/qa-gates/` (optional rollup)
11) `<EPIC>/evidence/baseline/` (optional rollup)
12) `<EPIC>/evidence/remediation-baseline/` (optional rollup)

Rule:
- Use the list order by default for audit fidelity.
- If the task is explicitly remediation reconciliation, you may prefer `remediation-baseline` over `baseline`, but still record the original baseline as the authoritative audit reference.

If evidence or issue-update mirrors are found elsewhere, record it as non-canonical and include a remediation step to move/copy it into the first applicable canonical location.

### Fail-before Requirements (Deterministic Check)

When an acceptance criterion (AC) or plan item requires **fail-before / pass-after** evidence:

- First, search for a failing run artifact in `<FEATURE>/evidence/regression-testing/`.
- If no failing run exists (or it is structurally impossible), search for a **fail-before exception dossier**.

Minimum required search pattern:
- `fail-before-exception.*.md` (preferred name prefix)

Only after checking both (failing run OR exception dossier) may you write a negative claim like
"no fail-before evidence exists".

## Evidence Artifact Schema (Machine-Checkable)

When evidence artifacts are used for automated checking or plan reconciliation, include:
- `Timestamp: <ISO-8601>`
- `Command: <exact command>`
- `EXIT_CODE: <int>`

### Baseline Evidence Output Summary (Required)

For baseline evidence artifacts stored under `evidence/baseline/`, include an output summary in addition to the schema fields above:
- `Output Summary: <1–20 lines capturing the essential outcome>`

The summary should be concise, human-readable, and include the most important result signal (e.g., “All checks passed”, “767 passed”, coverage total, or a brief error description).

If a fail-before run is required but impossible, include a short exception dossier with:
- `WhyFailingRunImpossible: <1–3 sentences>`
- An alternative proof section (e.g., absence-of-test proof)

Fail-before exception dossiers should be stored under `evidence/regression-testing/`.

Preferred filename for fail-before exception dossiers:
- `fail-before-exception.<timestamp>.md`

If an exception dossier is present and schema-valid, it counts as satisfying the
"fail-before" requirement for audit/plan reconciliation purposes.

## Negative Evidence Claims (Absence Must Be Auditable)

If you claim evidence is missing (e.g., "no exception dossier recorded"), you MUST also record:

- `SearchScope:` the exact folder(s) searched (include both current-version scope and feature root when applicable)
- `SearchPatterns:` the filename patterns used (e.g., `fail-before-exception.*.md`)
- `SearchResult:` what was found (paths) or `none`

This prevents false negatives caused by searching the wrong scope or using incomplete patterns.

## Evidence-First Audit Writing

When marking FAIL or PARTIAL in audit artifacts, include:
- Concrete file + location (line/hunk/section when possible)
- The violated rule or expected behavior
- The verification command and its output (or why it could not be run)

## Issue Update Mirroring (Canonical Location)

When work involves updating a GitHub issue, create a local mirror artifact at:
- `<FEATURE>/evidence/issue-updates/issue-<N>.<timestamp>.md`

Required contents:
- `Timestamp: <ISO-8601>`
- The exact text intended/posted
- `PostedAs: body` or `PostedAs: comment` (preferred), or `PostedAs: unknown`
- If posted as a comment: the GitHub URL to the comment
- If posted as an issue body update: the GitHub URL to the issue and `IssueUpdatedAt: <ISO-8601>`
- If not posted: a `POSTING BLOCKED` header and the reason

If `PostedAs: body`, mirror the same update into the local feature `issue.md` (current version folder if present; otherwise feature root).
