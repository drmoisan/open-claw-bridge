using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var configPath = GetArg(args, "--config") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw", "MailBridge", "bridge.settings.json");
        var settings = LoadSettings(configPath);
        var errors = BridgeSettingsValidator.Validate(settings);
        if (errors.Count > 0)
        {
            Console.Error.WriteLine(string.Join(";", errors));
            return 2;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(new BridgeStateStore(settings));
        builder.Services.AddSingleton<CacheRepository>();
        builder.Services.AddSingleton<OutlookStaExecutor>();
        builder.Services.AddSingleton<OutlookScanner>();
        builder.Services.AddHostedService<ScanWorker>();
        builder.Services.AddHostedService<PipeRpcWorker>();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }

    private static BridgeSettings LoadSettings(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, JsonSerializer.Serialize(BridgeSettings.Default, new JsonSerializerOptions { WriteIndented = true }));
            return BridgeSettings.Default;
        }

        return JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path)) ?? BridgeSettings.Default;
    }

    private static string? GetArg(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
        return null;
    }
}

internal sealed class BridgeStateStore
{
    public BridgeStateStore(BridgeSettings settings) { Mode = settings.Mode; State = BridgeState.starting; }
    public BridgeState State { get; private set; }
    public string Mode { get; }
    public bool OutlookConnected { get; set; }
    public bool CacheStale { get; set; }
    public string? StaleReason { get; set; }
    public DateTimeOffset? LastInboxScanUtc { get; set; }
    public DateTimeOffset? LastCalendarScanUtc { get; set; }
    public void SetState(BridgeState s) => State = s;
}

internal sealed class OutlookStaExecutor : IDisposable
{
    private readonly BlockingCollection<(Func<object?> work, TaskCompletionSource<object?> tcs)> _queue = new();
    private readonly Thread _thread;

    public OutlookStaExecutor()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Outlook-STA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public async Task<T> InvokeAsync<T>(Func<T> operation)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add((() => operation()!, tcs));
        var value = await tcs.Task.ConfigureAwait(false);
        return (T)value!;
    }

    private void Run()
    {
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            try { item.tcs.SetResult(item.work()); }
            catch (Exception ex) { item.tcs.SetException(ex); }
        }
    }

    public void Dispose() => _queue.CompleteAdding();
}

internal sealed class OutlookScanner(BridgeSettings settings, BridgeStateStore state, ILogger<OutlookScanner> logger)
{
    private object? _outlookApp;

    public async Task ScanAsync(CacheRepository repo)
    {
        try
        {
            await EnsureOutlookAsync();
            state.OutlookConnected = _outlookApp is not null;
            if (_outlookApp is null) return;
            state.SetState(BridgeState.ready);
            await repo.TouchScanStateAsync("last_inbox_scan_utc", DateTimeOffset.UtcNow);
            await repo.TouchScanStateAsync("last_calendar_scan_utc", DateTimeOffset.UtcNow);
            state.LastInboxScanUtc = DateTimeOffset.UtcNow;
            state.LastCalendarScanUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            state.CacheStale = true;
            state.StaleReason = "scan_failure";
            state.SetState(BridgeState.degraded);
            logger.LogError("Scan failed: {Message}", ex.Message);
        }
    }

    private Task EnsureOutlookAsync()
    {
        if (_outlookApp is not null) return Task.CompletedTask;
        var outlookRunning = Process.GetProcessesByName("OUTLOOK").Length > 0;
        if (outlookRunning)
        {
            _outlookApp = ComActiveObject.TryGet("Outlook.Application");
            if (_outlookApp is null)
            {
                state.SetState(BridgeState.degraded);
            }
            return Task.CompletedTask;
        }

        if (!settings.AutostartOutlook)
        {
            state.SetState(BridgeState.waiting_for_outlook);
            return Task.CompletedTask;
        }

        var t = Type.GetTypeFromProgID("Outlook.Application", false) ?? throw new InvalidOperationException("Outlook COM unavailable");
        var app = Activator.CreateInstance(t) ?? throw new InvalidOperationException("Outlook activation failed");
        var ns = t.InvokeMember("GetNamespace", BindingFlags.InvokeMethod, null, app, ["MAPI"]);
        ns!.GetType().InvokeMember("Logon", BindingFlags.InvokeMethod, null, ns, ["", "", Type.Missing, Type.Missing]);
        _outlookApp = app;
        return Task.CompletedTask;
    }
}

internal sealed class ScanWorker(OutlookStaExecutor sta, OutlookScanner scanner, CacheRepository repo, BridgeSettings settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repo.InitializeAsync();
        while (!stoppingToken.IsCancellationRequested)
        {
            await sta.InvokeAsync(() => { scanner.ScanAsync(repo).GetAwaiter().GetResult(); return 0; });
            await Task.Delay(TimeSpan.FromSeconds(settings.InboxPollSeconds), stoppingToken);
        }
    }
}

internal sealed class CacheRepository
{
    private readonly string _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw", "MailBridge", "cache.db");
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    public async Task InitializeAsync()
    {
        await using var conn = Open();
        await conn.OpenAsync();
        var sql = @"
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
        cmd.CommandText = "INSERT INTO scan_state(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value.UtcDateTime.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<DateTimeOffset?> GetScanStateAsync(string key)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT value FROM scan_state WHERE key=$k"; cmd.Parameters.AddWithValue("$k", key);
        var val = (string?)await cmd.ExecuteScalarAsync();
        return DateTimeOffset.TryParse(val, out var parsed) ? parsed : null;
    }
}

internal sealed class PipeRpcWorker(BridgeSettings settings, BridgeStateStore state, CacheRepository repo, ILogger<PipeRpcWorker> logger) : BackgroundService
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var server = CreateServer();
            await server.WaitForConnectionAsync(stoppingToken);
            _ = HandleClientAsync(server, stoppingToken);
        }
    }

    private NamedPipeServerStream CreateServer()
    {
        var sec = BuildPipeSecurity();
        return NamedPipeServerStreamAcl.Create(settings.PipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 65536, 1024 * 1024, sec);
    }

    private static PipeSecurity BuildPipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        // openclaw-svc SID placeholder must be set by operator if different account
        security.AddAccessRule(new PipeAccessRule(new NTAccount("openclaw-svc"), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        try
        {
            using var ms = new MemoryStream();
            var buffer = new byte[4096];
            do
            {
                var read = await server.ReadAsync(buffer, ct);
                ms.Write(buffer, 0, read);
                if (ms.Length > 65536) { await WriteResponse(server, RpcResponse.Failure("unknown", BridgeErrorCodes.PayloadTooLarge, "Request exceeds 64KB"), ct); return; }
            } while (!server.IsMessageComplete);

            var req = JsonSerializer.Deserialize<RpcRequest>(Encoding.UTF8.GetString(ms.ToArray()), _json);
            if (req is null || !BridgeMethods.All.Contains(req.Method))
            {
                await WriteResponse(server, RpcResponse.Failure(req?.Id ?? "unknown", BridgeErrorCodes.InvalidRequest, "Unsupported method"), ct);
                return;
            }

            var resp = await Handle(req);
            await WriteResponse(server, resp, ct);
        }
        catch (Exception ex)
        {
            logger.LogError("Pipe request failed: {Message}", ex.Message);
        }
    }

    private async Task<RpcResponse> Handle(RpcRequest req)
    {
        if (req.Method == BridgeMethods.GetStatus)
        {
            state.LastInboxScanUtc = await repo.GetScanStateAsync("last_inbox_scan_utc");
            state.LastCalendarScanUtc = await repo.GetScanStateAsync("last_calendar_scan_utc");
            return RpcResponse.Success(req.Id, new BridgeStatusDto(state.State.ToString(), state.Mode, state.OutlookConnected, state.CacheStale, state.StaleReason, state.LastInboxScanUtc, state.LastCalendarScanUtc));
        }

        return RpcResponse.Success(req.Id, new { items = Array.Empty<object>() });
    }

    private async Task WriteResponse(Stream stream, RpcResponse response, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, _json);
        if (payload.Length > 1024 * 1024) response = RpcResponse.Failure(response.Id, BridgeErrorCodes.InternalError, "Response too large");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, _json);
        await stream.WriteAsync(bytes, ct);
    }
}

internal static class ComActiveObject
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, nint reserved, [MarshalAs(UnmanagedType.IUnknown)] out object obj);

    public static object? TryGet(string progId)
    {
        try
        {
            CLSIDFromProgID(progId, out var clsid);
            GetActiveObject(ref clsid, nint.Zero, out var obj);
            return obj;
        }
        catch
        {
            return null;
        }
    }
}
