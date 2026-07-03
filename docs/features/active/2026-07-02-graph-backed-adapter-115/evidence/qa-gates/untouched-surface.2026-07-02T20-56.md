# QA Gate — Untouched-Surface Verification (P8-T7, AC-5 evidence)

Timestamp: 2026-07-02T20-56
Command: git status --porcelain=v1 --untracked-files=all (scope re-check and docker-compose grep); git diff --stat -- "docker-compose*"; git diff -U0 src/OpenClaw.Core/Program.cs (hunk inspection); P8-T5 full-suite run record
EXIT_CODE: 0
Output Summary: PASS — all three assertions hold.

## (a) Program.cs default-path registration text unchanged

`git diff -U0 src/OpenClaw.Core/Program.cs` shows exactly two hunks:
1. One added using directive (`using OpenClaw.Core.CloudGraph;`).
2. The backend-selection conditional block. Inside the `else` branch, the default-path registration text — `builder.Services.AddHttpClient<IHostAdapterClient, HostAdapterHttpClient>((serviceProvider, client) => { var options = serviceProvider.GetRequiredService<IOptions<OpenClawOptions>>().Value; client.BaseAddress = new Uri(EnsureTrailingSlash(options.HostAdapter.BaseUrl)); });` — is token-for-token identical to the original registration; the only textual difference is the one-level indentation introduced by the enclosing `else` block. No other Program.cs edits exist.

## (b) docker-compose zero diff

`docker-compose.yml` and `docker-compose.dev.yml` both exist at repo root; `git status --porcelain --untracked-files=all` contains no docker-compose entry (grep exit 1 = no matches) and `git diff --stat -- "docker-compose*"` is empty. Zero diff.

## (c) Default-path selection test passed in the P8-T5 run

The P8-T5 final QA run (`evidence/qa-gates/csharp-test-coverage.2026-07-02T20-53.md`) executed the full OpenClaw.Core.Tests suite (616/616 passed, 0 failed), which includes `GraphBackendSelectionTests.DefaultPath_GraphAdapterAbsent_ResolvesHostAdapterHttpClient`; with zero failures in the run, the default-path test passed. The same test's individual pass is also recorded in `evidence/regression-testing/graph-backend-selection-fail-before.2026-07-02T20-45.md` (pass-after record, filtered run, EXIT_CODE 0).

## Diff-scope re-check at final head

Excluding the four allowed scopes (feature folder, `src/OpenClaw.Core/CloudGraph/`, `tests/OpenClaw.Core.Tests/CloudGraph/`, `src/OpenClaw.Core/Program.cs`), the change set is empty (filter grep exit 1 = no out-of-scope entries). The P7-T2 file list (`evidence/other/diff-scope-verification.2026-07-02T20-50.md`) remains accurate at final head, plus the Phase 8 evidence artifacts inside the feature folder.
