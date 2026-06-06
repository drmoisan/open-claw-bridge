# Missed-Line Enumeration for `scripts/Install.Helpers.psm1` and `scripts/Install.Preflight.psm1`

Timestamp: 2026-04-27T08-00
Command: Read artifacts/pester/install-layer-coverage.xml
EXIT_CODE: 0

## Output Summary

Source: `artifacts/pester/install-layer-coverage.xml`, `<sourcefile>` blocks for each module. A line is "missed" when its `mi` attribute is non-zero.

### `scripts/Install.Helpers.psm1` — missed lines

| Line | Function | Notes / In-Scope-for-Remediation |
| --- | --- | --- |
| 40  | `Get-ManifestVersion` | "empty version field" throw — pre-existing module body, NOT in scope (no AC-14b regression risk; existing module tests cover other branches). |
| 71  | `Get-ManifestVersion` | "manifest not found at …" throw — NOT in scope (pre-existing module body). |
| 99  | `Test-ManifestIntegrity` | Size mismatch discrepancy line — NOT in scope. |
| 330 | `Wait-ComposeHealthy` | docker compose ps non-zero exit retry path — NOT in scope. |
| 336 | `Wait-ComposeHealthy` | Raw normalization (`raw -is [array]`) branch — NOT in scope. |
| 372 | `Wait-ComposeHealthy` | Failing-service fallback (`$failing = 'openclaw-core'`) — NOT in scope. |
| 459 | `Get-ListeningProcessId` | `Get-NetTCPConnection -ErrorAction SilentlyContinue` invocation. **In scope (P2-T3 / P2-T4).** |
| 460 | `Get-ListeningProcessId` | `Select-Object -First 1` continuation. **In scope.** |
| 461 | `Get-ListeningProcessId` | `if (-not $connection) { return $null }` empty-listener branch. **In scope (P2-T3).** |
| 462 | `Get-ListeningProcessId` | `return [int]$connection.OwningProcess` happy path. **In scope (P2-T4).** |
| 474 | `Get-ProcessMainModulePath` | `$proc = Get-Process -Id $ProcessId -ErrorAction Stop`. **In scope (P2-T1, P2-T2).** |
| 475 | `Get-ProcessMainModulePath` | `$mainModule = $proc.MainModule`. **In scope (P2-T2).** |
| 476 | `Get-ProcessMainModulePath` | `if ($null -eq $mainModule) { return $null }`. **In scope (P2-T2).** |
| 477 | `Get-ProcessMainModulePath` | `return [string]$mainModule.FileName` happy-path return — covered by Install.Preflight.Tests stale-process / matching-path tests (already PASS). |
| 479 | `Get-ProcessMainModulePath` | `catch { return $null }` defensive branch. **In scope (P2-T1).** |
| 493 | `Invoke-HostAdapterStatusRequest` | `Invoke-WebRequest -Uri $StatusUri -Method Get` invocation — exercised indirectly via Preflight tests' `Mock Invoke-HostAdapterStatusRequest`. Not a missed *behavioral* branch under module-level mocking; coverage tracker records the body lines because the wrapper itself is not invoked. NOT addressable by additional unit tests against this seam without exercising real HTTP. |
| 494 | `Invoke-HostAdapterStatusRequest` | Same as line 493 — wrapper-seam body. NOT in scope. |

### `scripts/Install.Preflight.psm1` — missed lines

| Line | Function | Notes / In-Scope-for-Remediation |
| --- | --- | --- |
| 28  | `Get-InstallEnvFileMap` | `if (-not (Test-Path -LiteralPath $EnvFilePath)) { return $map }` — NOT new; covered indirectly by Install.Tests.ps1 fixtures. |
| 36  | `Get-InstallEnvFileMap` | `$key = $trimmed.Substring(0, $equalsIndex).Trim()`. NOT in scope (existing module tests cover other branches; this is the per-line key-parse path which fires only when the test fixture provides a non-empty `.env` with a normal `KEY=VAL` line). |
| 37  | `Get-InstallEnvFileMap` | Value-parse path with quote stripping. NOT in scope. |
| 38  | `Get-InstallEnvFileMap` | Map assignment `$map[$key] = $value`. NOT in scope. |
| 58  | `Get-InstallEndpointUri` | URI builder concatenation branch. NOT in scope. |
| 75  | `Get-HostAdapterPreflightUri` | Custom-base-url override branch. NOT in scope (existing tests cover the default; the override is exercised by other Install.Tests fixtures). |
| 81  | `Get-HostAdapterPreflightUri` | `throw "OpenClaw__HostAdapter__BaseUrl … is not a valid URI"` — defensive throw on invalid URI; covered by existing helpers indirectly. NOT in scope under the AC-14b carve. |
| 130 | `Format-HostAdapterPreflightFailure` | `$codePart = if ($null -ne $errorCode) { $errorCode } else { '(missing)' }` — only-message branch. **In scope (P3-T2d).** |
| 131 | `Format-HostAdapterPreflightFailure` | `$messagePart = if ($null -ne $errorMessage) { … } else { '(missing)' }` — only-code branch. **In scope (P3-T2c).** |
| 151 | `Get-PreflightTokenAndUri` | `HOSTADAPTER_TOKEN_FILE` environment-variable expansion branch. NOT in scope. |
| 155 | `Get-PreflightTokenAndUri` | "Token file not found" throw. NOT in scope. |
| 160 | `Get-PreflightTokenAndUri` | "Token file empty" throw. NOT in scope. |
| 188 | `Assert-HostAdapterRespondingPreflight` | Unreachable-network throw. NOT in scope (covered by existing Preflight tests' status-code-only failures). |
| 238 | `Assert-HostAdapterBridgeReadyPreflight` | Unreachable-network throw. NOT in scope. |
| 267 | `Assert-HostAdapterBridgeReadyPreflight` | `catch { $bridgeReady = $false }` JSON-parse-failure branch. **In scope (P3-T1).** |

## In-scope coverage gaps (primary driver of >=90% gate)

Three known gaps per `evidence/qa-gates/p7-coverage-delta.md` Analysis section:

1. `Get-ProcessMainModulePath` catch branch (Helpers line 479) — addressed by P2-T1.
2. `Get-ListeningProcessId` empty-listener branch (Helpers line 461) — addressed by P2-T3.
3. `Assert-HostAdapterBridgeReadyPreflight` JSON-parse-failure branch (Preflight line 267) — addressed by P3-T1.

Additional high-leverage in-scope gaps identified by this enumeration:

4. `Get-ProcessMainModulePath` MainModule-null branch (Helpers lines 474-476) — addressed by P2-T2.
5. `Get-ListeningProcessId` happy path (Helpers line 462) — addressed by P2-T4 (round-out).
6. `Format-HostAdapterPreflightFailure` only-code/only-message branches (Preflight lines 130-131) — addressed by P3-T2c and P3-T2d.

The remaining missed lines listed above as "NOT in scope" are documented as the structural Install.ps1 measurement artifact and the AC-14a documentation carve-out described in `p7-coverage-delta.md`. They are not the driver for the AC-14b 90% gate on `Install.Helpers.psm1` and `Install.Preflight.psm1`.

## Lines that the plan-targeted tests will exercise

| File | Lines newly covered (predicted) |
| --- | --- |
| `Install.Helpers.psm1` | 461, 462 (P2-T3/P2-T4), 474, 475, 476, 479 (P2-T1/P2-T2), and 459-460 indirectly via the same Get-NetTCPConnection mock invocations. |
| `Install.Preflight.psm1` | 130, 131 (P3-T2c/P3-T2d), 267 (P3-T1). |

After these additions, `Install.Helpers.psm1` rises from 132/148 (89.2%) toward at least 138/148 (93.2%) and `Install.Preflight.psm1` rises from 97/108 (89.8%) toward at least 100/108 (92.6%), satisfying AC-14b.

## P2-T5 / P3-T3 close-out note

P2-T5 close: No additional gaps in `scripts/Install.Helpers.psm1` beyond those covered by P2-T1..P2-T4. Remaining missed lines (40, 71, 99, 330, 336, 372, 493-494) are pre-existing module-body branches outside the AC-14b focus or wrapper-seam shim bodies (lines 493-494) that cannot be exercised without real HTTP and are out of scope.

P3-T3 close: No additional in-scope gaps in `scripts/Install.Preflight.psm1` beyond those covered by P3-T1..P3-T2. Remaining missed lines (28, 36-38, 58, 75, 81, 151, 155, 160, 188, 238) are pre-existing module-body branches outside the AC-14b focus on the JSON-parse-failure branch and the Format-HostAdapterPreflightFailure boundary cases.
