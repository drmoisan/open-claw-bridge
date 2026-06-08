# Baseline Coverage-Threshold Scan (Issue #66)

Timestamp: 2026-06-08T09-20
Command: `rg -n "85%|75%|80%|90%" .claude/rules AGENTS.md`
EXIT_CODE: 0

Output Summary: Pre-change threshold divergence confirmed.

- `AGENTS.md` L348 states line coverage `>= 80%`; L349 states new-code `>= 90%`. These are the divergent (non-canonical) gates to be replaced with 85%/75% in P5-T6.
- `.claude/rules/*` already state the canonical 85%/75% (quality-tiers.md L33,34,51; csharp.md L74; general-unit-test.md L23,24; powershell.md L63,64; python.md L88,89 — python.md to be deleted P1-T3).
- `.claude/rules/typescript.md` L50,61 cite 85%/75% but the file is deleted in P1-T1.

Divergence to reconcile: only `AGENTS.md` carries the prior 80%/90% gate. The `.github/instructions/general-unit-test.instructions.md` (not in this rules-scoped scan path) also carries 80%/90% per the spec (L39-40), addressed in P5-T4.
