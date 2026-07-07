using System.Globalization;
using Microsoft.Data.Sqlite;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core;

/// <summary>
/// <see cref="ISubscriptionStore"/> implementation for <see cref="CoreCacheRepository"/>
/// (issue #117). Persists Graph subscription records in the <c>graph_subscriptions</c>
/// table using the repository's per-call connection pattern. Timestamps are
/// caller-supplied; this partial has no clock dependency. A lazy once-per-instance
/// schema-ensure guard creates the table before the first store operation (the
/// <c>sent_actions</c> precedent), so the store is safe on databases that have not run
/// <see cref="InitializeAsync"/> since the table was added.
/// </summary>
internal sealed partial class CoreCacheRepository : ISubscriptionStore
{
    internal const string CreateGraphSubscriptionsTableSql =
        @"
CREATE TABLE IF NOT EXISTS graph_subscriptions(
    subscription_id TEXT PRIMARY KEY,
    resource TEXT NOT NULL,
    mailbox TEXT NOT NULL,
    client_state TEXT NOT NULL,
    expiration_utc TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);";

    private bool graphSubscriptionsSchemaEnsured;

    /// <inheritdoc />
    public async Task<GraphSubscriptionRecord?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken ct
    )
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureGraphSubscriptionsSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT * FROM graph_subscriptions WHERE subscription_id = $subscription_id LIMIT 1;";
        command.Parameters.AddWithValue("$subscription_id", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSubscription(reader) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphSubscriptionRecord>> ListSubscriptionsAsync(
        CancellationToken ct
    )
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureGraphSubscriptionsSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM graph_subscriptions ORDER BY subscription_id;";
        var rows = new List<GraphSubscriptionRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadSubscription(reader));
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task UpsertSubscriptionAsync(
        GraphSubscriptionRecord record,
        DateTimeOffset nowUtc,
        CancellationToken ct
    )
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureGraphSubscriptionsSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO graph_subscriptions(
    subscription_id, resource, mailbox, client_state, expiration_utc, status,
    created_at_utc, updated_at_utc)
VALUES(
    $subscription_id, $resource, $mailbox, $client_state, $expiration_utc, $status,
    $now_utc, $now_utc)
ON CONFLICT(subscription_id) DO UPDATE SET
    resource = excluded.resource,
    mailbox = excluded.mailbox,
    client_state = excluded.client_state,
    expiration_utc = excluded.expiration_utc,
    status = excluded.status,
    updated_at_utc = excluded.updated_at_utc;";
        command.Parameters.AddWithValue("$subscription_id", record.SubscriptionId);
        command.Parameters.AddWithValue("$resource", record.Resource);
        command.Parameters.AddWithValue("$mailbox", record.Mailbox);
        command.Parameters.AddWithValue("$client_state", record.ClientState);
        command.Parameters.AddWithValue("$expiration_utc", RenderUtc(record.ExpirationUtc));
        command.Parameters.AddWithValue("$status", record.Status);
        command.Parameters.AddWithValue("$now_utc", RenderUtc(nowUtc));
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateSubscriptionStatusAsync(
        string subscriptionId,
        string status,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct
    )
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureGraphSubscriptionsSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            @"
UPDATE graph_subscriptions
SET status = $status, updated_at_utc = $updated_at_utc
WHERE subscription_id = $subscription_id;";
        command.Parameters.AddWithValue("$subscription_id", subscriptionId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$updated_at_utc", RenderUtc(updatedAtUtc));
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureGraphSubscriptionsSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM graph_subscriptions WHERE subscription_id = $subscription_id;";
        command.Parameters.AddWithValue("$subscription_id", subscriptionId);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Round-trip ("O") invariant UTC rendering shared by this partial.</summary>
    private static string RenderUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static GraphSubscriptionRecord ReadSubscription(SqliteDataReader reader)
    {
        var subscriptionId = ReadString(reader, "subscription_id")!;
        var expirationUtc =
            ReadDateTimeOffset(reader, "expiration_utc")
            ?? throw new InvalidOperationException(
                $"Graph subscription '{subscriptionId}' has an unparseable 'expiration_utc' value; "
                    + "the column is NOT NULL and always written via RenderUtc, so the stored row is corrupt."
            );
        return new(
            subscriptionId,
            ReadString(reader, "resource")!,
            ReadString(reader, "mailbox")!,
            ReadString(reader, "client_state")!,
            expirationUtc,
            ReadString(reader, "status")!
        );
    }

    /// <summary>
    /// Lazily ensures the <c>graph_subscriptions</c> table exists, once per repository
    /// instance. The DDL is <c>CREATE TABLE IF NOT EXISTS</c>, so a concurrent
    /// duplicate execution is harmless; the flag only avoids repeated DDL round-trips.
    /// </summary>
    private async Task EnsureGraphSubscriptionsSchemaAsync(
        SqliteConnection connection,
        CancellationToken ct
    )
    {
        if (graphSubscriptionsSchemaEnsured)
        {
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = CreateGraphSubscriptionsTableSql;
        await command.ExecuteNonQueryAsync(ct);
        graphSubscriptionsSchemaEnsured = true;
    }
}
