#!/usr/bin/env sh
set -eu

curl --fail --silent http://127.0.0.1:8081/health/live >/dev/null
