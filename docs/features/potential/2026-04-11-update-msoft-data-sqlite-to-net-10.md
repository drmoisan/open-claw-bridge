---
title: "update-msoft-data-sqlite-to-net-10 - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-46"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# update-msoft-data-sqlite-to-net-10 (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

`src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` targets `net10.0-windows`. Every other `Microsoft.*` package in its `ItemGroup` is at version `10.0.5` (`Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.Extensions.Logging.Console`). A single outlier remains: `Microsoft.Data.Sqlite` is pinned at `8.0.11` (design audit deviation #18, Low severity, `csproj` line 22).

This version skew means the embedded SQLite native library and the `Microsoft.Data.Sqlite` managed layer ship at a .NET 8-era release while the rest of the host and dependency injection infrastructure is at the .NET 10 release. While no concrete behavioral regression has been identified, the mismatch creates unnecessary risk: the `SQLitePCLRaw` native bundle chosen by v8 may differ from the one that ships with v10, and the inconsistency makes dependency auditing and NuGet vulnerability scanning more difficult because one package appears with an anomalous version number in audit outputs.

## Proposed Behavior

Update the single `PackageReference` in `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` from:

```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.11" />
```

to:

```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.0" />
```

(or the latest stable `10.x` release available on NuGet at the time of implementation — the target is the same major version as the project's `TargetFramework`).

No other file changes are required. The `CacheRepository` uses only `SqliteConnection`, `SqliteCommand`, `SqliteDataReader`, `SqliteParameter`, and `SqliteException` — all of which are API-stable across this version range. The self-contained single-file publish (`SelfContained=true`, `PublishSingleFile=true`) will automatically embed the newer native SQLite library that ships with `10.x`.

## Acceptance Criteria (early draft)

- [ ] `Microsoft.Data.Sqlite` appears in `OpenClaw.MailBridge.csproj` at a `10.x` version, not `8.0.11`.
- [ ] The package version matches or is consistent with the other `Microsoft.*` packages in the same `ItemGroup` (currently `10.0.5`).
- [ ] `dotnet build` targeting `net10.0-windows` succeeds with no errors or warnings related to the version change.
- [ ] All 86 existing unit tests pass after the version bump; no new test failures are introduced.
- [ ] The self-contained single-file publish artifact builds successfully and embeds the updated native SQLite library.

## Constraints & Risks

- **Native SQLite library version change.** `Microsoft.Data.Sqlite` embeds the `SQLitePCLRaw` native SQLite bundle. Moving from v8 to v10 will embed a newer native SQLite binary. For the SQL constructs used in this project (`CREATE TABLE IF NOT EXISTS`, `INSERT OR IGNORE`, `SELECT`, `INSERT OR REPLACE`, JSON column writes) this is low risk — none rely on SQLite-version-specific behavior.
- **No source code changes required.** The API surface used by `CacheRepository` (`SqliteConnection`, `SqliteCommand`, `SqliteDataReader`) is unchanged across this version range. This is a version bump only.
- **Restore lock file.** If a `packages.lock.json` exists, it must be updated via `dotnet restore --force-evaluate` after the version change. The project does not currently show a lock file in the workspace, so this is a low-friction step.
- **Scope is one file, one line.** The change is confined to `OpenClaw.MailBridge.csproj`. No changes to test projects, `Contracts`, or `Client` are needed because those projects do not reference `Microsoft.Data.Sqlite`.
- **NuGet availability.** The `10.0.x` release of `Microsoft.Data.Sqlite` must be available as a stable (non-prerelease) package on nuget.org at the time of implementation. If only a prerelease exists, defer until a stable release ships.

## Test Conditions to Consider

- [ ] **Build verification:** `dotnet build src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` completes without error after the version change.
- [ ] **Unit test regression:** Run the full test suite (`dotnet test` or the repo test script); all 86 existing tests pass. Pay particular attention to `CacheRepository` tests (`Cache_repository_should_store_and_load_scan_state`, message/event upsert and query tests), as these directly exercise the SQLite layer.
- [ ] **Single-file publish:** `dotnet publish` with `SelfContained=true` and `PublishSingleFile=true` produces a valid executable without packaging errors.
- [ ] **No version inconsistency remains:** After the change, `dotnet list package` for the solution shows no `Microsoft.Data.Sqlite` entry at v8.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/update-msoft-data-sqlite-to-net-10/` folder from the template

