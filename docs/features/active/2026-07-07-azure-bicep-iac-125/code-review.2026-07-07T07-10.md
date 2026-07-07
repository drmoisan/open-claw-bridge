# Code Review: azure-bicep-iac (#125) — Remediation Re-Audit (R4, cycle 1 exit)

**Review Date:** 2026-07-07 (re-audit)
**Prior Review:** `code-review.2026-07-07T05-30.md` (no Blocking/Major findings; one Minor finding: Service Bus preview API version)
**Branch:** `feature/azure-bicep-iac-125` @ `56fdbbecf308fffdacee6bb878e7ec794e08cd35` (re-confirmed via `git rev-parse HEAD`, and identical to `origin/feature/azure-bicep-iac-125` after `git fetch`)
**Base:** `epic/openclaw-vision-integration` @ merge-base `7a29286b687f00c6a10809efa41102c78f009c36` (re-confirmed via `git merge-base HEAD origin/epic/openclaw-vision-integration`)
**Scope:** Full branch diff, re-computed against the current head — 46 files changed, +2365/-1 (`git diff --stat 7a29286..HEAD`), up from 42 files / +1697/-1 at the prior review. The delta is 4 new Markdown audit artifacts from remediation cycle 1 (`code-review.2026-07-07T05-30.md`, `feature-audit.2026-07-07T05-30.md`, `policy-audit.2026-07-07T05-30.md`, `remediation-inputs.2026-07-07T05-30.md`); no code file changed since the prior review.

## Executive Summary

No code file (Bicep, PowerShell, or workflow YAML) changed between the prior code review and this re-audit. Every code-quality finding from `code-review.2026-07-07T05-30.md` is re-confirmed unchanged by re-reading the same files (`main.bicep`, `containerApp.bicep`, `keyVault.bicep`, `queue.bicep`, `main.dev.bicepparam`, `Test-OpenClawBicepParameterSecrets.ps1`, its test, `_bicep-validate.yml`, `ci.yml`) at the current head. This re-audit's incremental work was verifying the remediation evidence: a `workflow_dispatch` CI run (28846902040) that, for the first time, exercised `bicep build deploy/azure/main.bicep` and the parameter-file secret scan against real filesystem state on a GitHub-hosted runner, rather than the local structural-review fallback or Pester-mocked filesystem state used at the prior review.

This re-audit independently re-verified the run via `gh run view 28846902040 --json status,conclusion,headSha,jobs`:

- `conclusion`: `success`, `headSha`: `56fdbbecf308fffdacee6bb878e7ec794e08cd35` (matches the current branch head exactly)
- All four jobs (".NET Build + Test", "PowerShell QC", "Bicep Validate / Bicep Build + Parameter Secret Scan", "Workflow Lint") report `success`

This closes the two residual verification risks the prior remediation-inputs artifact explicitly flagged as unconfirmed until a real dispatch occurred: (1) whether `bicep build` would succeed against the real templates on a runner with the `bicep` CLI installed (it did — the "Bicep build" step succeeded), and (2) whether `Test-OpenClawBicepParameterSecrets.ps1` would behave correctly against the real `deploy/azure/parameters/` directory rather than only Pester-mocked filesystem state (it did — the "Parameter-file secret scan" step succeeded, finding no secret-shaped literals in the real, committed `main.dev.bicepparam`).

No Blocking or Major code-quality findings are recorded in this re-audit, consistent with the prior review. The prior review's one Minor finding is carried forward unchanged (no code change occurred that would affect it).

## Findings Table (carried forward, unchanged — no code file changed in this cycle)

| Severity | File | Location | Finding | Recommendation | Status |
|----------|------|----------|---------|----------------|--------|
| Minor | deploy/azure/modules/queue.bicep | lines 21, 31 (`Microsoft.ServiceBus/namespaces@2022-10-01-preview`) | Both Service Bus resources pin a `-preview`-suffixed API version. | Track as a follow-up to bump to a stable API version once Microsoft ships one, and re-run `bicep build` to confirm no property-shape changes. | Unchanged; carried forward from `code-review.2026-07-07T05-30.md`. Now additionally confirmed: the real CI `bicep build` run (28846902040) succeeded against this preview API version, meaning the pin is currently functional on the runner's installed `bicep` CLI version — this does not remove the future-retirement risk the finding describes. |
| Info | deploy/azure/modules/containerApp.bicep | lines 41-47 | No `registries` block for private-registry auth; correctly out of scope per `spec.md`'s Non-Goals. | None required. | Unchanged. |
| Info | scripts/Test-OpenClawBicepParameterSecrets.ps1 | lines 41-46 | Base64-looking-token pattern is intentionally broad (conservative secret scanner). | None required — correct default. | Unchanged. Additionally confirmed this cycle: the real CI "Parameter-file secret scan" step ran this exact pattern set against the real, committed `main.dev.bicepparam` and did not false-positive on its placeholder `containerImage` value, corroborating the prior review's conclusion that the placeholder contains no secret-shaped substring. |
| Info | deploy/azure/main.bicep | lines 25-53 | Module-naming scheme derived from `resourceNamePrefix`/`environmentName`. | None required. | Unchanged. |

## New Evidence Reviewed This Cycle

- `docs/features/active/2026-07-07-azure-bicep-iac-125/evidence/qa-gates/ci-green-run.2026-07-07T06-43.md` — read in full. Reports Run ID 28846902040, `workflow_dispatch`, head SHA `56fdbbecf308fffdacee6bb878e7ec794e08cd35`, `success` conclusion, all four jobs `success`.
- Independent re-verification: `gh run view 28846902040 --json status,conclusion,headSha,jobs,event,url` — every field in the evidence artifact matches the `gh` output exactly (run ID, head SHA, event type, per-job names, per-job conclusions). No discrepancy found between the claimed evidence and the independently-queried source of truth.
- Independent re-verification: `git rev-parse HEAD` and `git rev-parse origin/feature/azure-bicep-iac-125` (after `git fetch origin feature/azure-bicep-iac-125`) both equal `56fdbbecf308fffdacee6bb878e7ec794e08cd35`, and this equals the run's recorded `headSha`. No SHA drift between local, origin, and the dispatched run.

## Research Log (this re-audit)

- Re-read all `.bicep`/`.bicepparam`/`.ps1`/`.ps1`-test/`_bicep-validate.yml`/`ci.yml` files at the current head; byte-for-byte unchanged from the prior review's reviewed content (confirmed via `git diff b3a252b..HEAD --stat`, which shows only the 4 new Markdown files, zero code-file changes).
- Independently queried `gh run view 28846902040` rather than accepting the caller-supplied run details or the executor's evidence artifact at face value.
- Cross-checked the run's `headSha` field against both `git rev-parse HEAD` and the fetched `origin/feature/azure-bicep-iac-125` ref to rule out a stale-evidence scenario (the same class of staleness independently detected in the PR-context artifacts during this session — see the policy audit's "PR Context Artifact Freshness" section).

## Verdict

**Approve.** No Blocking or Major code-quality findings, unchanged from the prior review. The prior review's conditional approval ("Approve, conditional on the policy-audit's Blocking finding closing") is now unconditional: the policy audit's Blocking finding (`modified-workflow-needs-green-run`) is independently confirmed closed in this cycle (see `policy-audit.2026-07-07T07-10.md`, Section 8). One Minor finding (Service Bus preview API version) remains open as a non-blocking follow-up, unchanged.

**Total Blocking/Major findings in this re-audit: 0.**
