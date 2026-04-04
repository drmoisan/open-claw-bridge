#!/usr/bin/env bash
set -Eeuo pipefail

# codex-web-setup.sh
#
# Purpose:
#   Bootstrap a repository inside a Codex Web cloud environment.
#
# Design goals:
#   - Safe in non-interactive cloud setup
#   - Works on detached HEAD checkouts
#   - Performs only environment/bootstrap tasks
#   - Never mutates git history or requires GitHub auth
#   - Emits clear diagnostics to help troubleshoot failures
#
# What it does:
#   - Prints basic repo/runtime diagnostics
#   - Detects common package manager manifests
#   - Runs conservative dependency install steps when appropriate
#   - Optionally runs a repo-provided bootstrap hook if present
#
# What it does NOT do:
#   - No git fetch/push/branch creation
#   - No interactive prompts
#   - No Codex/GitHub account linking
#   - No repo scaffolding generation

SCRIPT_NAME="$(basename "$0")"

say() { printf '%s\n' "$*"; }
warn() { printf 'WARN: %s\n' "$*" >&2; }
err() { printf 'ERROR: %s\n' "$*" >&2; }

have() {
    command -v "$1" >/dev/null 2>&1
}

repo_root() {
    git rev-parse --show-toplevel 2>/dev/null || pwd
}

head_ref() {
    git rev-parse --abbrev-ref HEAD 2>/dev/null || printf 'UNKNOWN\n'
}

head_sha() {
    git rev-parse --short HEAD 2>/dev/null || printf 'UNKNOWN\n'
}

maybe_run() {
    say "==> $*"
    "$@"
}

install_node() {
    if [[ -f package-lock.json ]]; then
        have npm || {
            warn "npm not found; skipping Node install"
            return 0
        }
        maybe_run npm ci
        return 0
    fi

    if [[ -f npm-shrinkwrap.json ]]; then
        have npm || {
            warn "npm not found; skipping Node install"
            return 0
        }
        maybe_run npm ci
        return 0
    fi

    if [[ -f pnpm-lock.yaml ]]; then
        if have pnpm; then
            maybe_run pnpm install --frozen-lockfile
        elif have corepack; then
            maybe_run corepack pnpm install --frozen-lockfile
        else
            warn "pnpm lockfile found but pnpm/corepack unavailable; skipping Node install"
        fi
        return 0
    fi

    if [[ -f yarn.lock ]]; then
        if have yarn; then
            maybe_run yarn install --frozen-lockfile
        elif have corepack; then
            maybe_run corepack yarn install --frozen-lockfile
        else
            warn "yarn lockfile found but yarn/corepack unavailable; skipping Node install"
        fi
        return 0
    fi

    if [[ -f package.json ]]; then
        have npm || {
            warn "package.json found but npm not available; skipping Node install"
            return 0
        }
        maybe_run npm install
    fi
}

install_python() {
    if [[ -f requirements.txt ]]; then
        have python || have python3 || {
            warn "Python not found; skipping pip install"
            return 0
        }
        local py
        py="$(command -v python || command -v python3)"
        maybe_run "$py" -m pip install --upgrade pip
        maybe_run "$py" -m pip install -r requirements.txt
        return 0
    fi

    if [[ -f pyproject.toml ]]; then
        if have poetry && grep -Eq '^\[tool\.poetry\]' pyproject.toml; then
            maybe_run poetry install --no-interaction
            return 0
        fi

        if have uv; then
            maybe_run uv sync
            return 0
        fi

        if have python || have python3; then
            local py
            py="$(command -v python || command -v python3)"
            maybe_run "$py" -m pip install --upgrade pip
            maybe_run "$py" -m pip install -e .
            return 0
        fi

        warn "pyproject.toml found but no supported Python installer available; skipping Python install"
    fi
}

install_ruby() {
    if [[ -f Gemfile ]]; then
        have bundle || {
            warn "Gemfile found but bundler unavailable; skipping bundle install"
            return 0
        }
        maybe_run bundle install
    fi
}

install_go() {
    if [[ -f go.mod ]]; then
        have go || {
            warn "go.mod found but Go unavailable; skipping go mod download"
            return 0
        }
        maybe_run go mod download
    fi
}

install_rust() {
    if [[ -f Cargo.toml ]]; then
        have cargo || {
            warn "Cargo.toml found but cargo unavailable; skipping cargo fetch"
            return 0
        }
        maybe_run cargo fetch
    fi
}

install_php() {
    if [[ -f composer.json ]]; then
        have composer || {
            warn "composer.json found but composer unavailable; skipping composer install"
            return 0
        }
        maybe_run composer install --no-interaction --prefer-dist
    fi
}

install_java() {
    if [[ -f gradlew ]]; then
        chmod +x ./gradlew || true
        maybe_run ./gradlew dependencies
        return 0
    fi

    if [[ -f pom.xml ]]; then
        have mvn || {
            warn "pom.xml found but mvn unavailable; skipping maven dependency resolution"
            return 0
        }
        maybe_run mvn -B -q -DskipTests dependency:go-offline
    fi
}

run_repo_hook() {
    local hook=""

    for candidate in \
        .codex/setup.sh \
        codex/setup.sh \
        scripts/codex-setup.sh \
        scripts/setup-codex.sh \
        .devcontainer/postCreateCommand.sh; do
        if [[ -f "$candidate" ]]; then
            hook="$candidate"
            break
        fi
    done

    if [[ -n "$hook" ]]; then
        say "==> Running repo bootstrap hook: $hook"
        bash "$hook"
    else
        say "==> No repo bootstrap hook found"
    fi
}

main() {
    say "==> $SCRIPT_NAME starting"

    local root
    root="$(repo_root)"
    cd "$root"

    say "==> Repository root: $root"
    say "==> HEAD ref: $(head_ref)"
    say "==> HEAD sha: $(head_sha)"

    if have git; then
        say "==> Git status (short):"
        git status --short || true
    fi

    install_node
    install_python
    install_ruby
    install_go
    install_rust
    install_php
    install_java
    run_repo_hook

    say "==> $SCRIPT_NAME complete"
}

main "$@"
