---
name: policy-audit-template-usage
description: 'Policy audit template usage and output requirements. Use when creating policy-audit.<timestamp>.md artifacts from the repo templates.'
---

# Policy Audit Template Usage

Shared rules for creating policy audit artifacts from the MCP-exposed bundled template assets.

## When to Use This Skill

Use this skill when:
- An agent must create a `policy-audit.<timestamp>.md` file.
- The MCP-exposed policy-audit template asset is required.

## Template Source

- Required template source: the MCP server tool `resolve_policy_audit_template_asset` with asset selector `template`.
- The resolved MCP asset is authoritative even when a matching repo file also exists.
- If MCP asset resolution fails, create a minimal policy audit artifact marked BLOCKED and document the missing template resolution.

## Required Steps

1) Resolve the policy-audit template through the MCP server tool `resolve_policy_audit_template_asset` with asset `template`, then copy the resolved asset to the target location using an ISO-8601 timestamp.
2) Replace placeholders with actual values (component, date, files under test, commits).
3) Remove any template usage instructions per template guidance.
4) Mark each section PASS/FAIL/N/A using the template’s expected conventions.
5) Preserve the canonical major sections from the template:
   - `## Executive Summary`
   - `## 1. General Unit Test Policy Compliance`
   - `## 2. General Code Change Policy Compliance`
   - `## 3. Language-Specific Code Change Policy Compliance`
   - `## 4. Language-Specific Unit Test Policy Compliance`
   - `## 5. Test Coverage Detail`
   - `## 6. Test Execution Metrics`
   - `## 7. Code Quality Checks`
   - `## 8. Gaps and Exceptions`
   - `## 9. Summary of Changes`
   - `## 10. Compliance Verdict`
   - `## Appendix A: Test Inventory`
   - `## Appendix B: Toolchain Commands Reference`
6) Run the `validate_orchestration_artifacts` MCP tool with `artifact_type: "policy-audit"` and `artifact_path: <path>` and fail closed on any non-zero result.

## Invalid Outputs

- A freeform summary note is invalid.
- A policy audit that retains the template instruction block is invalid.
- A policy audit that omits the canonical major headings is invalid.
