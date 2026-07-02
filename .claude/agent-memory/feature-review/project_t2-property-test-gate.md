---
name: t2-property-test-gate
description: How the T1/T2 property-test-density gate has been graded in this repo's audits; CsCheck absent repo-wide as of 2026-07-02
metadata:
  type: project
---

The quality-tiers T1/T2 gate ">= 1 property test per pure function" has been graded PASS in prior
audits (#19, #80) only via "this change adds no new pure functions." CsCheck (the tool named by
`.claude/rules/csharp.md`) is not referenced anywhere in the repo as of 2026-07-02.

**Why:** issue #18 (2026-07-02) was the first branch to add new pure functions (`IsSensitive`,
`RedactMessage`, `RedactEvent` on T2 `OpenClaw.MailBridge`); I graded the gate PARTIAL/Major (not
Blocking) because the example-based tests exhaustively pinned the boundary partition and full
field dispositions, and routed it to remediation with two options: add CsCheck (policy-named, so
dependency-policy-sanctioned) or record a dated exception with owner acceptance.

**How to apply:** when a branch adds pure functions on T1/T2, do not reuse the "no new pure
functions" PASS wording — evaluate the gate. Check `quality-tiers.yml` for the module tier and
`grep -rn CsCheck Directory.Packages.props tests/` for harness presence. Grade severity by how
exhaustive the example-based coverage is. See [[per-file-coverage-masking]] for the audit that
established this.
