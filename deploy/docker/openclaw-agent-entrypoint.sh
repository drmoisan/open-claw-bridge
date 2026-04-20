#!/bin/sh
set -eu

seed_dir="/opt/openclaw-assistant-seed"
workspace_dir="/workspace"
runtime_dir="/.openclaw"

mkdir -p "$workspace_dir"
mkdir -p "$runtime_dir"

# Seed the managed workspace files only when they are missing so operator
# onboarding state written under /workspace survives container restarts.
# The /.openclaw runtime directory is tmpfs and is always re-seeded.
if [ ! -e "$workspace_dir/AGENTS.md" ]; then
    cp "$seed_dir/AGENTS.md" "$workspace_dir/AGENTS.md"
fi
if [ ! -e "$workspace_dir/IDENTITY.md" ]; then
    cp "$seed_dir/IDENTITY.md" "$workspace_dir/IDENTITY.md"
fi
if [ ! -e "$workspace_dir/SOUL.md" ]; then
    cp "$seed_dir/SOUL.md" "$workspace_dir/SOUL.md"
fi
if [ ! -e "$workspace_dir/TOOLS.md" ]; then
    cp "$seed_dir/TOOLS.md" "$workspace_dir/TOOLS.md"
fi
if [ ! -e "$workspace_dir/USER.md" ]; then
    cp "$seed_dir/USER.md" "$workspace_dir/USER.md"
fi
if [ ! -e "$workspace_dir/openclaw.json" ]; then
    cp "$seed_dir/openclaw.json" "$workspace_dir/openclaw.json"
fi
cp "$seed_dir/openclaw.json" "$runtime_dir/openclaw.json"

mkdir -p "$workspace_dir/skills/mailbridge_admin"
if [ ! -e "$workspace_dir/skills/mailbridge_admin/SKILL.md" ]; then
    cp "$seed_dir/skills/mailbridge_admin/SKILL.md" "$workspace_dir/skills/mailbridge_admin/SKILL.md"
fi

exec docker-entrypoint.sh "$@"
