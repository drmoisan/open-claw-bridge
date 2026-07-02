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

**Resolution precedent (issue #18 remediation cycle 1, accepted PASS at the 2026-07-02T10-23
re-audit):** the gate can be closed WITHOUT CsCheck via "option (b)" — deterministic
exhaustive/parameterized invariant tests (full-domain equivalence for predicates; exact-
transformed-set + complete mechanical-preservation + idempotence matrices for transforms) —
when (1) the remediation directive/orchestrator directs it, (2) a dated decision record exists
under `evidence/other/` citing the dependency-minimization policy, and (3) the tests enumerate
inputs explicitly (no randomness). Grade Section 4 property-test row PASS with the recorded
exception; keep repo-wide CsCheck adoption as an informational follow-up, not a finding.
