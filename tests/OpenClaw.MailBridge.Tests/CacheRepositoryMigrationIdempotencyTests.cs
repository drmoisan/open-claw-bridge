using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Targeted regression test for issue #45, acceptance criterion AC-4: the
/// <see cref="CacheRepository.InitializeAsync"/> method must be idempotent across
/// repeated invocations on the same database. The migration that adds
/// <c>events.response_status</c> uses a <c>PRAGMA table_info</c> existence check
/// so re-running it cannot throw a duplicate-column error. Uses an in-memory
/// SQLite database so no temp files are created.
/// </summary>
[TestClass]
public sealed class CacheRepositoryMigrationIdempotencyTests
{
    [TestMethod]
    public async Task InitializeAsync_should_add_response_status_column_on_pre_migration_schema()
    {
        // Arrange — build a shared in-memory database that already has a legacy
        // `events` table without the `response_status` column. This forces the
        // migration to take the ALTER TABLE branch rather than the early-return.
        var connectionString =
            $"Data Source=migration-alter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using (var seed = new SqliteConnection(connectionString))
        {
            await seed.OpenAsync();
            var ddl = seed.CreateCommand();
            ddl.CommandText =
                @"CREATE TABLE events(
                    bridge_id TEXT PRIMARY KEY,
                    entry_id TEXT NULL,
                    store_id TEXT NULL,
                    global_appointment_id TEXT NULL,
                    item_kind TEXT NOT NULL,
                    subject TEXT NULL,
                    start_utc TEXT NOT NULL,
                    end_utc TEXT NOT NULL,
                    location TEXT NULL,
                    busy_status INTEGER NULL,
                    meeting_status INTEGER NULL,
                    is_recurring INTEGER NOT NULL,
                    sensitivity INTEGER NULL,
                    organizer TEXT NULL,
                    required_attendees_json TEXT NULL,
                    optional_attendees_json TEXT NULL,
                    resources_json TEXT NULL,
                    body_preview TEXT NULL,
                    protected_fields_available INTEGER NOT NULL,
                    is_redacted INTEGER NOT NULL,
                    last_modified_utc TEXT NULL,
                    last_seen_utc TEXT NOT NULL
                );";
            await ddl.ExecuteNonQueryAsync();
            // Anchor connection to keep the in-memory DB alive while we
            // construct the repository below (Cache=Shared is required).
            using var repo = new CacheRepository(connectionString);

            var before = await ReadEventsColumnNamesAsync(connectionString);
            before
                .Should()
                .NotContain(
                    "response_status",
                    "pre-migration schema must intentionally omit response_status"
                );

            // Act
            await repo.InitializeAsync();

            // Assert — the ALTER branch added the missing column without error.
            var after = await ReadEventsColumnNamesAsync(connectionString);
            after.Should().Contain("response_status");
        }
    }

    [TestMethod]
    public async Task InitializeAsync_should_be_idempotent_and_keep_events_schema_stable()
    {
        // Arrange — a shared in-memory database anchor so the schema survives
        // between the two InitializeAsync invocations.
        var connectionString =
            $"Data Source=migration-idem-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var repo = new CacheRepository(connectionString);

        // Act — initialize twice; the second call must not throw.
        await repo.InitializeAsync();
        var firstRunColumns = await ReadEventsColumnNamesAsync(connectionString);

        Func<Task> secondInvocation = async () => await repo.InitializeAsync();
        await secondInvocation.Should().NotThrowAsync("migration must be idempotent");

        var secondRunColumns = await ReadEventsColumnNamesAsync(connectionString);

        // Assert — schema shape after the second invocation matches the first.
        secondRunColumns
            .Should()
            .BeEquivalentTo(
                firstRunColumns,
                "repeated migrations must not alter the events table schema"
            );
        secondRunColumns
            .Should()
            .Contain("response_status", "response_status column must be present after migration");
    }

    [TestMethod]
    public async Task InitializeAsync_should_add_linked_appointment_column_on_pre_146_messages_schema()
    {
        // Arrange — a legacy `messages` table that predates issue #146 (it carries the issue-#73
        // resolved columns but not `linked_global_appointment_id`). This forces the messages
        // migration to take the guarded-ALTER branch for the linkage column.
        var connectionString =
            $"Data Source=msg-link-alter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using (var seed = new SqliteConnection(connectionString))
        {
            await seed.OpenAsync();
            var ddl = seed.CreateCommand();
            ddl.CommandText =
                @"CREATE TABLE messages(
                    bridge_id TEXT PRIMARY KEY,
                    entry_id TEXT NOT NULL,
                    store_id TEXT NULL,
                    item_kind TEXT NOT NULL,
                    subject TEXT NULL,
                    received_utc TEXT NULL,
                    sent_utc TEXT NULL,
                    importance INTEGER NULL,
                    sensitivity INTEGER NULL,
                    unread INTEGER NOT NULL,
                    has_attachments INTEGER NOT NULL,
                    message_class TEXT NULL,
                    sender_name TEXT NULL,
                    sender_email TEXT NULL,
                    to_json TEXT NULL,
                    cc_json TEXT NULL,
                    body_preview TEXT NULL,
                    protected_fields_available INTEGER NOT NULL,
                    is_redacted INTEGER NOT NULL,
                    last_seen_utc TEXT NOT NULL,
                    sender_email_resolved TEXT NULL,
                    from_email_address TEXT NULL,
                    conversation_id TEXT NULL,
                    meeting_message_type INTEGER NULL
                );";
            await ddl.ExecuteNonQueryAsync();
            using var repo = new CacheRepository(connectionString);

            var before = await ReadMessagesColumnNamesAsync(connectionString);
            before
                .Should()
                .NotContain(
                    "linked_global_appointment_id",
                    "pre-#146 schema must intentionally omit the linkage column"
                );

            // Act
            await repo.InitializeAsync();

            // Assert — the ALTER branch added the missing column.
            var after = await ReadMessagesColumnNamesAsync(connectionString);
            after.Should().Contain("linked_global_appointment_id");

            // And re-running the migration is idempotent (no duplicate-column error).
            Func<Task> secondInit = async () => await repo.InitializeAsync();
            await secondInit
                .Should()
                .NotThrowAsync("the messages linkage migration must be idempotent");
        }
    }

    private static async Task<List<string>> ReadEventsColumnNamesAsync(string connectionString) =>
        await ReadColumnNamesAsync(connectionString, "events");

    private static async Task<List<string>> ReadMessagesColumnNamesAsync(string connectionString) =>
        await ReadColumnNamesAsync(connectionString, "messages");

    private static async Task<List<string>> ReadColumnNamesAsync(
        string connectionString,
        string table
    )
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // table_info column index 1 is the column name.
            columns.Add(reader.GetString(1));
        }
        return columns;
    }
}
