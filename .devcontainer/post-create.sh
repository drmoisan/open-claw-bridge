#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -n "${WORKSPACE_FOLDER:-}" ] && git -C "${WORKSPACE_FOLDER}" rev-parse --show-toplevel >/dev/null 2>&1; then
    WORKSPACE_DIR="$(git -C "${WORKSPACE_FOLDER}" rev-parse --show-toplevel)"
else
    WORKSPACE_DIR="$(git -C "${SCRIPT_DIR}/.." rev-parse --show-toplevel 2>/dev/null || pwd)"
fi

cd "${WORKSPACE_DIR}"
export WORKSPACE_DIR

echo "=================================="
echo "OpenClaw MailBridge container setup"
echo "=================================="
echo "Workspace: ${WORKSPACE_DIR}"
echo ""

BASHRC="${HOME}/.bashrc"
LINE="export WORKSPACE_DIR=\"${WORKSPACE_DIR}\""
grep -qxF "${LINE}" "${BASHRC}" 2>/dev/null || echo "${LINE}" >> "${BASHRC}"

mkdir -p "${HOME}/.config/powershell"
cat > "${HOME}/.config/powershell/Microsoft.PowerShell_profile.ps1" <<PROFILE
\$env:WORKSPACE_DIR = '${WORKSPACE_DIR}'
PROFILE

echo "Installed toolchain:"
echo "  dotnet: $(dotnet --version)"
echo "  pwsh:   $(pwsh --version)"
echo "  git:    $(git --version)"
echo "  gh:     $(gh --version | head -n 1)"
echo ""

echo "Restoring solution..."
dotnet restore OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
echo ""

echo "Building solution..."
dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableWindowsTargeting=true
echo ""

echo "Running cross-platform tests..."
dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj \
    -c Debug \
    -f net10.0 \
    --no-build \
    -p:EnableWindowsTargeting=true
echo ""

echo "Verification:"
bash .devcontainer/verify-container.sh
echo ""

echo "Notes:"
echo "  - The container restores and builds net10.0-windows projects by setting EnableWindowsTargeting=true."
echo "  - Running OpenClaw.MailBridge, Outlook COM integration, and classic Outlook validation still require Windows."
echo "  - For container-safe loops, use scripts/Build.ps1 and scripts/Test.ps1 or the matching VS Code tasks."
