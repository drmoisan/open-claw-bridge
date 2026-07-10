# Expect-Fail — Manifest [long] size regression (Issue #142, P3-T2)

Timestamp: 2026-07-10T19-10
Command: pwsh Invoke-Pester (Run.Path = tests/scripts/Publish.Helpers.Tests.ps1; Filter.FullName = '*returns size as long*') against the pre-fix scripts/Publish.Helpers.psm1 (still `size = [int]$fileInfo.Length`)
EXIT_CODE: non-zero (Pester reported Failed: 1; the [expect-fail] target failed as intended)

Output Summary:
- Tests Passed: 0, Failed: 1, NotRun: 20.
- Failing assertion is upstream of the assertion itself — the function throws during execution:
  `OverflowException: Value was either too large or too small for an Int32.`
  `PSInvalidCastException: Cannot convert value "3000000000" to type "System.Int32".`
  at `New-ManifestEntry`, `scripts/Publish.Helpers.psm1:153` (the `[int]` cast site; line 155 in the source, offset by the module load).
- This confirms the latent enabling defect: a >2 GiB image tar length (3,000,000,000 > 2,147,483,647) overflows the `[int]` cast. The fix (P3-T5: `[int]` -> `[long]`) is required for the test to pass.
