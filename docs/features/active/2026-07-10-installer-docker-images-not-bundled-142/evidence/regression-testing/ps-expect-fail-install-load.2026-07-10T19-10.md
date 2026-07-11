# Expect-Fail — Install Stage 9 image-load wiring (Issue #142, P4-T6)

Timestamp: 2026-07-10T19-10
Command: pwsh Invoke-Pester (Run.Path = tests/scripts/Install.DockerStage.Tests.ps1; Filter.FullName = '*image load stage*') against the pre-wiring scripts/Install.ps1 (no Invoke-DockerImageLoad call yet)
EXIT_CODE: non-zero (Pester reported Failed: 3)

Output Summary:
- Tests Passed: 0, Failed: 3, NotRun: 4 (the 4 -SkipDocker Its were not in the filter).
- Failing Its (Context 'image load stage'):
  1. "loads the tar at <DestDockerDir>\openclaw-images.tar" — `$global:LastImageTarPath` is `$null` because `Invoke-DockerImageLoad` is never invoked.
  2. "loads the image after copying bundle contents and before compose up" — `IndexOf('Invoke-DockerImageLoad')` returns -1.
  3. "still loads the image before compose up on a -Force reinstall" — same missing-call cause.
- This confirms Stage 9 does not yet load the bundled image tar. The wiring in P4-T7/P4-T8 (import Install.Docker.psm1 and call Invoke-DockerImageLoad before Invoke-ComposeUp inside the -SkipDocker gate) is required for these to pass.
