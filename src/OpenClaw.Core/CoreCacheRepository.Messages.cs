using Microsoft.Data.Sqlite;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core;

/// <summary>
/// Message-persistence members for <see cref="CoreCacheRepository"/>. Split into this partial-class
/// file (issue #73 RF-2) so the previously over-cap <c>CoreCacheRepository.cs</c> falls under the
/// 500-line limit, following the partial-split convention established by
/// <c>CoreCacheRepository.Schema.cs</c>. The move is behavior-preserving: same partial class, same
/// member signatures and bodies. The shared reader helpers (<c>ReadString</c>,
/// <c>ReadDateTimeOffset</c>, <c>ReadNullableInt</c>, <c>ReadBoolean</c>, <c>ToDbValue</c>,
/// <c>CountAsync</c>) and <c>GetCountsAsync</c> remain in the base file and are reached via the
/// shared partial class.
/// </summary>
internal sealed partial class CoreCacheRepository
{
    public async Task<IReadOnlyList<MessageDto>> ListMessagesAsync(
        string kind,
        DateTimeOffset? sinceUtc,
        int limit
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            @"
SELECT *
FROM messages
WHERE ($kind = 'all'
    OR ($kind = 'meeting' AND item_kind = 'meeting')
    OR ($kind = 'mail' AND item_kind <> 'meeting'))
  AND ($since_utc IS NULL OR received_utc IS NULL OR received_utc >= $since_utc)
ORDER BY COALESCE(received_utc, sent_utc, observed_at_utc) DESC, bridge_id ASC
LIMIT $limit;";
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue(
            "$since_utc",
            sinceUtc is null ? DBNull.Value : sinceUtc.Value.UtcDateTime.ToString("O")
        );
        command.Parameters.AddWithValue("$limit", limit);
        return await ReadMessagesAsync(command);
    }

    public async Task<MessageDto?> GetMessageAsync(string bridgeId)
    {
        await using var connection = Open();
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM messages WHERE bridge_id = $bridge_id LIMIT 1;";
        command.Parameters.AddWithValue("$bridge_id", bridgeId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadMessage(reader) : null;
    }

    public async Task UpsertMessagesAsync(
        IReadOnlyList<MessageDto> messages,
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        await using var connection = Open();
        await connection.OpenAsync();
        foreach (var message in messages)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                @"
INSERT INTO messages(
    bridge_id, item_kind, subject, received_utc, sent_utc, importance, sensitivity, unread,
    has_attachments, message_class, sender_name, sender_email, to_json, cc_json, body_preview,
    protected_fields_available, is_redacted, bridge_mode, cache_stale, stale_reason,
    adapter_request_id, observed_at_utc,
    sender_email_resolved, from_email_address, conversation_id, meeting_message_type)
VALUES(
    $bridge_id, $item_kind, $subject, $received_utc, $sent_utc, $importance, $sensitivity, $unread,
    $has_attachments, $message_class, $sender_name, $sender_email, $to_json, $cc_json, $body_preview,
    $protected_fields_available, $is_redacted, $bridge_mode, $cache_stale, $stale_reason,
    $adapter_request_id, $observed_at_utc,
    $sender_email_resolved, $from_email_address, $conversation_id, $meeting_message_type)
ON CONFLICT(bridge_id) DO UPDATE SET
    item_kind = excluded.item_kind,
    subject = excluded.subject,
    received_utc = excluded.received_utc,
    sent_utc = excluded.sent_utc,
    importance = excluded.importance,
    sensitivity = excluded.sensitivity,
    unread = excluded.unread,
    has_attachments = excluded.has_attachments,
    message_class = excluded.message_class,
    sender_name = excluded.sender_name,
    sender_email = excluded.sender_email,
    to_json = excluded.to_json,
    cc_json = excluded.cc_json,
    body_preview = excluded.body_preview,
    protected_fields_available = excluded.protected_fields_available,
    is_redacted = excluded.is_redacted,
    bridge_mode = excluded.bridge_mode,
    cache_stale = excluded.cache_stale,
    stale_reason = excluded.stale_reason,
    adapter_request_id = excluded.adapter_request_id,
    observed_at_utc = excluded.observed_at_utc,
    sender_email_resolved = excluded.sender_email_resolved,
    from_email_address = excluded.from_email_address,
    conversation_id = excluded.conversation_id,
    meeting_message_type = excluded.meeting_message_type;";
            AddMessageParameters(command, message, bridgeStatus, requestId, observedAtUtc);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static void AddMessageParameters(
        SqliteCommand command,
        MessageDto message,
        BridgeStatusDto bridgeStatus,
        string requestId,
        DateTimeOffset observedAtUtc
    )
    {
        command.Parameters.AddWithValue("$bridge_id", message.BridgeId);
        command.Parameters.AddWithValue("$item_kind", message.ItemKind);
        command.Parameters.AddWithValue("$subject", (object?)message.Subject ?? DBNull.Value);
        command.Parameters.AddWithValue("$received_utc", ToDbValue(message.ReceivedUtc));
        command.Parameters.AddWithValue("$sent_utc", ToDbValue(message.SentUtc));
        command.Parameters.AddWithValue("$importance", ToDbValue(message.Importance));
        command.Parameters.AddWithValue("$sensitivity", ToDbValue(message.Sensitivity));
        command.Parameters.AddWithValue("$unread", message.Unread ? 1 : 0);
        command.Parameters.AddWithValue("$has_attachments", message.HasAttachments ? 1 : 0);
        command.Parameters.AddWithValue(
            "$message_class",
            (object?)message.MessageClass ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$sender_name",
            (object?)message.SenderName ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$sender_email",
            (object?)message.SenderEmail ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$to_json", (object?)message.ToJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$cc_json", (object?)message.CcJson ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$body_preview",
            (object?)message.BodyPreview ?? DBNull.Value
        );
        command.Parameters.AddWithValue(
            "$protected_fields_available",
            message.ProtectedFieldsAvailable ? 1 : 0
        );
        command.Parameters.AddWithValue("$is_redacted", message.IsRedacted ? 1 : 0);
        command.Parameters.AddWithValue("$bridge_mode", bridgeStatus.Mode);
        command.Parameters.AddWithValue("$cache_stale", bridgeStatus.CacheStale ? 1 : 0);
        command.Parameters.AddWithValue(
            "$stale_reason",
            (object?)bridgeStatus.StaleReason ?? DBNull.Value
        );
        command.Parameters.AddWithValue("$adapter_request_id", requestId);
        command.Parameters.AddWithValue(
            "$observed_at_utc",
            observedAtUtc.UtcDateTime.ToString("O")
        );
        AddMessageResolvedFieldParameters(command, message);
    }

    private static async Task<IReadOnlyList<MessageDto>> ReadMessagesAsync(SqliteCommand command)
    {
        var rows = new List<MessageDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadMessage(reader));
        }

        return rows;
    }

    private static MessageDto ReadMessage(SqliteDataReader reader) =>
        new(
            ReadString(reader, "bridge_id")!,
            ReadString(reader, "item_kind")!,
            ReadString(reader, "subject"),
            ReadDateTimeOffset(reader, "received_utc"),
            ReadDateTimeOffset(reader, "sent_utc"),
            ReadNullableInt(reader, "importance"),
            ReadNullableInt(reader, "sensitivity"),
            ReadBoolean(reader, "unread"),
            ReadBoolean(reader, "has_attachments"),
            ReadString(reader, "message_class"),
            ReadString(reader, "sender_name"),
            ReadString(reader, "sender_email"),
            ReadString(reader, "to_json"),
            ReadString(reader, "cc_json"),
            ReadString(reader, "body_preview"),
            ReadBoolean(reader, "protected_fields_available"),
            ReadBoolean(reader, "is_redacted"),
            SenderEmailResolved: ReadString(reader, "sender_email_resolved"),
            FromEmailAddress: ReadString(reader, "from_email_address"),
            ConversationId: ReadString(reader, "conversation_id"),
            MeetingMessageType: ReadNullableInt(reader, "meeting_message_type")
        );
}
