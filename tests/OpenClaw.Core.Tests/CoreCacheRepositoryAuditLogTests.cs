using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for the <see cref="IActionAuditLog"/> implementation on
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> (issue #107, AC1): write/read round-trip,
/// query ordering (<c>recorded_at_utc DESC, id DESC</c>), UTC normalization, restart
/// survival, migration idempotency, the lazy schema-ensure guard, and the required-field
/// guards. Uses in-memory shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositoryAuditLogTests
{
    private static string NewConnectionString(string label) =>
        $"Data Source=core-al-{label}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private static ActionAuditRecord NewRecord(
        string messageId,
        DateTimeOffset recordedAt,
        string resultCode = ActionAuditResultCode.Sent,
        string correlationId = "11111111-1111-1111-1111-111111111111"
    ) =>
        new(
            Mailbox: "owner@contoso.com",
            MessageId: messageId,
            EventId: null,
            ActionType: SentActionKey.ProposalReply,
            ActingFlags: "SendEnabled=True;CalendarWriteEnabled=False",
            CorrelationId: correlationId,
            ResultCode: resultCode,
            ErrorDetail: null,
            OriginalStartUtc: null,
            OriginalEndUtc: null,
            NewStartUtc: null,
            NewEndUtc: null,
            RecordedAtUtc: recordedAt
        );

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    [TestMethod]
    public async Task GetByMessageIdAsync_should_return_empty_for_unknown_message()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("unknown"));
        await repo.InitializeAsync();

        // Act
        var records = await repo.GetByMessageIdAsync("msg-none", CancellationToken.None);

        // Assert
        records.Should().BeEmpty("no audit record has been written for the message");
    }

    [TestMethod]
    public async Task RecordAsync_then_GetByMessageIdAsync_should_round_trip_all_fields()
    {
        // Arrange: every field populated, including the Stage 2 time columns and error detail.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("full"));
        await repo.InitializeAsync();
        var record = new ActionAuditRecord(
            Mailbox: "owner@contoso.com",
            MessageId: "msg-full",
            EventId: "evt-9",
            ActionType: SentActionKey.ProposalReply,
            ActingFlags: "SendEnabled=True;CalendarWriteEnabled=True",
            CorrelationId: "22222222-2222-2222-2222-222222222222",
            ResultCode: ActionAuditResultCode.SendFailed,
            ErrorDetail: "InvalidOperationException: send rejected",
            OriginalStartUtc: new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
            OriginalEndUtc: new DateTimeOffset(2026, 7, 1, 9, 30, 0, TimeSpan.Zero),
            NewStartUtc: new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero),
            NewEndUtc: new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero),
            RecordedAtUtc: new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero)
        );

        // Act
        await repo.RecordAsync(record, CancellationToken.None);
        var records = await repo.GetByMessageIdAsync("msg-full", CancellationToken.None);

        // Assert
        records.Should().ContainSingle("one record was written for the message");
        records[0].Should().Be(record, "every field must survive the persistence round-trip");
    }

    [TestMethod]
    public async Task RecordAsync_with_null_optionals_should_round_trip_nulls()
    {
        // Arrange: the Stage 0 shape — all optional fields null.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("nulls"));
        await repo.InitializeAsync();
        var record = NewRecord(
            "msg-null",
            new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero)
        );

        // Act
        await repo.RecordAsync(record, CancellationToken.None);
        var records = await repo.GetByMessageIdAsync("msg-null", CancellationToken.None);

        // Assert
        records.Should().ContainSingle();
        records[0].EventId.Should().BeNull("a null event id must read back as null");
        records[0].ErrorDetail.Should().BeNull("a null error detail must read back as null");
        records[0].OriginalStartUtc.Should().BeNull();
        records[0].OriginalEndUtc.Should().BeNull();
        records[0].NewStartUtc.Should().BeNull();
        records[0].NewEndUtc.Should().BeNull();
    }

    [TestMethod]
    public async Task GetByMessageIdAsync_should_order_most_recent_first()
    {
        // Arrange: insert the older record second so insertion order cannot mask the sort.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("order"));
        await repo.InitializeAsync();
        var older = NewRecord(
            "msg-ord",
            new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
            ActionAuditResultCode.SendDisabled
        );
        var newer = NewRecord(
            "msg-ord",
            new DateTimeOffset(2026, 7, 2, 11, 0, 0, TimeSpan.Zero),
            ActionAuditResultCode.Sent
        );

        // Act
        await repo.RecordAsync(newer, CancellationToken.None);
        await repo.RecordAsync(older, CancellationToken.None);
        var records = await repo.GetByMessageIdAsync("msg-ord", CancellationToken.None);

        // Assert
        records
            .Select(r => r.ResultCode)
            .Should()
            .ContainInOrder(ActionAuditResultCode.Sent, ActionAuditResultCode.SendDisabled);
        records.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GetByMessageIdAsync_identical_timestamps_should_tie_break_by_id_desc()
    {
        // Arrange: a fixed test clock produces identical timestamps; the id tie-break keeps
        // ordering deterministic (later insert first).
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("tie"));
        await repo.InitializeAsync();
        var at = new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero);
        var first = NewRecord("msg-tie", at, ActionAuditResultCode.DedupeSkipped);
        var second = NewRecord("msg-tie", at, ActionAuditResultCode.Sent);

        // Act
        await repo.RecordAsync(first, CancellationToken.None);
        await repo.RecordAsync(second, CancellationToken.None);
        var records = await repo.GetByMessageIdAsync("msg-tie", CancellationToken.None);

        // Assert
        records
            .Select(r => r.ResultCode)
            .Should()
            .ContainInOrder(ActionAuditResultCode.Sent, ActionAuditResultCode.DedupeSkipped);
    }

    [TestMethod]
    public async Task RecordAsync_non_utc_offset_should_read_back_as_equivalent_utc_instant()
    {
        // Arrange: 12:30 at +02:00 is the instant 10:30Z.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("offset"));
        await repo.InitializeAsync();
        var record = NewRecord(
            "msg-off",
            new DateTimeOffset(2026, 7, 2, 12, 30, 0, TimeSpan.FromHours(2))
        );

        // Act
        await repo.RecordAsync(record, CancellationToken.None);
        var records = await repo.GetByMessageIdAsync("msg-off", CancellationToken.None);

        // Assert
        records.Should().ContainSingle();
        records[0]
            .RecordedAtUtc.Offset.Should()
            .Be(TimeSpan.Zero, "the store normalizes timestamps to UTC");
        records[0]
            .RecordedAtUtc.Should()
            .Be(
                new DateTimeOffset(2026, 7, 2, 10, 30, 0, TimeSpan.Zero),
                "the instant must be preserved through UTC normalization"
            );
    }

    [TestMethod]
    public async Task Second_repository_instance_should_read_records_written_by_the_first()
    {
        // Arrange: an external anchor keeps the shared in-memory database alive across
        // repository instances, simulating restart survival on the same database.
        var connectionString = NewConnectionString("restart");
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        var record = NewRecord(
            "msg-restart",
            new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.Zero)
        );

        using (var firstRepo = new OpenClaw.Core.CoreCacheRepository(connectionString))
        {
            await firstRepo.InitializeAsync();
            await firstRepo.RecordAsync(record, CancellationToken.None);
        }

        // Act
        using var secondRepo = new OpenClaw.Core.CoreCacheRepository(connectionString);
        var records = await secondRepo.GetByMessageIdAsync("msg-restart", CancellationToken.None);

        // Assert
        records.Should().ContainSingle("records must survive a repository restart");
        records[0].Should().Be(record);
    }

    [TestMethod]
    public async Task InitializeAsync_twice_should_not_throw_and_audit_log_should_exist()
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
        (await TableExistsAsync(check, "audit_log"))
            .Should()
            .BeTrue("InitializeAsync must create the audit_log table");
    }

    [TestMethod]
    public async Task InitializeAsync_should_add_audit_log_to_pre_existing_database()
    {
        // Arrange: seed an existing database that has a current table but no audit_log
        // (the pre-#107 shape). The anchor connection keeps the in-memory database alive.
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
        (await TableExistsAsync(anchor, "audit_log"))
            .Should()
            .BeFalse("the seeded database must not have audit_log before the upgrade");

        using var repo = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act
        await repo.InitializeAsync();

        // Assert
        (await TableExistsAsync(anchor, "audit_log"))
            .Should()
            .BeTrue("InitializeAsync must add audit_log to a pre-existing database");
    }

    [TestMethod]
    public async Task Store_methods_should_work_on_fresh_database_without_InitializeAsync()
    {
        // Arrange: no InitializeAsync call; the lazy schema-ensure guard must create the table.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("lazy"));
        var record = NewRecord("msg-lazy", new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero));

        // Act
        var emptyBeforeWrite = await repo.GetByMessageIdAsync("msg-lazy", CancellationToken.None);
        await repo.RecordAsync(record, CancellationToken.None);
        var recordsAfterWrite = await repo.GetByMessageIdAsync("msg-lazy", CancellationToken.None);

        // Assert
        emptyBeforeWrite.Should().BeEmpty("nothing is recorded on a fresh database");
        recordsAfterWrite
            .Should()
            .ContainSingle("the lazy schema-ensure guard makes the store usable");
    }

    [TestMethod]
    public async Task RecordAsync_then_GetByMessageIdAsync_should_round_trip_cloudsync_event_unchanged()
    {
        // Arrange: a CloudSync subscription-created event using the reused MessageId/ActingFlags
        // fields (spec.md decision 1) rather than a new schema/interface shape — proves the
        // existing audit_log schema and IActionAuditLog interface require no change for
        // CloudSync event types.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("cloudsync"));
        await repo.InitializeAsync();
        var record = new ActionAuditRecord(
            Mailbox: "owner@contoso.com",
            MessageId: "sub-123456",
            EventId: null,
            ActionType: CloudSyncActivityType.SubscriptionCreated,
            ActingFlags: CloudSyncActingFlags.NotApplicable,
            CorrelationId: "33333333-3333-3333-3333-333333333333",
            ResultCode: CloudSyncActivityResultCode.Success,
            ErrorDetail: null,
            OriginalStartUtc: null,
            OriginalEndUtc: null,
            NewStartUtc: null,
            NewEndUtc: null,
            RecordedAtUtc: new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero)
        );

        // Act
        await repo.RecordAsync(record, CancellationToken.None);
        var records = await repo.GetByMessageIdAsync("sub-123456", CancellationToken.None);

        // Assert
        records.Should().ContainSingle("one CloudSync audit record was written for the subscription id");
        records[0].ActingFlags.Should().Be(CloudSyncActingFlags.NotApplicable);
        records[0].ActionType.Should().Be(CloudSyncActivityType.SubscriptionCreated);
    }

    [TestMethod]
    [DataRow("Mailbox", "", DisplayName = "empty Mailbox")]
    [DataRow("Mailbox", " ", DisplayName = "whitespace Mailbox")]
    [DataRow("MessageId", "", DisplayName = "empty MessageId")]
    [DataRow("MessageId", " ", DisplayName = "whitespace MessageId")]
    [DataRow("ActionType", "", DisplayName = "empty ActionType")]
    [DataRow("ActionType", " ", DisplayName = "whitespace ActionType")]
    [DataRow("ActingFlags", "", DisplayName = "empty ActingFlags")]
    [DataRow("ActingFlags", " ", DisplayName = "whitespace ActingFlags")]
    [DataRow("CorrelationId", "", DisplayName = "empty CorrelationId")]
    [DataRow("CorrelationId", " ", DisplayName = "whitespace CorrelationId")]
    [DataRow("ResultCode", "", DisplayName = "empty ResultCode")]
    [DataRow("ResultCode", " ", DisplayName = "whitespace ResultCode")]
    public async Task RecordAsync_empty_required_field_should_throw_ArgumentException(
        string field,
        string value
    )
    {
        // Arrange: each required field must fail fast instead of persisting an unusable row.
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("guards"));
        await repo.InitializeAsync();
        var valid = NewRecord("msg-guard", new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero));
        var record = field switch
        {
            "Mailbox" => valid with { Mailbox = value },
            "MessageId" => valid with { MessageId = value },
            "ActionType" => valid with { ActionType = value },
            "ActingFlags" => valid with { ActingFlags = value },
            "CorrelationId" => valid with { CorrelationId = value },
            "ResultCode" => valid with { ResultCode = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        // Act
        var act = async () => await repo.RecordAsync(record, CancellationToken.None);

        // Assert
        (
            await act.Should().ThrowAsync<ArgumentException>("the required field is blank")
        ).WithParameterName(field);
    }
}
