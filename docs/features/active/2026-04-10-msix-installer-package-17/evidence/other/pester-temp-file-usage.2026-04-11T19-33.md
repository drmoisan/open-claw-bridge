Timestamp: 2026-04-11T19:33:00-04:00
Command: Select-String -Path 'tests/scripts/build-msix.Tests.ps1','tests/scripts/New-MsixDevCert.Tests.ps1' -Pattern '\$TestDrive'
EXIT_CODE: 0
Output Summary:
- tests/scripts/build-msix.Tests.ps1:55:$script:TestRoot = Join-Path $TestDrive 'build-msix-tests'
- tests/scripts/build-msix.Tests.ps1:84:Remove-Item -Recurse -Force (Join-Path $TestDrive 'stamp-test') -ErrorAction SilentlyContinue
- tests/scripts/build-msix.Tests.ps1:85:Remove-Item -Recurse -Force (Join-Path $TestDrive 'layout-test') -ErrorAction SilentlyContinue
- tests/scripts/build-msix.Tests.ps1:86:Remove-Item -Recurse -Force (Join-Path $TestDrive 'pack-test') -ErrorAction SilentlyContinue
- tests/scripts/build-msix.Tests.ps1:98:$stagingForTest = Join-Path $TestDrive 'stamp-test\staging'
- tests/scripts/build-msix.Tests.ps1:113:$missingBridgeDir = Join-Path $TestDrive 'nonexistent\bridge'
- tests/scripts/build-msix.Tests.ps1:127:$stagingForLayout = Join-Path $TestDrive 'layout-test\staging'
- tests/scripts/build-msix.Tests.ps1:144:$stagingForPack = Join-Path $TestDrive 'pack-test\staging'
- tests/scripts/build-msix.Tests.ps1:147:$outputForPack = Join-Path $TestDrive 'pack-test\output'
- tests/scripts/New-MsixDevCert.Tests.ps1:100:$testOutputDir = Join-Path $TestDrive 'cert-export-test'
