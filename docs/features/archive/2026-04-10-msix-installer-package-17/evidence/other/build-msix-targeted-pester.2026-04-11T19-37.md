Timestamp: 2026-04-11T19:37:00-04:00
Command: Invoke-Pester -Path 'tests/scripts/build-msix.Tests.ps1' -PassThru
EXIT_CODE: 0
Output Summary:
- PassedCount=7
- FailedCount=0
- WhatIf leaves installer/staging/AppxManifest.xml absent=Passed
- WhatIf does not invoke MakePri, makeappx, or signtool=Passed
