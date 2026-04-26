# Baseline — Tool Availability

Timestamp: 2026-04-22T23-20

## dotnet

Command: dotnet --version
EXIT_CODE: 0
Output Summary: `10.0.202`

## docker

Command: docker --version
EXIT_CODE: 0
Output Summary: `Docker version 29.4.0, build 9d7ad9f`

Note: the Docker CLI is present on PATH. Whether the Docker daemon is reachable from the sandbox and whether Phase 5 build/recreate steps can execute end-to-end is an operator-dependent runtime concern and will be recorded at the time those tasks are attempted.

## gh

Command: gh --version
EXIT_CODE: 0
Output Summary: `gh version 2.87.3 (2026-02-23)`
