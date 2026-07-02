using System.Globalization;
using Microsoft.Data.Sqlite;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core;

/// <summary>
/// <see cref="ISentActionStore"/> implementation for <see cref="CoreCacheRepository"/>
/// (issue #101). Persists send-idempotency records in the <c>sent_actions</c> table using
/// the repository's per-call connection pattern. Timestamps are caller-supplied; this
/// partial has no clock dependency. A lazy once-per-instance schema-ensure guard creates
/// the <c>sent_actions</c> table before the first store operation so the store is safe on
/// databases that have not run <see cref="InitializeAsync"/> since the table was added.
/// </summary>
internal sealed partial class CoreCacheRepository : ISentActionStore
{
    private const string CreateSentActionsTableSql =
        "CREATE TABLE IF NOT EXISTS sent_actions(dedupe_key TEXT PRIMARY KEY, mailbox TEXT NOT NULL, message_id TEXT NOT NULL, action_type TEXT NOT NULL, recorded_at_utc TEXT NOT NULL);";

    private bool sentActionsSchemaEnsured;

    /// <inheritdoc />
    public async Task<bool> IsRecordedAsync(string dedupeKey, CancellationToken ct)
    {
        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureSentActionsSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sent_actions WHERE dedupe_key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", dedupeKey);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    /// <inheritdoc />
    public async Task RecordAsync(
        string dedupeKey,
        DateTimeOffset recordedAtUtc,
        CancellationToken ct
    )
    {
        var (mailbox, messageId, actionType) = ParseSentActionKey(dedupeKey);

        await using var connection = Open();
        await connection.OpenAsync(ct);
        await EnsureSentActionsSchemaAsync(connection, ct);

        var command = connection.CreateCommand();
        command.CommandText =
            @"
INSERT INTO sent_actions(dedupe_key, mailbox, message_id, action_type, recorded_at_utc)
VALUES($dedupe_key, $mailbox, $message_id, $action_type, $recorded_at_utc)
ON CONFLICT(dedupe_key) DO NOTHING;";
        command.Parameters.AddWithValue("$dedupe_key", dedupeKey);
        command.Parameters.AddWithValue("$mailbox", mailbox);
        command.Parameters.AddWithValue("$message_id", messageId);
        command.Parameters.AddWithValue("$action_type", actionType);
        command.Parameters.AddWithValue(
            "$recorded_at_utc",
            recordedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        );
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Parses the fixed <c>{mailbox}:{messageId}:{actionType}</c> key shape produced by
    /// <see cref="SentActionKey.Build"/> into its three components.
    /// </summary>
    /// <exception cref="ArgumentException">The key does not have three non-empty components.</exception>
    private static (string Mailbox, string MessageId, string ActionType) ParseSentActionKey(
        string dedupeKey
    )
    {
        var parts = string.IsNullOrWhiteSpace(dedupeKey) ? [] : dedupeKey.Split(':', 3);
        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                $"Dedupe key must have the shape {{mailbox}}:{{messageId}}:{{actionType}}; got '{dedupeKey}'.",
                nameof(dedupeKey)
            );
        }

        return (parts[0], parts[1], parts[2]);
    }

    /// <summary>
    /// Lazily ensures the <c>sent_actions</c> table exists, once per repository instance.
    /// The DDL is <c>CREATE TABLE IF NOT EXISTS</c>, so a concurrent duplicate execution
    /// is harmless; the flag only avoids repeated DDL round-trips.
    /// </summary>
    private async Task EnsureSentActionsSchemaAsync(
        SqliteConnection connection,
        CancellationToken ct
    )
    {
        if (sentActionsSchemaEnsured)
        {
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = CreateSentActionsTableSql;
        await command.ExecuteNonQueryAsync(ct);
        sentActionsSchemaEnsured = true;
    }
}
