#!/usr/bin/env bash
set -Eeuo pipefail

QUIET=false
if [ "${1:-}" = "--quiet" ]; then
    QUIET=true
fi

say() {
    if [ "${QUIET}" = false ]; then
        printf '%s\n' "$*"
    fi
}

fail() {
    printf 'ERROR: %s\n' "$*" >&2
    exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_DIR="$(git -C "${SCRIPT_DIR}/.." rev-parse --show-toplevel 2>/dev/null || pwd)"

for required in dotnet pwsh git gh shellcheck shfmt actionlint jq sqlite3; do
    command -v "${required}" >/dev/null 2>&1 || fail "Missing required tool: ${required}"
done

if [ ! -f "${WORKSPACE_DIR}/global.json" ]; then
    fail "global.json not found at ${WORKSPACE_DIR}"
fi

EXPECTED_DOTNET_VERSION="$(jq -r '.sdk.version' "${WORKSPACE_DIR}/global.json")"
ACTUAL_DOTNET_VERSION="$(dotnet --version)"

if [ "${EXPECTED_DOTNET_VERSION}" != "${ACTUAL_DOTNET_VERSION}" ]; then
    fail "dotnet version mismatch. Expected ${EXPECTED_DOTNET_VERSION}, found ${ACTUAL_DOTNET_VERSION}"
fi

if [ "${EnableWindowsTargeting:-}" != "true" ]; then
    fail "EnableWindowsTargeting environment variable is not set to true"
fi

if [ ! -f "${WORKSPACE_DIR}/.devcontainer/devcontainer.json" ]; then
    fail "Standard devcontainer config is missing"
fi

if [ ! -f "${WORKSPACE_DIR}/.devcontainer/local/devcontainer.json" ]; then
    fail "Local alias devcontainer config is missing"
fi

if [ ! -f "${WORKSPACE_DIR}/.devcontainer/codespaces/devcontainer.json" ]; then
    fail "Codespaces alias devcontainer config is missing"
fi

if [ ! -f "${WORKSPACE_DIR}/OpenClaw.MailBridge.sln" ]; then
    fail "OpenClaw.MailBridge.sln not found"
fi

ENV_TYPE="local"
if [ "${CODESPACES:-}" = "true" ]; then
    ENV_TYPE="codespaces"
fi

say "========================================="
say "OpenClaw MailBridge devcontainer verified"
say "========================================="
say "Environment: ${ENV_TYPE}"
say "Workspace:   ${WORKSPACE_DIR}"
say "OS:          $(. /etc/os-release && printf '%s' "${PRETTY_NAME}")"
say "dotnet:      ${ACTUAL_DOTNET_VERSION}"
say "pwsh:        $(pwsh --version)"
say "git:         $(git --version)"
say "gh:          $(gh --version | head -n 1)"
say "shellcheck:  $(shellcheck --version | head -n 1)"
say "shfmt:       $(shfmt --version)"
say "actionlint:  $(actionlint -version | head -n 1)"
say "sqlite3:     $(sqlite3 --version)"
say ""
say "The container supports restore, build, and test workflows."
say "Running the net10.0-windows bridge and Outlook COM integration still requires Windows."
