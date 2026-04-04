---
name: policy-audit-template-usage
description: 'Policy audit artifact rules. Use when creating policy-audit.<timestamp>.md files from repository templates or minimal fallbacks.'
---

# Policy Audit Template Usage

Shared rules for creating policy audit artifacts.

## Template Source

- Preferred template: `docs/features/templates/policy_audit/policy-audit.yyyy-MM-ddTHH-mm.md`
- If missing, search the repository for `policy-audit.yyyy-MM-ddTHH-mm.md`.
- If still missing, create a minimal policy audit artifact marked blocked and record the missing template.

## Required Steps

1. Copy the template to the target location using an ISO-style timestamp.
2. Replace placeholders with real values.
3. Remove template instructions from the final artifact.
4. Mark each section `PASS`, `FAIL`, or `N/A` using the template convention.
