---
name: policy-audit-template-usage
description: 'Policy audit template usage and output requirements. Use when creating policy-audit.<timestamp>.md artifacts from the repo templates.'
---

# Policy Audit Template Usage

Shared rules for creating policy audit artifacts from the repo templates.

## When to Use This Skill

Use this skill when:
- An agent must create a `policy-audit.<timestamp>.md` file.
- The repo template under `docs/features/templates/policy_audit/` is required.

## Template Source

- Preferred template: `docs/features/templates/policy_audit/policy-audit.yyyy-MM-ddTHH-mm.md`
- If missing, search the repo for `policy-audit.yyyy-MM-ddTHH-mm.md`.
- If still missing, create a minimal policy audit artifact marked BLOCKED and document the missing template.

## Required Steps

1) Copy the template to the target location using an ISO-8601 timestamp.
2) Replace placeholders with actual values (component, date, files under test, commits).
3) Remove any template usage instructions per template guidance.
4) Mark each section PASS/FAIL/N/A using the template’s expected conventions.
