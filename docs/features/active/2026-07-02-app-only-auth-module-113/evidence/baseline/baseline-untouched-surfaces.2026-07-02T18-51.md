# Baseline — Untouched Surfaces (Pre-Change State)

Timestamp: 2026-07-02T18-51
Command: git rev-parse HEAD; git hash-object <4 files>; grep (ripgrep) over src/ for Azure.Identity | Microsoft.Identity | CloudAuth
EXIT_CODE: 0

Commit SHA (Phase 0): 970034f35b462ace78a5ab10f16409b90e810d29

File hashes (git hash-object):
- src/OpenClaw.Core/Program.cs — dde60556f928226dc3a9cca617e89b97b98d4b84
- src/OpenClaw.Core/appsettings.json — cab3ea107fafc3cb9dabe984920f8d1765716525
- tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs — a2ccbf31314d3b922ac133fb87054e42f0c3947d
- docker-compose.yml — 9a4bee45b19e8aa7da097ac7f7e2148d41e1ce5c

Pre-existing reference check:
- Command: ripgrep pattern `Azure\.Identity|Microsoft\.Identity|CloudAuth` over `src/`
- Result: No files found (empty result confirmed). `src/` contains no Azure.Identity, Microsoft.Identity, or CloudAuth reference at baseline.

Output Summary: Phase 0 SHA and four untouched-surface hashes recorded; grep over src/ for Azure.Identity/Microsoft.Identity/CloudAuth returned zero matches, confirming a clean pre-change state for AC-5 verification in Phase 5.
