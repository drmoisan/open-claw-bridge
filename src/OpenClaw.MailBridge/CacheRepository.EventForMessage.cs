using Microsoft.Data.Sqlite;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Message-to-event linkage resolution for <see cref="CacheRepository"/> (issue #146). Split into a
/// partial-class file so the resolution join lives beside the reader helpers without pushing the
/// main <c>CacheRepository.cs</c> over the 500-line cap. The join is a pure cache read: no
/// COM/Outlook session participates in the RPC resolution path.
/// </summary>
internal sealed partial class CacheRepository
{
    public async Task<EventDto?> GetEventForMessageAsync(
        string messageBridgeId,
        CancellationToken cancellationToken = default
    )
    {
        // Defensive decode guard. A malformed id is normally rejected upstream by the RPC handler
        // (INVALID_REQUEST); here it simply yields the clean unlinked result rather than throwing.
        if (!BridgeIdCodec.TryDecodeMessageId(messageBridgeId, out _, out _))
        {
            return null;
        }

        await using var conn = Open();
        await conn.OpenAsync(cancellationToken);

        var linkedKey = await ReadLinkedAppointmentKeyAsync(
            conn,
            messageBridgeId,
            cancellationToken
        );
        if (string.IsNullOrWhiteSpace(linkedKey))
        {
            // Ordinary mail, an absent message row, or a meeting message with no stored linkage key
            // all resolve to a clean not-linked result.
            return null;
        }

        return await ResolveEventByGlobalAppointmentIdAsync(conn, linkedKey, cancellationToken);
    }

    /// <summary>
    /// Reads the stored <c>linked_global_appointment_id</c> for a message row. Returns
    /// <see langword="null"/> when the message row is absent or the linkage key is NULL.
    /// </summary>
    private static async Task<string?> ReadLinkedAppointmentKeyAsync(
        SqliteConnection conn,
        string messageBridgeId,
        CancellationToken cancellationToken
    )
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT linked_global_appointment_id FROM messages WHERE bridge_id = $bridge_id LIMIT 1;";
        cmd.Parameters.AddWithValue("$bridge_id", messageBridgeId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return GetString(reader, "linked_global_appointment_id");
    }

    /// <summary>
    /// Resolves the newest event whose <c>global_appointment_id</c> equals the linkage key. For a
    /// recurring series multiple rows may share the key; the newest instance is chosen by
    /// <c>start_utc DESC</c>, matching <c>ListCalendarWindow</c> ordering.
    /// </summary>
    private static async Task<EventDto?> ResolveEventByGlobalAppointmentIdAsync(
        SqliteConnection conn,
        string linkedKey,
        CancellationToken cancellationToken
    )
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"
SELECT *
FROM events
WHERE global_appointment_id = $key
ORDER BY start_utc DESC
LIMIT 1;";
        cmd.Parameters.AddWithValue("$key", linkedKey);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEvent(reader) : null;
    }
}
