# CI Research

This document records the design rationale for the repository's CI quality gates. Section 1 defines
the module rigor tier system that `.claude/rules/quality-tiers.md` and `quality-tiers.yml` reference
as the source of truth.

## 1. Module Rigor Tiers (T1–T4)

Every project in `OpenClaw.MailBridge.sln` is classified into one of four rigor tiers. The tier sets
the strength of the CI gates applied to that project (mutation score, property-test density, golden
tests, E2E scope). Line and branch coverage thresholds are uniform across all tiers (line >= 85%,
branch >= 75%); the tier controls the other, tier-dependent gates. The authoritative project-to-tier
map is `quality-tiers.yml` at the repository root.

### Tier definitions

- **T1 — Critical.** Behavior bugs cause silent data loss, host-state corruption, or security holes.
  In this repository the critical surface is the host-agnostic core and the host-adapter contract
  implementation that mediate between callers and the Outlook COM bridge. Representative OpenClaw
  examples: `OpenClaw.Core` (host-agnostic command/transfer model and core logic) and
  `OpenClaw.HostAdapter` (the host-adapter implementation that brokers host operations).

- **T2 — Core.** Bugs cause feature regressions but not silent data loss. This covers the contract
  surfaces and the bridge entry point whose defects are observable as feature failures. Representative
  OpenClaw examples: `OpenClaw.MailBridge.Contracts`, `OpenClaw.HostAdapter.Contracts`, and the
  managed (non-COM) surface of `OpenClaw.MailBridge`.

- **T3 — Adapters & UI.** Glue around APIs the team does not own, including the Outlook COM interop
  confined to `OpenClaw.MailBridge` and the client transport. Outlook COM is allowed here and confined
  to `OpenClaw.MailBridge`; it is not banned. Representative OpenClaw examples: `OpenClaw.MailBridge.Client`
  and the Outlook-COM-confined surface within `OpenClaw.MailBridge`.

- **T4 — Scaffolding.** Dependency-injection wiring, bootstrap, build scripts, and developer tooling.
  Defects here are caught at build or smoke time. Representative OpenClaw examples: DI/bootstrap glue,
  build scripts, and PowerShell developer tooling under `scripts/` and `tests/scripts`.

### Test-project classification

Each test project is classified with its production peer so that the test project inherits the rigor
of the code it exercises:

- `OpenClaw.Core.Tests` follows `OpenClaw.Core` (T1).
- `OpenClaw.HostAdapter.Tests` follows `OpenClaw.HostAdapter` (T1).
- `OpenClaw.MailBridge.Tests` follows `OpenClaw.MailBridge` (T2 for the managed surface; the
  Outlook-COM-confined behavior it exercises is T3).

### Source of truth

- The tier system is defined here (section 1).
- `quality-tiers.yml` at the repository root maps each of the nine solution projects to a tier.
- The CI `tier-classification` stage fails if any solution project is missing a tier entry or if an
  entry names a project absent from `OpenClaw.MailBridge.sln`.
