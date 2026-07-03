using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for the <see cref="ISubscriptionStore"/> implementation on
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> (issue #117, AC-2): upsert/get
/// round-trip with expiration precision, status update, delete, schema-ensure
/// idempotency, and restart survival through a fresh repository instance on the same
/// connection string. Uses in-memory shared-cache SQLite so no temp files are created.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositorySubscriptionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    private static string NewConnectionString(string label) =>
        $"Data Source=core-sub-{label}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private static GraphSubscriptionRecord Record(
        string id = "sub-1",
        string status = SubscriptionStatus.Active
    ) =>
        new(
            id,
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            "client-state-abc123",
            new DateTimeOffset(2026, 7, 10, 8, 30, 15, 123, TimeSpan.Zero).AddTicks(4567),
            status
        );

    [TestMethod]
    public async Task Upsert_then_get_round_trips_every_field_including_expiration_precision()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("roundtrip"));
        var record = Record();

        // Act
        await repo.UpsertSubscriptionAsync(record, Now, CancellationToken.None);
        var stored = await repo.GetSubscriptionAsync("sub-1", CancellationToken.None);

        // Assert: the "O" round-trip form preserves sub-second expiration precision.
        stored.Should().Be(record, "every field including tick-level expiration round-trips");
    }

    [TestMethod]
    public async Task Get_unknown_subscription_returns_null()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("unknown"));

        // Act
        var stored = await repo.GetSubscriptionAsync("sub-missing", CancellationToken.None);

        // Assert
        stored.Should().BeNull("no subscription has been stored");
    }

    [TestMethod]
    public async Task Second_upsert_with_same_id_replaces_the_row()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("upsert"));
        await repo.UpsertSubscriptionAsync(Record(), Now, CancellationToken.None);
        var renewed = Record() with
        {
            ExpirationUtc = new DateTimeOffset(2026, 7, 17, 8, 0, 0, TimeSpan.Zero),
            ClientState = "client-state-rotated",
        };

        // Act
        await repo.UpsertSubscriptionAsync(renewed, Now.AddHours(1), CancellationToken.None);
        var stored = await repo.GetSubscriptionAsync("sub-1", CancellationToken.None);
        var all = await repo.ListSubscriptionsAsync(CancellationToken.None);

        // Assert
        stored.Should().Be(renewed, "the upsert replaces field values for the same id");
        all.Should().ContainSingle("the upsert must not create a second row");
    }

    [TestMethod]
    public async Task UpdateStatus_marks_the_record_reauthorize_failed()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("status"));
        await repo.UpsertSubscriptionAsync(Record(), Now, CancellationToken.None);

        // Act
        await repo.UpdateSubscriptionStatusAsync(
            "sub-1",
            SubscriptionStatus.ReauthorizeFailed,
            Now.AddMinutes(5),
            CancellationToken.None
        );
        var stored = await repo.GetSubscriptionAsync("sub-1", CancellationToken.None);

        // Assert
        stored!.Status.Should().Be("reauthorize_failed");
        stored
            .Should()
            .Be(
                Record(status: SubscriptionStatus.ReauthorizeFailed),
                "only the status changes; every other field is preserved"
            );
    }

    [TestMethod]
    public async Task Delete_removes_the_row_and_is_idempotent()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("delete"));
        await repo.UpsertSubscriptionAsync(Record(), Now, CancellationToken.None);

        // Act
        await repo.DeleteSubscriptionAsync("sub-1", CancellationToken.None);
        var afterDelete = await repo.GetSubscriptionAsync("sub-1", CancellationToken.None);
        var deleteAgain = async () =>
            await repo.DeleteSubscriptionAsync("sub-1", CancellationToken.None);

        // Assert
        afterDelete.Should().BeNull("delete removes the row");
        await deleteAgain.Should().NotThrowAsync("deleting an absent row is a no-op");
    }

    [TestMethod]
    public async Task Schema_ensure_is_idempotent_across_two_repository_instances()
    {
        // Arrange: two repository instances against the same database each run their
        // own lazy schema-ensure; the second must not fail on the existing table.
        var connectionString = NewConnectionString("idempotent");
        using var first = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await first.UpsertSubscriptionAsync(Record(), Now, CancellationToken.None);

        using var second = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act
        var act = async () => await second.ListSubscriptionsAsync(CancellationToken.None);

        // Assert
        (await act.Should().NotThrowAsync("CREATE TABLE IF NOT EXISTS is idempotent"))
            .Which.Should()
            .ContainSingle();
    }

    [TestMethod]
    public async Task InitializeAsync_twice_should_not_throw_and_table_should_exist()
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
        var command = check.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'graph_subscriptions';";
        Convert
            .ToInt32(await command.ExecuteScalarAsync())
            .Should()
            .Be(1, "InitializeAsync must create the graph_subscriptions table");
    }

    [TestMethod]
    public async Task Subscription_state_survives_restart_via_a_fresh_repository_instance()
    {
        // Arrange: an anchor connection keeps the shared in-memory database alive
        // across repository instances, simulating durable storage over a restart.
        var connectionString = NewConnectionString("restart");
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();

        var record = Record();
        using (var writer = new OpenClaw.Core.CoreCacheRepository(connectionString))
        {
            await writer.UpsertSubscriptionAsync(record, Now, CancellationToken.None);
        }

        // Act: a fresh instance (fresh lazy-ensure state) reads the same database.
        using var reader = new OpenClaw.Core.CoreCacheRepository(connectionString);
        var stored = await reader.GetSubscriptionAsync("sub-1", CancellationToken.None);

        // Assert
        stored.Should().Be(record, "subscription state survives a repository restart");
    }
}
