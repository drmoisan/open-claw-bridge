---
name: evidence-and-timestamp-conventions
description: 'Evidence storage and timestamp naming conventions. Use when storing baseline, regression, remediation, or QA evidence artifacts with deterministic timestamps.'
---

# Evidence and Timestamp Conventions

Reusable conventions for evidence storage and timestamped artifacts.

## Timestamp Format

Use `yyyy-MM-ddTHH-mm` for audit, remediation, and evidence artifacts.

## Canonical Evidence Locations

- Baseline evidence: `evidence/baseline/`
- Regression-testing evidence: `evidence/regression-testing/`
- Other evidence: `evidence/other/`
- QA-gate evidence: `evidence/qa-gates/`
- Issue-update mirrors: `evidence/issue-updates/`
- Remediation baselines: `evidence/remediation-baseline/`

## Evidence Artifact Schema

Machine-checkable evidence artifacts should include:
- `Timestamp: <ISO-8601>`
- `Command: <exact command>`
- `EXIT_CODE: <int>`
- `Output Summary: <short outcome summary>`

## Negative Evidence Claims

If you claim evidence is missing, also record:
- `SearchScope:`
- `SearchPatterns:`
- `SearchResult:`

## Issue Update Mirrors

When a workflow updates or proposes text for a GitHub issue, create a local mirror artifact at:
- `<FEATURE>/evidence/issue-updates/issue-<N>.<timestamp>.md`
