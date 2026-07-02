---
name: t2-property-test-gate
description: How the T1/T2 property-test-density gate is graded; CsCheck 4.7.0 now present in OpenClaw.Core.Tests (six *PropertyTests classes) but still absent from other test projects
metadata:
  type: project
---

The quality-tiers T1/T2 gate ">= 1 property test per pure function" grading history:

- #19, #80: PASS via "this change adds no new pure functions."
- #18 (T2 OpenClaw.MailBridge): first branch adding new pure functions with no harness in that
  test project; graded PARTIAL/Major, closed via option (b) below.
- #99 (T1 OpenClaw.Core, 2026-07-02): PASS directly — CsCheck 4.7.0 is referenced by
  `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj` and the suite has six `*PropertyTests`
  classes (five under `Agent/`, one under `Agent/Runtime/`) using a seeded `Gen`/`Sample`
  convention (`iter: 1000`, failing seed printed). New pure functions in OpenClaw.Core can and
  should get a real CsCheck property test; "no harness" reasoning no longer applies there.

**Why:** an earlier version of this memory said CsCheck was absent repo-wide; that became stale
when the OpenClaw.Core property suites landed. Harness presence is per-test-project, not
repo-global — `OpenClaw.MailBridge.Tests` may still lack it.

**How to apply:** when a branch adds pure functions on T1/T2, evaluate the gate per test project:
`grep -n CsCheck tests/<project>/<project>.csproj` and `ls tests/<project>/**/*PropertyTests.cs`.
If the harness exists in that project, expect a genuine property test (PARTIAL/Major if missing).
If not, the #18 resolution precedent applies: the gate can be closed WITHOUT CsCheck via
deterministic exhaustive/parameterized invariant tests when (1) the remediation directive directs
it, (2) a dated decision record exists under `evidence/other/` citing dependency minimization, and
(3) the tests enumerate inputs explicitly (no randomness). See [[per-file-coverage-masking]].
