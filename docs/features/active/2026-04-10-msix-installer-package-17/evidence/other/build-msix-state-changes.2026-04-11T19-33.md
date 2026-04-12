Timestamp: 2026-04-11T19:33:00-04:00
Command: build-msix-state-change-inventory
EXIT_CODE: 0
Output Summary:
- StagingManifestWrite=scripts/build-msix.ps1:100-106 writes installer/staging/AppxManifest.xml via New-Item plus XmlDocument.Save()
- StagingLayoutCopy=scripts/build-msix.ps1:142-154 creates staging subdirectories and copies bridge, client, and asset files into installer/staging/
- PriGeneration=scripts/build-msix.ps1:167-181 invokes makepri.exe createconfig and new to emit priconfig.xml and resources.pri
- MsixPack=scripts/build-msix.ps1:200-210 creates the output directory and invokes makeappx.exe pack to write the output .msix
- Signing=scripts/build-msix.ps1:227-233 invokes signtool.exe sign against the generated .msix when -SkipSign is not specified
