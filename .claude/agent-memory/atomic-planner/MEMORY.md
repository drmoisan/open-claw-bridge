# Atomic Planner Memory

- [CSharpier command form](csharpier-command-form.md) — C# format/check plan tasks use `csharpier format .` / `csharpier check .`, not the `dotnet csharpier` driver (no local tool manifest in repo)
- [Raw intermediates path](raw-intermediates-artifacts-csharp.md) — raw TRX/Cobertura/build logs go to `artifacts/csharp/`; only summarizing evidence md goes to `<FEATURE>/evidence/<kind>/`
- [Oversized test files](oversized-test-files-sibling-add.md) — cap/near-cap test files get sibling ADDs; PS instances: Install.Tests.ps1 505, Install.Helpers.psm1 527, Publish.Tests.ps1 480
- [PoshQC coverage workaround](poshqc-coverage-workaround.md) — MCP coverage run always fails (bundled runsettings defect); plans must cite corrected-runsettings Invoke-PoshQCTest for numeric coverage
