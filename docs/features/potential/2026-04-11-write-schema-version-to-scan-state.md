---
title: "write-schema-version-to-scan-state - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-40"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# write-schema-version-to-scan-state (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

The `scan_state` table in the SQLite cache is a generic key-value store (`key TEXT PRIMARY KEY, value TEXT NOT NULL`). It is used at runtime to record timestamps for inbox and calendar scan cycles (`TouchScanStateAsync`/`GetScanStateAsync`). The spec requires a `schema_version` key to be present in this table so that consumers and diagnostic tooling can determine which version of the cache schema is in use. This key is never written anywhere in the codebase — `InitializeAsync` creates the table but inserts no rows (design audit deviation #10, Medium severity; also listed in the "NOT EVIDENCED" block of the Runtime-Proof Audit section 6).

Without `schema_version`, there is no reliable way for operators or future migration code to know whether the on-disk database predates a schema change, making forward-compatibility management more difficult.

## Proposed Behavior

During `CacheRepository.InitializeAsync()`, after the three `CREATE TABLE IF NOT EXISTS` statements execute, insert the `schema_version` key with a fixed string value representing the current schema revision — using an `INSERT OR IGNORE` (or equivalent `ON CONFLICT DO NOTHING`) pattern so that the value is written only on first initialization and is not overwritten on subsequent bridge restarts. This preserves the version that was current when the database was first created, which is the correct value for migration detection.

The initial value should be a well-defined constant (e.g., `"1"`) defined as an internal constant in `CacheRepository` so it can be referenced in tests without a magic string. No new column, no schema change to the `scan_state` table definition, and no change to `TouchScanStateAsync` or `GetScanStateAsync` are required.

`TouchScanStateAsync` takes a `DateTimeOffset` and is not appropriate for writing `schema_version`; the insert must be performed inline in `InitializeAsync` as a direct SQL statement, consistent with how the table DDL is already executed in that method.

## Acceptance Criteria (early draft)

- [ ] After `InitializeAsync` completes on a new (empty) database, querying `SELECT value FROM scan_state WHERE key='schema_version'` returns a non-null, non-empty string.
- [ ] The returned value matches the expected current schema version constant defined in `CacheRepository`.
- [ ] Calling `InitializeAsync` a second time on the same database (simulating a bridge restart) does not overwrite a pre-existing `schema_version` value.
- [ ] `InitializeAsync` continues to create the `messages`, `events`, and `scan_state` tables as before; no regression to table creation behavior.
- [ ] The `schema_version` constant is accessible from the test project (via `InternalsVisibleTo`) so tests can assert the expected value without duplicating the literal.
- [ ] No existing `CacheRepository` tests fail after the change.

## Constraints & Risks

- **`INSERT OR IGNORE` semantics required.** The insert must be idempotent: subsequent calls to `InitializeAsync` on an existing database must leave the stored `schema_version` intact. An `INSERT OR IGNORE` (SQLite syntax: `INSERT OR IGNORE INTO scan_state(key,value) VALUES('schema_version','1')`) or `ON CONFLICT(key) DO NOTHING` variant achieves this.
- **Value must be a string.** The `scan_state.value` column is `TEXT NOT NULL`. The version value must be serialized as a plain string (e.g., `"1"`). It must not use the ISO-8601 date format that `TouchScanStateAsync` uses, to remain clearly distinguishable from timestamp entries.
- **`TouchScanStateAsync` and `GetScanStateAsync` are not the right mechanism.** `TouchScanStateAsync` accepts only `DateTimeOffset`; using it for `schema_version` would serialize a timestamp instead of a version string. The write must be a direct SQL statement in `InitializeAsync`.
- **Future schema migration is out of scope.** This feature only ensures the initial value is written. Logic to detect version mismatches, migrate data, or alter tables on version mismatch is a separate, future concern. This feature creates the observable artifact, nothing more.
- **Test isolation.** Existing tests use in-memory SQLite databases (`Mode=Memory;Cache=Shared`) with unique names per test. The new `schema_version` insert will appear in those databases as well, which is correct; tests that query `scan_state` directly should continue to work since the new row does not conflict with any timestamp-keyed row.

## Test Conditions to Consider

- [ ] **Unit — schema_version written on first init:** Create a new `CacheRepository` with an in-memory connection string, call `InitializeAsync`, then execute `SELECT value FROM scan_state WHERE key='schema_version'` directly and assert it equals the expected constant.
- [ ] **Unit — schema_version not overwritten on re-init:** Call `InitializeAsync` twice on the same in-memory database; assert the `schema_version` row's value is unchanged after the second call.
- [ ] **Unit — other scan_state keys unaffected:** After calling `InitializeAsync`, call `TouchScanStateAsync` for a timestamp key, then verify both the timestamp row and the `schema_version` row coexist in `scan_state` with correct values.
- [ ] **Regression — existing scan_state tests pass:** `Cache_repository_should_store_and_load_scan_state` and any other existing tests that call `InitializeAsync` must continue to pass unchanged.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/write-schema-version-to-scan-state/` folder from the template

