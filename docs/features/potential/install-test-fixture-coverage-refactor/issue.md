# Install.ps1 test-fixture coverage refactor

- Date captured: 2026-04-27
- Author: drmoisan
- Status: Potential
- Work Mode: TBD (placeholder until promotion)

## Summary

`scripts/Install.ps1` per-file coverage reads as 5.9% in `artifacts/pester/install-layer-coverage.xml` because the test fixture in `tests/scripts/Install.Tests.ps1` pre-registers global functions for `Test-IsElevatedAdmin`, `Test-TcpPortOpen`, `Invoke-HostAdapterProcess`, and `Invoke-HostAdapterStart`. The dot-source-time `if (-not (Get-Command ...))` shim guards in `Install.ps1` short-circuit when those globals already exist, so Pester's coverage tracker records the un-entered shim bodies as missed even though the functional behavior is exercised by other tests against the pre-registered globals.

The full analytical detail is recorded in `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/qa-gates/p7-coverage-delta.md` Analysis section, points 1 and 2. This entry is the deferred follow-up to that analysis.

## Problem statement

`scripts/Install.ps1`'s inline shim guards short-circuit Pester coverage tracking against pre-registered test globals. Behavior is verified by other tests that run different shim definitions, but the coverage tracker does not credit those tests because they execute distinct function bodies. The result is an artificially low per-file coverage figure that does not reflect missing behavioral coverage.

## Proposed scope

Refactor `tests/scripts/Install.Tests.ps1` so that the orchestrator's stage transitions execute under coverage instrumentation. Likely shape:

- Replace the pre-registration of globals (`function global:Test-IsElevatedAdmin { ... }`, etc.) with module-level mocks via `Mock -ModuleName Install Test-IsElevatedAdmin { ... }` after dot-sourcing `Install.ps1` so the production shim bodies execute and are credited by the coverage tracker.
- Or, equivalently, restructure `Install.ps1` to expose its inline shims via a small helper module that can be imported and mocked at module scope, so that the dot-source path in tests no longer needs the `if (-not (Get-Command ...))` guard.

Target outcome: lift `scripts/Install.ps1` per-file figure toward the 95.98% pre-bug-fix repo-wide baseline recorded in `artifacts/pester/coverage-final.refinement.xml` (2026-04-19). The behavioral test suite remains green; only the measurement is restored.

## Acceptance criteria

(Placeholder — to be defined when this entry is promoted to active. At minimum: `scripts/Install.ps1` per-file coverage >= 90% under the standard MCP test runner, and the existing `tests/scripts/Install.Tests.ps1` and `tests/scripts/Install.Force.Tests.ps1` continue to pass without behavioral regressions.)

## Out of scope for: 2026-04-26-install-stale-hostadapter-detection (Option A remediation)

This refactor is explicitly excluded from the 2026-04-26 install-stale-hostadapter-detection feature scope. The Option A minor-audit remediation only added unit tests to `Install.Helpers.psm1` and `Install.Preflight.psm1` and amended the AC-14 acceptance criterion. See `docs/features/active/2026-04-26-install-stale-hostadapter-detection/remediation-plan.2026-04-27T08-00.md` for the boundary.
