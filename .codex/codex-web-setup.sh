#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Codex Web copies this script to /tmp before running it, so the script-relative
# REPO_ROOT resolves to / rather than the actual checkout.  Fall back to the
# working directory, which Codex sets to the repo root.
if [ ! -f "${REPO_ROOT}/TaskMaster.sln" ]; then
  REPO_ROOT="$(pwd)"
fi

log() {
  printf '[codex-web-setup] %s\n' "$*"
}

fail() {
  printf '[codex-web-setup] error: %s\n' "$*" >&2
  exit 1
}

warn() {
  printf '[codex-web-setup] warning: %s\n' "$*" >&2
}

read_global_json_sdk_version() {
  local global_json_path="${REPO_ROOT}/global.json"

  if [ ! -f "${global_json_path}" ]; then
    return 1
  fi

  grep -oE '"version"[[:space:]]*:[[:space:]]*"[^"]+"' "${global_json_path}" |
    head -n 1 |
    sed -E 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/'
}

append_if_missing() {
  local file="$1"
  local line="$2"

  touch "$file"
  if ! grep -Fqx "$line" "$file"; then
    printf '%s\n' "$line" >>"$file"
  fi
}

install_apt_packages() {
  if ! command -v apt-get >/dev/null 2>&1; then
    warn "apt-get is unavailable; skipping OS package installation."
    return
  fi

  local runner=""
  if [ "$(id -u)" -eq 0 ]; then
    runner=""
  elif command -v sudo >/dev/null 2>&1; then
    runner="sudo"
  else
    warn "apt-get requires root and sudo is unavailable; skipping OS package installation."
    return
  fi

  log "Installing Codex Web dependencies with apt-get..."
  ${runner} apt-get update
  ${runner} apt-get install -y \
    ca-certificates \
    curl \
    git \
    jq \
    lsb-release \
    mono-complete \
    ripgrep \
    unzip \
    zip
}

install_powershell() {
  if command -v pwsh >/dev/null 2>&1; then
    log "PowerShell is already available; skipping."
    return
  fi

  if ! command -v apt-get >/dev/null 2>&1; then
    warn "apt-get is unavailable; skipping PowerShell installation."
    return
  fi

  local runner=""
  if [ "$(id -u)" -ne 0 ]; then
    if command -v sudo >/dev/null 2>&1; then
      runner="sudo"
    else
      warn "PowerShell installation requires root; skipping."
      return
    fi
  fi

  local ubuntu_version
  ubuntu_version="$(lsb_release -rs 2>/dev/null || echo '24.04')"

  log "Registering Microsoft package repository for PowerShell (Ubuntu ${ubuntu_version})..."
  local ms_pkg
  ms_pkg="$(mktemp --suffix=.deb)"
  if curl -fsSL "https://packages.microsoft.com/config/ubuntu/${ubuntu_version}/packages-microsoft-prod.deb" -o "${ms_pkg}"; then
    ${runner} dpkg -i "${ms_pkg}" || true
    rm -f "${ms_pkg}"
    ${runner} apt-get update
    ${runner} apt-get install -y powershell
  else
    rm -f "${ms_pkg}"
    warn "Could not download Microsoft package repo; skipping PowerShell installation."
  fi
}

install_nuget() {
  if command -v nuget >/dev/null 2>&1; then
    log "nuget is already available; skipping."
    return
  fi

  if ! command -v mono >/dev/null 2>&1; then
    warn "mono is unavailable; cannot install nuget wrapper."
    return
  fi

  log "Downloading nuget.exe and creating mono wrapper..."
  local nuget_exe="/usr/local/bin/nuget.exe"
  local nuget_wrapper="/usr/local/bin/nuget"

  local runner=""
  if [ "$(id -u)" -ne 0 ]; then
    runner="sudo"
  fi

  if curl -fsSL "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -o "${nuget_exe}"; then
    printf '#!/usr/bin/env bash\nexec mono %s "$@"\n' "${nuget_exe}" | ${runner} tee "${nuget_wrapper}" >/dev/null
    ${runner} chmod +x "${nuget_wrapper}"
    log "nuget wrapper installed at ${nuget_wrapper}."
  else
    warn "Could not download nuget.exe; skipping."
  fi
}

install_dotnet_sdk() {
  local dotnet_root="${REPO_ROOT}/.dotnet-sdk"
  local dotnet_version=""
  local install_script=""

  dotnet_version="$(read_global_json_sdk_version || true)"
  if [ -z "${dotnet_version}" ]; then
    warn "Could not read the SDK version from global.json; skipping repo-local .NET SDK installation."
    return
  fi

  if [ -x "${dotnet_root}/dotnet" ] && [ -d "${dotnet_root}/sdk/${dotnet_version}" ]; then
    log "Repo-local .NET SDK ${dotnet_version} is already available at ${dotnet_root}."
  else
    log "Installing repo-local .NET SDK ${dotnet_version} into ${dotnet_root}..."
    install_script="$(mktemp)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${install_script}"
    bash "${install_script}" --version "${dotnet_version}" --install-dir "${dotnet_root}"
    rm -f "${install_script}"
  fi

  export DOTNET_ROOT="${dotnet_root}"
  export PATH="${dotnet_root}:${PATH}"

  append_if_missing "${HOME}/.bashrc" "export DOTNET_ROOT=\"${dotnet_root}\""
  append_if_missing "${HOME}/.bashrc" "export PATH=\"${dotnet_root}:\$PATH\""
}

install_dotnet_tools() {
  if ! command -v dotnet >/dev/null 2>&1; then
    fail "dotnet is unavailable; cannot restore the required dotnet tool manifest."
  fi

  if [ ! -f "${REPO_ROOT}/dotnet-tools.json" ]; then
    fail "Required dotnet tool manifest not found at ${REPO_ROOT}/dotnet-tools.json."
  fi

  log "Restoring repo-local dotnet tools from dotnet-tools.json..."
  (
    cd "${REPO_ROOT}"
    dotnet tool restore --tool-manifest "${REPO_ROOT}/dotnet-tools.json"
  )
}

install_dotnet_coverage() {
  if ! command -v dotnet >/dev/null 2>&1; then
    fail "dotnet is unavailable; cannot install dotnet-coverage."
  fi

  local dotnet_tools_dir="${HOME}/.dotnet/tools"
  mkdir -p "${dotnet_tools_dir}"

  export PATH="${dotnet_tools_dir}:${PATH}"
  append_if_missing "${HOME}/.bashrc" "export PATH=\"\$HOME/.dotnet/tools:\$PATH\""

  if command -v dotnet-coverage >/dev/null 2>&1; then
    log "dotnet-coverage is already available; skipping."
    return
  fi

  log "Installing dotnet-coverage as a global dotnet tool..."
  if dotnet tool list --global | grep -Eq '^dotnet-coverage\s'; then
    dotnet tool update --global dotnet-coverage
  else
    dotnet tool install --global dotnet-coverage
  fi
}

install_actionlint() {
  if command -v actionlint >/dev/null 2>&1; then
    log "actionlint is already available; skipping."
    return
  fi

  if ! command -v tar >/dev/null 2>&1; then
    warn "tar is unavailable; skipping actionlint installation."
    return
  fi

  local actionlint_version="1.7.7"
  local temp_dir=""
  local runner=""

  if [ "$(id -u)" -ne 0 ]; then
    if command -v sudo >/dev/null 2>&1; then
      runner="sudo"
    else
      warn "actionlint installation requires root or sudo; skipping."
      return
    fi
  fi

  temp_dir="$(mktemp -d)"
  log "Installing actionlint v${actionlint_version}..."
  if curl -fsSL "https://github.com/rhysd/actionlint/releases/download/v${actionlint_version}/actionlint_${actionlint_version}_linux_amd64.tar.gz" -o "${temp_dir}/actionlint.tar.gz"; then
    tar -xzf "${temp_dir}/actionlint.tar.gz" -C "${temp_dir}" actionlint
    ${runner} install -m 0755 "${temp_dir}/actionlint" /usr/local/bin/actionlint
    log "actionlint installed at /usr/local/bin/actionlint."
  else
    warn "Could not download actionlint; skipping."
  fi

  rm -rf "${temp_dir}"
}

restore_packages_if_needed() {
  cd "${REPO_ROOT}"

  if [ -d "${REPO_ROOT}/packages" ] && [ -n "$(find "${REPO_ROOT}/packages" -mindepth 1 -maxdepth 1 -print -quit)" ]; then
    log "packages/ is already populated; skipping restore."
    return
  fi

  if ! command -v nuget >/dev/null 2>&1; then
    warn "nuget is unavailable; cannot restore packages.config dependencies."
    return
  fi

  log "Restoring solution packages into packages/..."
  nuget restore "${REPO_ROOT}/TaskMaster.sln" -PackagesDirectory "${REPO_ROOT}/packages"
}

verify_formatting_capability() {
  log "Verifying formatting capability..."

  command -v dotnet >/dev/null 2>&1 || fail "dotnet is not available after setup."
  command -v pwsh >/dev/null 2>&1 || fail "pwsh is not available after setup."

  (
    cd "${REPO_ROOT}"
    dotnet tool run csharpier --version >/dev/null
  ) || fail "CSharpier is not runnable via 'dotnet tool run csharpier'."
}

is_windows_powershell_host() {
  pwsh -NoProfile -ExecutionPolicy Bypass -Command "& { if (\$IsWindows) { exit 0 } ; exit 1 }" >/dev/null 2>&1
}

verify_windows_visual_studio_task_capability() {
  pwsh -NoProfile -ExecutionPolicy Bypass -File "${REPO_ROOT}/scripts/vscode/Invoke-VSBuild.ps1" -SolutionPath TaskMaster.sln -Configuration Debug -Platform 'Any CPU' -NoExecute >/dev/null || fail "MSBuild tooling required by the restore/build/lint/type-check tasks is unavailable."

  pwsh -NoProfile -ExecutionPolicy Bypass -Command "& {
    \$vswherePath = Join-Path \${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path \$vswherePath)) {
      throw 'vswhere.exe was not found. Install Visual Studio 2022 (or Build Tools) with Test Platform components.'
    }

    \$vstestPath = & \$vswherePath -latest -products * -find 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe' | Select-Object -First 1
    if (-not \$vstestPath) {
      throw 'vstest.console.exe not found via vswhere. Install Visual Studio Test Platform components.'
    }
  }" >/dev/null || fail "Visual Studio test tooling required by the MSTest tasks is unavailable."
}

verify_build_and_test_capability() {
  log "Verifying restore/build/lint/type-check/test capability..."

  command -v pwsh >/dev/null 2>&1 || fail "pwsh is not available after setup."
  command -v dotnet-coverage >/dev/null 2>&1 || fail "dotnet-coverage is not available after setup."
  [ -f "${REPO_ROOT}/coverage.config" ] || fail "coverage.config is missing from the repository root."

  if ! is_windows_powershell_host; then
    warn "Skipping Windows-only Visual Studio task verification because this host is not Windows. Restore/build/test parity still requires Windows plus Visual Studio tooling discoverable via vswhere.exe."
    return
  fi

  verify_windows_visual_studio_task_capability
}

verify_required_task_tooling() {
  verify_formatting_capability
  verify_build_and_test_capability
}

write_repo_notes() {
  cat <<'EOF'

Workspace profile detected:
- Legacy Visual Studio solution targeting .NET Framework 4.8.1
- Repo-pinned .NET SDK via global.json with install path rooted at .dotnet-sdk/
- Local dotnet tool manifest for CSharpier
- Global dotnet-coverage installation for MSTest coverage collection
- Windows-first build/test scripts that rely on VS tools such as vswhere, MSBuild.exe, and vstest.console.exe
- Outlook interop and VSTO references across the main add-in and supporting libraries

Codex Web caveat:
- This script verifies general Codex Web prerequisites everywhere, and it verifies the full Visual Studio task chain only on Windows hosts.
- In Linux-based Codex Web, the script completes with a warning after skipping the Windows-only Visual Studio checks because the restore/build/test tasks require Windows plus Visual Studio 2022 or Build Tools components discoverable through vswhere.exe.
- Full add-in build/debug parity still requires Windows with Visual Studio 2022, Office/VSTO tooling, and Outlook desktop.

Useful follow-up commands after a successful setup run:
- source ~/.bashrc
- dotnet --info
- dotnet tool run csharpier format .
- pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/vscode/Invoke-Restore.ps1 -SolutionPath TaskMaster.sln -Configuration Debug -Platform "Any CPU"
- pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/vscode/Invoke-VSBuild.ps1 -SolutionPath TaskMaster.sln -Configuration Debug -Platform "Any CPU" -EnableNETAnalyzers -EnforceCodeStyleInBuild
- pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/vscode/Invoke-VSBuild.ps1 -SolutionPath TaskMaster.sln -Configuration Debug -Platform "Any CPU" -EnableNullable -TreatWarningsAsErrors
- pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/vscode/Invoke-MSTest.ps1 -SearchRoot . -Configuration Debug
- pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/vscode/Invoke-MSTestWithCoverage.ps1 -SearchRoot . -Configuration Debug

Note:
- The repo's existing PowerShell helper scripts under scripts/vscode/ are Windows/Visual Studio oriented by design, so Linux-based Codex Web setup can prepare only a partial toolchain.
EOF
}

main() {
  log "Bootstrapping a Codex Web environment for ${REPO_ROOT}"

  export CI=true
  export DOTNET_CLI_TELEMETRY_OPTOUT=1
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
  export DOTNET_MULTILEVEL_LOOKUP=0
  export NUGET_XMLDOC_MODE=skip

  append_if_missing "${HOME}/.bashrc" 'export CI=true'
  append_if_missing "${HOME}/.bashrc" 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
  append_if_missing "${HOME}/.bashrc" 'export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'
  append_if_missing "${HOME}/.bashrc" 'export DOTNET_MULTILEVEL_LOOKUP=0'
  append_if_missing "${HOME}/.bashrc" 'export NUGET_XMLDOC_MODE=skip'

  install_apt_packages
  install_powershell
  install_nuget
  install_dotnet_sdk
  install_dotnet_tools
  install_dotnet_coverage
  verify_required_task_tooling
  install_actionlint
  restore_packages_if_needed

  if command -v git >/dev/null 2>&1; then
    git config --global core.autocrlf input || true
  fi

  write_repo_notes
  log "Setup complete."
}

main "$@"
