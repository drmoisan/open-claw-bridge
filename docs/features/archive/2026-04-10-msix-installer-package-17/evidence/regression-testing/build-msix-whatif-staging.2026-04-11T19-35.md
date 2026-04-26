Timestamp: 2026-04-11T19:35:00-04:00
Command: Invoke-Pester -Path 'tests/scripts/build-msix.Tests.ps1' -FullNameFilter 'WhatIf leaves installer/staging/AppxManifest.xml absent' -PassThru
EXIT_CODE: 1
Output Summary:
- Executed targeted scenario via FullNameFilter `build-msix.ps1.WhatIf leaves installer/staging/AppxManifest.xml absent`
- FailureType=ParameterBindingException
- FailureMessage=A parameter cannot be found that matches parameter name 'WhatIf'.
