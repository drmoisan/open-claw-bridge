Timestamp: 2026-04-09T22-18
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-mailbridge.ps1
EXIT_CODE: 1
Output Summary:
- PublishedBridgeTargetFramework: net10.0-windows
- BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0
- PublishedClientTargetFramework: net10.0-windows
- ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0
- The operation has timed out.
- [31;1mException: [0mC:\Users\DanMoisan\repos\open-claw-bridge\scripts\test-mailbridge.ps1:32:9[0m
- [31;1m[0m[36;1mLine |[0m
- [31;1m[0m[36;1m[36;1m  32 | [0m         [36;1mthrow "No JSON output returned for arguments: $($CommandArgum[0m .[0m
- [31;1m[0m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m         ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
- [31;1m[0m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mNo JSON output returned for arguments: status[0m
