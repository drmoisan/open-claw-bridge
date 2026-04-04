#!/usr/bin/env bash
set -Eeuo pipefail

# codex-web-setup.sh
#
# Purpose:
#   Prepare the current GitHub repository for use with Codex Web.
#
# What it does:
#   - Verifies local prerequisites
#   - Verifies current directory is a git repo
#   - Verifies a GitHub remote exists
#   - Ensures there is at least one commit
#   - Optionally creates .codex/ scaffolding
#   - Pushes the current branch to GitHub
#   - Prints next manual steps for Codex Web
#
# What it does NOT do:
#   - It cannot complete Codex Web GitHub OAuth/account linking.
#   - It does not create or manage Codex Web sessions.
#
# Notes:
#   - Codex configuration is stored in ~/.codex/config.toml or repo-scoped
#     .codex/config.toml. The CLI and IDE extension share these config layers.
#   - Codex Web setup itself is done in the Codex web product by connecting GitHub.
#
# References:
#   - Codex Web setup: https://developers.openai.com/codex/cloud/
#   - Config basics:   https://developers.openai.com/codex/config-basic/

SCRIPT_NAME="$(basename "$0")"
DEFAULT_BRANCH_FALLBACK="main"

say() { printf '%s\n' "$*"; }
warn() { printf 'WARN: %s\n' "$*" >&2; }
err() { printf 'ERROR: %s\n' "$*" >&2; }
die() {
    err "$*"
    exit 1
}

require_cmd() {
    command -v "$1" >/dev/null 2>&1 || die "Required command not found: $1"
}

confirm() {
    local prompt="${1:-Continue?}"
    local reply
    read -r -p "$prompt [y/N]: " reply || true
    [[ "${reply:-}" =~ ^[Yy]$ ]]
}

git_root() {
    git rev-parse --show-toplevel 2>/dev/null
}

has_initial_commit() {
    git rev-parse --verify HEAD >/dev/null 2>&1
}

current_branch() {
    git rev-parse --abbrev-ref HEAD
}

default_branch_guess() {
    git symbolic-ref "refs/remotes/origin/HEAD" 2>/dev/null |
        sed 's@^refs/remotes/origin/@@' ||
        true
}

github_remote_name() {
    local remote
    while read -r remote; do
        local url
        url="$(git remote get-url "$remote" 2>/dev/null || true)"
        if [[ "$url" == *github.com* ]]; then
            printf '%s\n' "$remote"
            return 0
        fi
    done < <(git remote)
    return 1
}

github_repo_web_url() {
    local remote_name="$1"
    local url
    url="$(git remote get-url "$remote_name")"

    # Normalize common GitHub remote formats to https URL.
    # Handles:
    #   git@github.com:owner/repo.git
    #   https://github.com/owner/repo.git
    #   ssh://git@github.com/owner/repo.git
    url="${url#ssh://git@github.com/}"
    if [[ "$url" == git@github.com:* ]]; then
        url="${url#git@github.com:}"
    elif [[ "$url" == https://github.com/* ]]; then
        url="${url#https://github.com/}"
    fi
    url="${url%.git}"

    printf 'https://github.com/%s\n' "$url"
}

ensure_codex_dir() {
    if [[ ! -d ".codex" ]]; then
        mkdir -p ".codex"
        say "Created .codex/"
    fi
}

create_project_config_if_missing() {
    local config_path=".codex/config.toml"
    if [[ -f "$config_path" ]]; then
        say "Found existing $config_path"
        return 0
    fi

    cat >"$config_path" <<'EOF'
# Project-scoped Codex configuration
# See:
#   https://developers.openai.com/codex/config-basic/
#   https://developers.openai.com/codex/config-reference/

# Keep this intentionally minimal and conservative.
# Adjust only if your team has a defined Codex policy.

# Example:
# profile = "default"

[projects]
trusted = true
EOF

    say "Created $config_path"
}

create_agents_md_if_missing() {
    local file="AGENTS.md"
    if [[ -f "$file" ]]; then
        say "Found existing $file"
        return 0
    fi

    cat >"$file" <<'EOF'
# AGENTS.md

Repository instructions for Codex and related coding agents.

## Principles
- Make minimal, high-confidence changes.
- Preserve existing architecture unless explicitly asked to refactor.
- Do not broaden scope.
- Prefer deterministic fixes over speculative improvements.
- Run relevant tests before concluding work.

## Workflow
- Read the relevant files before editing.
- State assumptions when they matter.
- Keep diffs focused.
- Update docs when behavior changes.
EOF

    say "Created $file"
}

print_summary() {
    local remote_name="$1"
    local repo_url="$2"
    local branch="$3"

    cat <<EOF

Setup complete.

Repository
  Remote:  $remote_name
  URL:     $repo_url
  Branch:  $branch

Next steps for Codex Web
  1. Open Codex Web.
  2. Connect your GitHub account there if you have not already.
  3. Select this repository.
  4. Start a task against branch: $branch

Helpful links
  Repo:    $repo_url
  Branch:  $repo_url/tree/$branch

Notes
  - Codex Web setup requires connecting GitHub in the Codex web interface.
  - Repo/project configuration can also live in .codex/config.toml.

EOF
}

main() {
    require_cmd git

    say "==> $SCRIPT_NAME starting"

    local root
    root="$(git_root)" || die "Current directory is not inside a git repository."
    cd "$root"

    say "==> Repository root: $root"

    if ! has_initial_commit; then
        die "Repository has no commits yet. Create an initial commit first."
    fi

    local remote_name
    remote_name="$(github_remote_name || true)"
    [[ -n "${remote_name:-}" ]] || die "No GitHub remote found."

    local repo_url
    repo_url="$(github_repo_web_url "$remote_name")"

    local branch
    branch="$(current_branch)"
    [[ "$branch" != "HEAD" ]] || die "Detached HEAD is not supported by this setup script."

    say "==> GitHub remote: $remote_name"
    say "==> GitHub repo:   $repo_url"
    say "==> Branch:        $branch"

    if ! git diff --quiet || ! git diff --cached --quiet; then
        warn "Working tree has uncommitted changes."
        if ! confirm "Continue anyway?"; then
            die "Aborted."
        fi
    fi

    if confirm "Create .codex/ scaffolding if missing?"; then
        ensure_codex_dir
        create_project_config_if_missing
    fi

    if confirm "Create AGENTS.md if missing?"; then
        create_agents_md_if_missing
    fi

    say "==> Fetching remote refs"
    git fetch "$remote_name" --prune

    local origin_default
    origin_default="$(default_branch_guess || true)"
    if [[ -z "${origin_default:-}" ]]; then
        origin_default="$DEFAULT_BRANCH_FALLBACK"
    fi

    say "==> Remote default branch guess: $origin_default"

    if git ls-remote --exit-code --heads "$remote_name" "$branch" >/dev/null 2>&1; then
        say "==> Remote branch already exists: $branch"
        if confirm "Push current local commits to $remote_name/$branch?"; then
            git push "$remote_name" "$branch"
        fi
    else
        say "==> Remote branch does not exist yet: $branch"
        if confirm "Create and push branch $branch to GitHub?"; then
            git push -u "$remote_name" "$branch"
        else
            warn "Branch was not pushed. Codex Web will need the repo/branch available on GitHub."
        fi
    fi

    print_summary "$remote_name" "$repo_url" "$branch"
}

main "$@"
