using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for the <see cref="ISeriesMoveHistory"/> implementation on
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> (issue #105, AC-1): record/query
/// round-trip, duplicate-record idempotency, non-UTC normalization, descending order,
/// series isolation, migration idempotency, pre-existing-database upgrade, the lazy
/// schema-ensure guard, blank-key rejection, and restart persistence. Uses in-memory
/// shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositorySeriesMovesTests
{
    private const string SeriesKey = "series-master-105";

    private static string NewConnectionString(string label) =>
        $"Data Source=core-sm-{label}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    [TestMethod]
    public async Task RecordMoveAsync_then_query_should_round_trip_occurrence_starts()
    {
        // Arrange (scenario a): recorded occurrence starts come back as the same instants.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("roundtrip"));
        await repo.InitializeAsync();
        var occurrenceStart = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);
        var movedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act
        await repo.RecordMoveAsync(SeriesKey, occurrenceStart, movedAt, CancellationToken.None);
        var starts = await repo.GetMovedOccurrenceStartsAsync(SeriesKey, CancellationToken.None);

        // Assert
        starts
            .Should()
            .ContainSingle("one move was recorded")
            .Which.Should()
            .Be(occurrenceStart, "the caller-supplied occurrence start must round-trip");
    }

    [TestMethod]
    public async Task RecordMoveAsync_duplicate_pair_should_not_throw_and_leave_one_row()
    {
        // Arrange (scenario b): idempotency for an identical (seriesKey, occurrenceStartUtc).
        var connectionString = NewConnectionString("duplicate");
        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await repo.InitializeAsync();
        var occurrenceStart = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);
        var firstMovedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);
        var secondMovedAt = new DateTimeOffset(2026, 7, 2, 11, 45, 0, TimeSpan.Zero);

        // Act
        await repo.RecordMoveAsync(
            SeriesKey,
            occurrenceStart,
            firstMovedAt,
            CancellationToken.None
        );
        var duplicate = async () =>
            await repo.RecordMoveAsync(
                SeriesKey,
                occurrenceStart,
                secondMovedAt,
                CancellationToken.None
            );

        // Assert
        await duplicate
            .Should()
            .NotThrowAsync("RecordMoveAsync is idempotent for a duplicate pair");

        await using var check = new SqliteConnection(connectionString);
        await check.OpenAsync();
        var count = check.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM series_moves WHERE series_key = $key;";
        count.Parameters.AddWithValue("$key", SeriesKey);
        Convert
            .ToInt32(await count.ExecuteScalarAsync())
            .Should()
            .Be(1, "a duplicate record must not add a second row");
    }

    [TestMethod]
    public async Task RecordMoveAsync_should_normalize_non_utc_offset_to_utc_o_form()
    {
        // Arrange (scenario c): a non-UTC offset must be stored as "O"-format UTC and
        // queried back as the same instant.
        var connectionString = NewConnectionString("normalize");
        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await repo.InitializeAsync();
        var occurrenceStart = new DateTimeOffset(2026, 7, 6, 17, 0, 0, TimeSpan.FromHours(2));
        var movedAt = new DateTimeOffset(2026, 7, 2, 12, 30, 0, TimeSpan.FromHours(2));

        // Act
        await repo.RecordMoveAsync(SeriesKey, occurrenceStart, movedAt, CancellationToken.None);
        var starts = await repo.GetMovedOccurrenceStartsAsync(SeriesKey, CancellationToken.None);

        // Assert: the stored string is UTC round-trip form; the queried value is the same instant.
        await using var check = new SqliteConnection(connectionString);
        await check.OpenAsync();
        var query = check.CreateCommand();
        query.CommandText =
            "SELECT occurrence_start_utc FROM series_moves WHERE series_key = $key;";
        query.Parameters.AddWithValue("$key", SeriesKey);
        var stored = (string?)await query.ExecuteScalarAsync();
        stored
            .Should()
            .Be(
                "2026-07-06T15:00:00.0000000Z",
                "the occurrence start must be stored as UTC in round-trip (O) form"
            );
        starts
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(occurrenceStart, "the queried value must equal the same instant");
    }

    [TestMethod]
    public async Task GetMovedOccurrenceStartsAsync_should_return_most_recent_first()
    {
        // Arrange (scenario d): multiple rows come back in descending occurrence-start order.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("order"));
        await repo.InitializeAsync();
        var oldest = new DateTimeOffset(2026, 6, 22, 15, 0, 0, TimeSpan.Zero);
        var middle = new DateTimeOffset(2026, 6, 29, 15, 0, 0, TimeSpan.Zero);
        var newest = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);
        var movedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act: record out of order to prove the ordering comes from the query.
        await repo.RecordMoveAsync(SeriesKey, middle, movedAt, CancellationToken.None);
        await repo.RecordMoveAsync(SeriesKey, newest, movedAt, CancellationToken.None);
        await repo.RecordMoveAsync(SeriesKey, oldest, movedAt, CancellationToken.None);
        var starts = await repo.GetMovedOccurrenceStartsAsync(SeriesKey, CancellationToken.None);

        // Assert
        starts
            .Should()
            .Equal(
                [newest, middle, oldest],
                "occurrence starts must be returned most recent first"
            );
    }

    [TestMethod]
    public async Task GetMovedOccurrenceStartsAsync_should_isolate_series_keys()
    {
        // Arrange (scenario e): rows recorded under key A are invisible to a query for key B.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("isolation"));
        await repo.InitializeAsync();
        var occurrenceStart = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);
        var movedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act
        await repo.RecordMoveAsync("series-a", occurrenceStart, movedAt, CancellationToken.None);
        var otherSeries = await repo.GetMovedOccurrenceStartsAsync(
            "series-b",
            CancellationToken.None
        );

        // Assert
        otherSeries.Should().BeEmpty("moves recorded under series-a must not leak to series-b");
    }

    [TestMethod]
    public async Task InitializeAsync_twice_should_not_throw_and_series_moves_should_exist()
    {
        // Arrange (scenario f): migration idempotency.
        var connectionString = NewConnectionString("initialize");
        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act
        await repo.InitializeAsync();
        var secondInit = async () => await repo.InitializeAsync();

        // Assert
        await secondInit.Should().NotThrowAsync("schema initialization must be idempotent");

        await using var check = new SqliteConnection(connectionString);
        await check.OpenAsync();
        (await TableExistsAsync(check, "series_moves"))
            .Should()
            .BeTrue("InitializeAsync must create the series_moves table");
    }

    [TestMethod]
    public async Task InitializeAsync_should_add_series_moves_to_pre_existing_database()
    {
        // Arrange (scenario g): seed a database with a pre-#105 table but no series_moves.
        // The anchor connection keeps the in-memory database alive.
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
);
CREATE TABLE IF NOT EXISTS sent_actions(dedupe_key TEXT PRIMARY KEY, mailbox TEXT NOT NULL, message_id TEXT NOT NULL, action_type TEXT NOT NULL, recorded_at_utc TEXT NOT NULL);";
        await seed.ExecuteNonQueryAsync();
        (await TableExistsAsync(anchor, "series_moves"))
            .Should()
            .BeFalse("the seeded database must not have series_moves before the upgrade");

        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act
        await repo.InitializeAsync();

        // Assert
        (await TableExistsAsync(anchor, "series_moves"))
            .Should()
            .BeTrue("InitializeAsync must add series_moves to a pre-existing database");
    }

    [TestMethod]
    public async Task Store_methods_should_work_on_fresh_database_without_InitializeAsync()
    {
        // Arrange (scenario h): no InitializeAsync call; the lazy schema-ensure guard
        // must create the table before the first store operation.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("lazy"));
        var occurrenceStart = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);
        var movedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act
        var emptyBeforeRecord = await repo.GetMovedOccurrenceStartsAsync(
            SeriesKey,
            CancellationToken.None
        );
        await repo.RecordMoveAsync(SeriesKey, occurrenceStart, movedAt, CancellationToken.None);
        var startsAfterRecord = await repo.GetMovedOccurrenceStartsAsync(
            SeriesKey,
            CancellationToken.None
        );

        // Assert
        emptyBeforeRecord.Should().BeEmpty("nothing is recorded on a fresh database");
        startsAfterRecord
            .Should()
            .ContainSingle("the lazy schema-ensure guard makes the store usable")
            .Which.Should()
            .Be(occurrenceStart);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "null key")]
    [DataRow("", DisplayName = "empty key")]
    [DataRow("   ", DisplayName = "whitespace-only key")]
    public async Task RecordMoveAsync_blank_key_should_throw_ArgumentException(string? seriesKey)
    {
        // Arrange (scenario i): a null/empty/whitespace-only series key must fail fast.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("blank"));
        await repo.InitializeAsync();
        var occurrenceStart = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);
        var movedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        // Act
        var act = async () =>
            await repo.RecordMoveAsync(
                seriesKey!,
                occurrenceStart,
                movedAt,
                CancellationToken.None
            );

        // Assert
        (
            await act.Should().ThrowAsync<ArgumentException>("the series key is blank")
        ).WithParameterName("seriesKey");
    }

    [TestMethod]
    public async Task Second_repository_instance_should_see_rows_recorded_by_first_instance()
    {
        // Arrange (scenario j): restart persistence over one shared in-memory database.
        // The anchor connection keeps the database alive across repository instances.
        var connectionString = NewConnectionString("restart");
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        var occurrenceStart = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.Zero);
        var movedAt = new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero);

        using (var first = new OpenClaw.Core.CoreCacheRepository(connectionString))
        {
            await first.InitializeAsync();
            await first.RecordMoveAsync(
                SeriesKey,
                occurrenceStart,
                movedAt,
                CancellationToken.None
            );
        }

        // Act: a second instance simulates a process restart over the same database.
        using var second = new OpenClaw.Core.CoreCacheRepository(connectionString);
        var starts = await second.GetMovedOccurrenceStartsAsync(SeriesKey, CancellationToken.None);

        // Assert
        starts
            .Should()
            .ContainSingle("the move recorded by the first instance must persist")
            .Which.Should()
            .Be(occurrenceStart);
    }
}
