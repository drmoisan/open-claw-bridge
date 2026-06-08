---
paths:
  - "**"
description: Module rigor tier system and uniform coverage thresholds.
---

# Module Rigor Tiers

This rule defines the T1–T4 module rigor tier system used by all CI gates in this repository. The tier system source of truth is `docs/ci.research.md` section 1; the file `quality-tiers.yml` at the repository root maps every project to a tier. Adding a project without a tier classification fails CI.

## Tiers

- **T1 — Critical.** Behavior bugs cause silent data loss, host-state corruption, or security holes. Examples: `OpenClaw.Core` (host-agnostic command/transfer model and core logic), `OpenClaw.HostAdapter` (host-adapter implementation brokering host operations).
- **T2 — Core.** Bugs cause feature regressions but not data loss. Examples: `OpenClaw.MailBridge.Contracts`, `OpenClaw.HostAdapter.Contracts`, and the managed (non-COM) surface of `OpenClaw.MailBridge`.
- **T3 — Adapters & UI.** Glue around APIs the team does not own. Examples: `OpenClaw.MailBridge.Client` (client transport) and the Outlook-COM-confined surface within `OpenClaw.MailBridge`. Outlook COM is allowed and confined here, not banned.
- **T4 — Scaffolding.** Examples: DI wiring, bootstrap, build scripts, PowerShell dev tooling under `scripts/`, generated code, manifests.

## Source of Truth

- `quality-tiers.yml` at repo root maps every project to one tier.
- The CI pipeline's `tier-classification` stage validates that every project entry has a tier and that no unclassified project exists. Adding a project without a tier classification fails CI.

## Uniform-vs-Tier-Dependent Gate Matrix

Per Authoritative Decision #2, line and branch coverage thresholds are uniform across all tiers. Other gates remain tier-dependent.

### Uniform across all tiers (T1–T4)

- Format check: 100% pass.
- Lint errors: 0.
- Type errors: 0.
- Architecture violations: 0.
- Line coverage: >= 85%.
- Branch coverage: >= 75%.
- No regression on changed lines.

### Tier-dependent

| Gate | T1 | T2 | T3 | T4 |
|---|---|---|---|---|
| Untyped escape hatches (`any`/`dynamic`) | 0 | 0 | <= 5 per file, justified | unlimited |
| Property test density | >= 1 per pure function | >= 1 per pure function | none | none |
| Mutation score | >= 75% | trend-only | none | none |
| Contract breaking changes | major bump required | major bump required | n/a | n/a |
| Determinism (retry rate) | < 0.5% | < 1% | < 2% | n/a |
| Golden tests | required for classifier-output modules | optional | none | none |
| Full E2E suite scope | all critical paths | core paths | adapter smoke | none |

## Rationale (uniform coverage thresholds)

High test coverage is a fundamental quality-control design choice that enables autonomous agentic development and trust in the work product. For that reason, line coverage >= 85% and branch coverage >= 75% apply uniformly across T1–T4; tier-specific lower coverage floors are not used in this repository.
