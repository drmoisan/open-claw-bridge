# Scope Confinement and Inert-Wiring Verification (P5-T1)

Timestamp: 2026-07-02T19-11

Commands and results (all EXIT_CODE: 0):

1. `git diff --name-only 970034f35b462ace78a5ab10f16409b90e810d29` (tracked modifications vs Phase 0 SHA):
   - `src/OpenClaw.Core/OpenClaw.Core.csproj` (the single PackageReference line, P1-T1)
   - `.claude/agent-memory/prd-feature/MEMORY.md` — pre-existing agent-harness memory change made by the planning agent before execution began; not production code, not touched by this executor, outside the production diff scope.
2. `git status --porcelain` (untracked additions):
   - `src/OpenClaw.Core/CloudAuth/` — exactly nine new production files
   - `tests/OpenClaw.Core.Tests/CloudAuth/` — exactly seven new test files (P3-T4 500-line split branch taken: `ClientCredentialsTokenProviderConcurrencyTests.cs` holds part 2; the plan explicitly accepts this shape)
   - `docs/features/active/2026-07-02-app-only-auth-module-113/` — feature docs/evidence
   - `.claude/agent-memory/prd-feature/project_core_namespace_partition_convention.md` — planning-agent memory artifact, non-production, not created by this executor.
3. `git hash-object` re-hash of the four untouched surfaces — all match `evidence/baseline/baseline-untouched-surfaces.2026-07-02T18-51.md` exactly:
   - `src/OpenClaw.Core/Program.cs` — dde60556f928226dc3a9cca617e89b97b98d4b84 (match)
   - `src/OpenClaw.Core/appsettings.json` — cab3ea107fafc3cb9dabe984920f8d1765716525 (match)
   - `tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs` — a2ccbf31314d3b922ac133fb87054e42f0c3947d (match)
   - `docker-compose.yml` — 9a4bee45b19e8aa7da097ac7f7e2148d41e1ce5c (match)
4. Azure containment (`grep -rln "Azure" src/` excluding obj/bin): matches only in four `src/OpenClaw.Core/CloudAuth/*.cs` files plus `src/OpenClaw.Core/OpenClaw.Core.csproj`. No Azure reference anywhere else under `src/`.
5. `AddCloudAuth` production callers (`grep -rn "AddCloudAuth" src/` excluding the extension file itself): NONE — registration is opt-in and nothing calls it (D8; inert wiring confirmed).
6. Line counts (`wc -l`) — every new file <= 500:
   - src: IAppTokenProvider.cs 18, AppAccessToken.cs 18, TokenFreshness.cs 20, TokenAcquisitionException.cs 43, CredentialFactory.cs 46, CloudAuthOptions.cs 57, CloudAuthOptionsValidator.cs 73, CloudAuthServiceCollectionExtensions.cs 48, ClientCredentialsTokenProvider.cs 152
   - tests: AppAccessTokenTests.cs 89, CloudAuthServiceCollectionExtensionsTests.cs 149, CloudAuthArchitectureBoundaryTests.cs 149, TokenFreshnessTests.cs 183, ClientCredentialsTokenProviderConcurrencyTests.cs 280, CloudAuthOptionsValidatorTests.cs 309, ClientCredentialsTokenProviderTests.cs 345
7. Secret-material scan of the seven test files: only clearly fake constants present — `"fake-token-value"`, `"other-fake-token-value"`, `"fake-client-secret-value"`, `"fake-scope-value"`, `"fake-authority-value"`, `/run/secrets/fake-cert.pem`, and all-zero GUIDs (`00000000-0000-0000-0000-000000000001/2`). Pattern scan for realistic secrets (JWT prefix `eyJ...`, PEM `BEGIN ... PRIVATE`) over src and tests CloudAuth folders: NONE.

EXIT_CODE: 0
Output Summary: Diff is confined to the csproj single-line change, nine src/CloudAuth files, seven tests/CloudAuth files (split branch), and feature docs/evidence; all four untouched surfaces hash-match baseline; Azure references contained to CloudAuth + csproj; zero AddCloudAuth production callers; all sixteen new files <= 500 lines; test data is fake-only. The two `.claude/agent-memory/prd-feature` items are pre-existing planning-agent harness artifacts outside production scope.
