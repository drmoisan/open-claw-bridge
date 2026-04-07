using Microsoft.Data.Sqlite;

namespace OpenClaw.MailBridge;

internal interface IScanStateRepository
{
    Task InitializeAsync();
    Task TouchScanStateAsync(string key, DateTimeOffset value);
    Task<DateTimeOffset?> GetScanStateAsync(string key);
}

/// <summary>
/// Persists cached bridge metadata in a local SQLite database under the user's profile.
/// </summary>
internal sealed class CacheRepository : IScanStateRepository
{
    private readonly string _dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenClaw",
        "MailBridge",
        "cache.db"
    );

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    public async Task InitializeAsync()
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var sql =
            @"
CREATE TABLE IF NOT EXISTS messages(bridge_id TEXT PRIMARY KEY,entry_id TEXT NOT NULL,store_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,received_utc TEXT NULL,sent_utc TEXT NULL,importance INTEGER NULL,sensitivity INTEGER NULL,unread INTEGER NOT NULL,has_attachments INTEGER NOT NULL,message_class TEXT NULL,sender_name TEXT NULL,sender_email TEXT NULL,to_json TEXT NULL,cc_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_seen_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS events(bridge_id TEXT PRIMARY KEY,entry_id TEXT NULL,store_id TEXT NULL,global_appointment_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,start_utc TEXT NOT NULL,end_utc TEXT NOT NULL,location TEXT NULL,busy_status INTEGER NULL,meeting_status INTEGER NULL,is_recurring INTEGER NOT NULL,sensitivity INTEGER NULL,organizer TEXT NULL,required_attendees_json TEXT NULL,optional_attendees_json TEXT NULL,resources_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_modified_utc TEXT NULL,last_seen_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS scan_state(key TEXT PRIMARY KEY,value TEXT NOT NULL);";
        await new SqliteCommand(sql, conn).ExecuteNonQueryAsync();
    }

    public async Task TouchScanStateAsync(string key, DateTimeOffset value)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO scan_state(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value.UtcDateTime.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<DateTimeOffset?> GetScanStateAsync(string key)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM scan_state WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);

        var val = (string?)await cmd.ExecuteScalarAsync();
        return DateTimeOffset.TryParse(val, out var parsed) ? parsed : null;
    }
}
