using System.Globalization;
using Microsoft.Data.Sqlite;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core;

/// <summary>
/// <see cref="IDeltaLinkStore"/> implementation for <see cref="CoreCacheRepository"/>
/// (issue #117). Persists per-mailbox <c>@odata.deltaLink</c> values verbatim in the
/// <c>graph_delta_links</c> table using the repository's per-call connection pattern.
/// Timestamps are caller-supplied; this partial has no clock dependency. A lazy
/// once-per-instance schema-ensure guard creates the table before the first store
/// operation (the <c>sent_actions</c> precedent).
/// </summary>
internal sealed partial class CoreCacheRepository : IDeltaLinkStore
{
    internal const string CreateGraphDeltaLinksTableSql =
        @"
CREATE TABLE IF NOT EXISTS graph_delta_links(
    mailbox TEXT PRIMARY KEY,
    delta_link TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);";

    private bool graphDeltaLinksSchemaEnsured;

    /// <inheritdoc />
    public async Task<string?> GetDeltaLinkAsync(string mailbox, CancellationToken ct)
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureGraphDeltaLinksSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT delta_link FROM graph_delta_links WHERE mailbox = $mailbox LIMIT 1;";
        command.Parameters.AddWithValue("$mailbox", mailbox);
        return (string?)await command.ExecuteScalarAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetDeltaLinkAsync(
        string mailbox,
        string deltaLink,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct
    )
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureGraphDeltaLinksSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO graph_delta_links(mailbox, delta_link, updated_at_utc)
VALUES($mailbox, $delta_link, $updated_at_utc)
ON CONFLICT(mailbox) DO UPDATE SET
    delta_link = excluded.delta_link,
    updated_at_utc = excluded.updated_at_utc;";
        command.Parameters.AddWithValue("$mailbox", mailbox);
        command.Parameters.AddWithValue("$delta_link", deltaLink);
        command.Parameters.AddWithValue(
            "$updated_at_utc",
            updatedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        );
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Lazily ensures the <c>graph_delta_links</c> table exists, once per repository
    /// instance. The DDL is <c>CREATE TABLE IF NOT EXISTS</c>, so a concurrent
    /// duplicate execution is harmless; the flag only avoids repeated DDL round-trips.
    /// </summary>
    private async Task EnsureGraphDeltaLinksSchemaAsync(
        SqliteConnection connection,
        CancellationToken ct
    )
    {
        if (graphDeltaLinksSchemaEnsured)
        {
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = CreateGraphDeltaLinksTableSql;
        await command.ExecuteNonQueryAsync(ct);
        graphDeltaLinksSchemaEnsured = true;
    }
}
