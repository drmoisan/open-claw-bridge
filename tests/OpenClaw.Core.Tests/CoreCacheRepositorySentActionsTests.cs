using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for the <see cref="ISentActionStore"/> implementation on
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> (issue #101, AC-1): record/exists
/// round-trip, duplicate-record idempotency, caller-supplied timestamp round-trip,
/// migration idempotency, pre-existing-database upgrade, and the lazy schema-ensure
/// guard. Uses in-memory shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositorySentActionsTests
{
    private static readonly string Key = SentActionKey.Build(
        "owner@contoso.com",
        "msg-1",
        SentActionKey.ProposalReply
    );

    private static string NewConnectionString(string label) =>
        $"Data Source=core-sa-{label}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    [TestMethod]
    public async Task IsRecordedAsync_should_return_false_for_unknown_key()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("unknown"));
        await repo.InitializeAsync();

        // Act
        var recorded = await repo.IsRecordedAsync(Key, CancellationToken.None);

        // Assert
        recorded.Should().BeFalse("no action has been recorded for the key");
    }

    [TestMethod]
    public async Task RecordAsync_then_IsRecordedAsync_should_return_true()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("roundtrip"));
        await repo.InitializeAsync();
        var recordedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act
        await repo.RecordAsync(Key, recordedAt, CancellationToken.None);
        var recorded = await repo.IsRecordedAsync(Key, CancellationToken.None);

        // Assert
        recorded.Should().BeTrue("the key was just recorded");
    }

    [TestMethod]
    public async Task RecordAsync_duplicate_key_should_not_throw_and_leave_one_row()
    {
        // Arrange
        var connectionString = NewConnectionString("duplicate");
        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await repo.InitializeAsync();
        var first = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);
        var second = new DateTimeOffset(2026, 7, 2, 11, 45, 0, TimeSpan.Zero);

        // Act
        await repo.RecordAsync(Key, first, CancellationToken.None);
        var duplicate = async () => await repo.RecordAsync(Key, second, CancellationToken.None);

        // Assert
        await duplicate.Should().NotThrowAsync("RecordAsync is idempotent for a duplicate key");

        await using var check = new SqliteConnection(connectionString);
        await check.OpenAsync();
        var count = check.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM sent_actions WHERE dedupe_key = $key;";
        count.Parameters.AddWithValue("$key", Key);
        Convert
            .ToInt32(await count.ExecuteScalarAsync())
            .Should()
            .Be(1, "a duplicate record must not add a second row");
    }

    [TestMethod]
    public async Task RecordAsync_should_round_trip_caller_supplied_timestamp_in_iso8601_o_form()
    {
        // Arrange: a non-UTC offset verifies the store normalizes to UTC before formatting.
        var connectionString = NewConnectionString("timestamp");
        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await repo.InitializeAsync();
        var recordedAt = new DateTimeOffset(2026, 7, 2, 12, 30, 0, TimeSpan.FromHours(2));

        // Act
        await repo.RecordAsync(Key, recordedAt, CancellationToken.None);

        // Assert: read back via a direct query on a second connection to the shared database.
        await using var check = new SqliteConnection(connectionString);
        await check.OpenAsync();
        var query = check.CreateCommand();
        query.CommandText = "SELECT recorded_at_utc FROM sent_actions WHERE dedupe_key = $key;";
        query.Parameters.AddWithValue("$key", Key);
        var stored = (string?)await query.ExecuteScalarAsync();
        stored
            .Should()
            .Be(
                "2026-07-02T10:30:00.0000000Z",
                "the caller-supplied timestamp must be stored as UTC in round-trip (O) form"
            );
    }

    [TestMethod]
    public async Task InitializeAsync_twice_should_not_throw_and_sent_actions_should_exist()
    {
        // Arrange
        var connectionString = NewConnectionString("initialize");
        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act
        await repo.InitializeAsync();
        var secondInit = async () => await repo.InitializeAsync();

        // Assert
        await secondInit.Should().NotThrowAsync("schema initialization must be idempotent");

        await using var check = new SqliteConnection(connectionString);
        await check.OpenAsync();
        (await TableExistsAsync(check, "sent_actions"))
            .Should()
            .BeTrue("InitializeAsync must create the sent_actions table");
    }

    [TestMethod]
    public async Task InitializeAsync_should_add_sent_actions_to_pre_existing_database()
    {
        // Arrange: seed an existing database that has a current table but no sent_actions
        // (the pre-#101 shape). The anchor connection keeps the in-memory database alive.
        var connectionString = NewConnectionString("upgrade");
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        var seed = anchor.CreateCommand();
        seed.CommandText =
            @"
CREATE TABLE IF NOT EXISTS poll_cursors(
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL
);";
        await seed.ExecuteNonQueryAsync();
        (await TableExistsAsync(anchor, "sent_actions"))
            .Should()
            .BeFalse("the seeded database must not have sent_actions before the upgrade");

        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act
        await repo.InitializeAsync();

        // Assert
        (await TableExistsAsync(anchor, "sent_actions"))
            .Should()
            .BeTrue("InitializeAsync must add sent_actions to a pre-existing database");
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty key")]
    [DataRow("no-colon-key", DisplayName = "no separators")]
    [DataRow("owner@contoso.com:msg-1", DisplayName = "two components only")]
    [DataRow("owner@contoso.com: :proposal-reply", DisplayName = "whitespace component")]
    public async Task RecordAsync_malformed_key_should_throw_ArgumentException(string key)
    {
        // Arrange: keys that do not match the fixed {mailbox}:{messageId}:{actionType}
        // shape must fail fast instead of persisting an unparseable row.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("malformed"));
        await repo.InitializeAsync();
        var recordedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act
        var act = async () => await repo.RecordAsync(key, recordedAt, CancellationToken.None);

        // Assert
        (
            await act.Should().ThrowAsync<ArgumentException>("the key shape is invalid")
        ).WithParameterName("dedupeKey");
    }

    [TestMethod]
    public async Task Store_methods_should_work_on_fresh_database_without_InitializeAsync()
    {
        // Arrange: no InitializeAsync call; the lazy schema-ensure guard must create the table.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("lazy"));
        var recordedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act
        var missBeforeRecord = await repo.IsRecordedAsync(Key, CancellationToken.None);
        await repo.RecordAsync(Key, recordedAt, CancellationToken.None);
        var hitAfterRecord = await repo.IsRecordedAsync(Key, CancellationToken.None);

        // Assert
        missBeforeRecord.Should().BeFalse("nothing is recorded on a fresh database");
        hitAfterRecord.Should().BeTrue("the lazy schema-ensure guard makes the store usable");
    }
}
