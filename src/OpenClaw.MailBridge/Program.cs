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

/// <summary>
/// Boots the mail bridge host, loads persisted settings, and wires the background services.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the bridge process and keeps it running until the host shuts down.
    /// </summary>
    /// <param name="args">Command-line arguments used to override the settings file path.</param>
    /// <returns>
    /// A process exit code. Returns <c>0</c> when the host runs normally and <c>2</c> when
    /// settings validation fails before startup.
    /// </returns>
    public static async Task<int> Main(string[] args)
    {
        var configPath =
            GetArg(args, "--config")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenClaw",
                "MailBridge",
                "bridge.settings.json"
            );
        var settings = LoadSettings(configPath);
        var errors = BridgeSettingsValidator.Validate(settings);

        // Fail before the host starts so operators see configuration problems immediately.
        if (errors.Count > 0)
        {
            Console.Error.WriteLine(string.Join(";", errors));
            return 2;
        }

        var builder = Host.CreateApplicationBuilder(args);

        // Register long-lived bridge components once so background services can share state safely.
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

    /// <summary>
    /// Loads bridge settings from disk, creating a default settings file on first launch.
    /// </summary>
    /// <param name="path">Absolute path to the JSON settings file.</param>
    /// <returns>The deserialized settings payload or the default settings when no file exists.</returns>
    private static BridgeSettings LoadSettings(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Seed first-run installs with a concrete file so operators can edit the effective defaults.
        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    BridgeSettings.Default,
                    new JsonSerializerOptions { WriteIndented = true }
                )
            );
            return BridgeSettings.Default;
        }

        return JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path))
            ?? BridgeSettings.Default;
    }

    /// <summary>
    /// Retrieves the value that follows a named command-line switch.
    /// </summary>
    /// <param name="args">Full command-line argument list.</param>
    /// <param name="key">Switch name to look for.</param>
    /// <returns>The value that follows <paramref name="key"/>, or <see langword="null"/> when absent.</returns>
    private static string? GetArg(string[] args, string key)
    {
        // Walk only to the penultimate argument because every supported switch expects a following value.
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}

/// <summary>
/// Tracks the bridge's current operating mode and the latest health information shared with RPC callers.
/// </summary>
internal sealed class BridgeStateStore
{
    /// <summary>
    /// Initializes state storage with the configured bridge mode and a startup lifecycle state.
    /// </summary>
    /// <param name="settings">Settings that define the advertised bridge mode.</param>
    public BridgeStateStore(BridgeSettings settings)
    {
        Mode = settings.Mode;
        State = BridgeState.starting;
    }

    /// <summary>
    /// Gets the lifecycle state that the bridge currently reports to clients.
    /// </summary>
    public BridgeState State { get; private set; }

    /// <summary>
    /// Gets the configured bridge mode exposed through status responses.
    /// </summary>
    public string Mode { get; }

    /// <summary>
    /// Gets or sets a value indicating whether Outlook is currently reachable through COM.
    /// </summary>
    public bool OutlookConnected { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cached scan data should be treated as stale.
    /// </summary>
    public bool CacheStale { get; set; }

    /// <summary>
    /// Gets or sets the reason associated with the most recent stale-cache transition.
    /// </summary>
    public string? StaleReason { get; set; }

    /// <summary>
    /// Gets or sets the last successful inbox scan timestamp.
    /// </summary>
    public DateTimeOffset? LastInboxScanUtc { get; set; }

    /// <summary>
    /// Gets or sets the last successful calendar scan timestamp.
    /// </summary>
    public DateTimeOffset? LastCalendarScanUtc { get; set; }

    /// <summary>
    /// Updates the bridge lifecycle state that downstream components expose to callers.
    /// </summary>
    /// <param name="state">New lifecycle state.</param>
    public void SetState(BridgeState state) => State = state;
}

/// <summary>
/// Executes Outlook COM work on a dedicated STA thread so background services can call into Outlook safely.
/// </summary>
internal sealed class OutlookStaExecutor : IDisposable
{
    private readonly BlockingCollection<(
        Func<object?> work,
        TaskCompletionSource<object?> tcs
    )> _queue = new();
    private readonly Thread _thread;

    /// <summary>
    /// Starts the dedicated STA worker thread used for COM-bound Outlook work.
    /// </summary>
    public OutlookStaExecutor()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Outlook-STA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>
    /// Schedules work to run on the STA thread and returns the operation result asynchronously.
    /// </summary>
    /// <typeparam name="T">Type produced by the scheduled operation.</typeparam>
    /// <param name="operation">Synchronous COM-bound work to execute.</param>
    /// <returns>The value produced by <paramref name="operation"/>.</returns>
    public async Task<T> InvokeAsync<T>(Func<T> operation)
    {
        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _queue.Add((() => operation()!, tcs));
        var value = await tcs.Task.ConfigureAwait(false);
        return (T)value!;
    }

    /// <summary>
    /// Processes queued COM work items sequentially on the dedicated STA thread.
    /// </summary>
    private void Run()
    {
        // Serialize all COM calls through one apartment so Outlook sees a consistent threading model.
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            try
            {
                item.tcs.SetResult(item.work());
            }
            catch (Exception ex)
            {
                item.tcs.SetException(ex);
            }
        }
    }

    /// <summary>
    /// Stops accepting new work and allows the STA thread to drain outstanding operations.
    /// </summary>
    public void Dispose() => _queue.CompleteAdding();
}

/// <summary>
/// Coordinates Outlook discovery and updates the cache metadata that signals scan freshness.
/// </summary>
/// <param name="settings">Bridge settings that control Outlook startup behavior.</param>
/// <param name="state">Shared state store updated with scan health information.</param>
/// <param name="logger">Logger used to capture scan failures.</param>
internal sealed class OutlookScanner(
    BridgeSettings settings,
    BridgeStateStore state,
    ILogger<OutlookScanner> logger
)
{
    private object? _outlookApp;

    /// <summary>
    /// Ensures Outlook is reachable and records the timestamps for a successful scan cycle.
    /// </summary>
    /// <param name="repo">Repository that persists scan-state timestamps.</param>
    public async Task ScanAsync(CacheRepository repo)
    {
        try
        {
            await EnsureOutlookAsync();
            state.OutlookConnected = _outlookApp is not null;

            // Stop the cycle early when Outlook is still unavailable; the state store already reflects why.
            if (_outlookApp is null)
                return;

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

    /// <summary>
    /// Connects to an existing Outlook instance or launches Outlook when configuration allows it.
    /// </summary>
    /// <returns>A completed task once the Outlook connection attempt has finished.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Outlook must be started but COM activation fails.
    /// </exception>
    private Task EnsureOutlookAsync()
    {
        if (_outlookApp is not null)
            return Task.CompletedTask;

        var outlookRunning = Process.GetProcessesByName("OUTLOOK").Length > 0;

        // Prefer attaching to an already-running Outlook instance to avoid duplicate desktop sessions.
        if (outlookRunning)
        {
            _outlookApp = ComActiveObject.TryGet("Outlook.Application");
            if (_outlookApp is null)
            {
                state.SetState(BridgeState.degraded);
            }
            return Task.CompletedTask;
        }

        // When auto-start is disabled, stay in a waiting state instead of creating Outlook implicitly.
        if (!settings.AutostartOutlook)
        {
            state.SetState(BridgeState.waiting_for_outlook);
            return Task.CompletedTask;
        }

        // Bootstrap a new Outlook session only after attach and wait paths have been ruled out.
        var t =
            Type.GetTypeFromProgID("Outlook.Application", false)
            ?? throw new InvalidOperationException("Outlook COM unavailable");
        var app =
            Activator.CreateInstance(t)
            ?? throw new InvalidOperationException("Outlook activation failed");
        var ns = t.InvokeMember("GetNamespace", BindingFlags.InvokeMethod, null, app, ["MAPI"]);
        ns!
            .GetType()
            .InvokeMember(
                "Logon",
                BindingFlags.InvokeMethod,
                null,
                ns,
                ["", "", Type.Missing, Type.Missing]
            );
        _outlookApp = app;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Periodically runs the Outlook scan workflow on the dedicated STA executor.
/// </summary>
/// <param name="sta">Executor that serializes Outlook COM work onto an STA thread.</param>
/// <param name="scanner">Scanner that refreshes Outlook-derived cache metadata.</param>
/// <param name="repo">Repository that persists scan-state timestamps.</param>
/// <param name="settings">Settings that define the scan interval.</param>
internal sealed class ScanWorker(
    OutlookStaExecutor sta,
    OutlookScanner scanner,
    CacheRepository repo,
    BridgeSettings settings
) : BackgroundService
{
    /// <summary>
    /// Initializes the cache and then repeats scan cycles until the host is asked to stop.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token signaled during host shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repo.InitializeAsync();

        // Keep scanning on a fixed cadence so the bridge status reflects Outlook health continuously.
        while (!stoppingToken.IsCancellationRequested)
        {
            await sta.InvokeAsync(() =>
            {
                scanner.ScanAsync(repo).GetAwaiter().GetResult();
                return 0;
            });
            await Task.Delay(TimeSpan.FromSeconds(settings.InboxPollSeconds), stoppingToken);
        }
    }
}

/// <summary>
/// Persists cached bridge metadata in a local SQLite database under the user's profile.
/// </summary>
internal sealed class CacheRepository
{
    private readonly string _dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenClaw",
        "MailBridge",
        "cache.db"
    );
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Opens a SQLite connection against the bridge cache database, creating the parent directory first.
    /// </summary>
    /// <returns>An unopened SQLite connection for the cache database.</returns>
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    /// <summary>
    /// Ensures the cache database schema exists before scans begin writing state.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var conn = Open();
        await conn.OpenAsync();

        // Bootstrap every table in one pass so later scan operations can assume the schema already exists.
        var sql =
            @"
CREATE TABLE IF NOT EXISTS messages(bridge_id TEXT PRIMARY KEY,entry_id TEXT NOT NULL,store_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,received_utc TEXT NULL,sent_utc TEXT NULL,importance INTEGER NULL,sensitivity INTEGER NULL,unread INTEGER NOT NULL,has_attachments INTEGER NOT NULL,message_class TEXT NULL,sender_name TEXT NULL,sender_email TEXT NULL,to_json TEXT NULL,cc_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_seen_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS events(bridge_id TEXT PRIMARY KEY,entry_id TEXT NULL,store_id TEXT NULL,global_appointment_id TEXT NULL,item_kind TEXT NOT NULL,subject TEXT NULL,start_utc TEXT NOT NULL,end_utc TEXT NOT NULL,location TEXT NULL,busy_status INTEGER NULL,meeting_status INTEGER NULL,is_recurring INTEGER NOT NULL,sensitivity INTEGER NULL,organizer TEXT NULL,required_attendees_json TEXT NULL,optional_attendees_json TEXT NULL,resources_json TEXT NULL,body_preview TEXT NULL,protected_fields_available INTEGER NOT NULL,is_redacted INTEGER NOT NULL,last_modified_utc TEXT NULL,last_seen_utc TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS scan_state(key TEXT PRIMARY KEY,value TEXT NOT NULL);";
        await new SqliteCommand(sql, conn).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Upserts a scan-state timestamp under the supplied key.
    /// </summary>
    /// <param name="key">Logical state key, such as the last inbox scan timestamp.</param>
    /// <param name="value">UTC timestamp to persist.</param>
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

    /// <summary>
    /// Retrieves a previously persisted scan-state timestamp.
    /// </summary>
    /// <param name="key">Logical state key to fetch.</param>
    /// <returns>The parsed timestamp when the key exists; otherwise <see langword="null"/>.</returns>
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

/// <summary>
/// Hosts the named-pipe RPC endpoint that exposes bridge status and cache-backed metadata to clients.
/// </summary>
/// <param name="settings">Bridge settings that define the named-pipe endpoint.</param>
/// <param name="state">Shared bridge state returned by status requests.</param>
/// <param name="repo">Repository used to read persisted scan-state timestamps.</param>
/// <param name="logger">Logger used to record request-processing failures.</param>
internal sealed class PipeRpcWorker(
    BridgeSettings settings,
    BridgeStateStore state,
    CacheRepository repo,
    ILogger<PipeRpcWorker> logger
) : BackgroundService
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Accepts named-pipe client connections until the host is asked to stop.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token signaled during host shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Accept one connection per server instance and immediately spin up the next listener.
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var server = CreateServer();
            await server.WaitForConnectionAsync(stoppingToken);
            _ = HandleClientAsync(server, stoppingToken);
        }
    }

    /// <summary>
    /// Creates a named-pipe server with the repository's ACL expectations.
    /// </summary>
    /// <returns>A configured named-pipe server stream.</returns>
    private NamedPipeServerStream CreateServer()
    {
        var sec = BuildPipeSecurity();
        return NamedPipeServerStreamAcl.Create(
            settings.PipeName,
            PipeDirection.InOut,
            4,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous,
            65536,
            1024 * 1024,
            sec
        );
    }

    /// <summary>
    /// Builds the pipe ACL that permits local service accounts while blocking network-originated access.
    /// </summary>
    /// <returns>The named-pipe security descriptor used for each listener instance.</returns>
    private static PipeSecurity BuildPipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow
            )
        );
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow
            )
        );
        security.AddAccessRule(
            new PipeAccessRule(
                WindowsIdentity.GetCurrent().User!,
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow
            )
        );

        // Keep the service account rule explicit so deployments can swap the identity without widening access.
        security.AddAccessRule(
            new PipeAccessRule(
                new NTAccount("openclaw-svc"),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow
            )
        );
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Deny
            )
        );
        return security;
    }

    /// <summary>
    /// Reads, validates, and responds to a single named-pipe request.
    /// </summary>
    /// <param name="server">Connected named-pipe server stream.</param>
    /// <param name="ct">Cancellation token for the request lifecycle.</param>
    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        try
        {
            using var ms = new MemoryStream();
            var buffer = new byte[4096];

            // Assemble the full payload before deserializing so request validation works on complete JSON.
            do
            {
                var read = await server.ReadAsync(buffer, ct);
                ms.Write(buffer, 0, read);
                if (ms.Length > 65536)
                {
                    await WriteResponse(
                        server,
                        RpcResponse.Failure(
                            "unknown",
                            BridgeErrorCodes.PayloadTooLarge,
                            "Request exceeds 64KB"
                        ),
                        ct
                    );
                    return;
                }
            } while (!server.IsMessageComplete);

            var req = JsonSerializer.Deserialize<RpcRequest>(
                Encoding.UTF8.GetString(ms.ToArray()),
                _json
            );

            // Reject unknown methods before dispatch so handlers can assume a valid contract.
            if (req is null || !BridgeMethods.All.Contains(req.Method))
            {
                await WriteResponse(
                    server,
                    RpcResponse.Failure(
                        req?.Id ?? "unknown",
                        BridgeErrorCodes.InvalidRequest,
                        "Unsupported method"
                    ),
                    ct
                );
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

    /// <summary>
    /// Executes a validated RPC request and returns the corresponding response payload.
    /// </summary>
    /// <param name="req">Validated request to execute.</param>
    /// <returns>The response payload to write back to the client.</returns>
    private async Task<RpcResponse> Handle(RpcRequest req)
    {
        // Route status requests to the persisted scan-state path; all other methods currently return an empty result envelope.
        if (req.Method == BridgeMethods.GetStatus)
        {
            state.LastInboxScanUtc = await repo.GetScanStateAsync("last_inbox_scan_utc");
            state.LastCalendarScanUtc = await repo.GetScanStateAsync("last_calendar_scan_utc");
            return RpcResponse.Success(
                req.Id,
                new BridgeStatusDto(
                    state.State.ToString(),
                    state.Mode,
                    state.OutlookConnected,
                    state.CacheStale,
                    state.StaleReason,
                    state.LastInboxScanUtc,
                    state.LastCalendarScanUtc
                )
            );
        }

        return RpcResponse.Success(req.Id, new { items = Array.Empty<object>() });
    }

    /// <summary>
    /// Serializes an RPC response and writes it to the connected stream.
    /// </summary>
    /// <param name="stream">Target stream for the serialized response.</param>
    /// <param name="response">Response payload to send.</param>
    /// <param name="ct">Cancellation token for the write operation.</param>
    private async Task WriteResponse(Stream stream, RpcResponse response, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, _json);

        // Downgrade oversized payloads to a compact failure response rather than breaking the pipe protocol.
        if (payload.Length > 1024 * 1024)
            response = RpcResponse.Failure(
                response.Id,
                BridgeErrorCodes.InternalError,
                "Response too large"
            );

        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, _json);
        await stream.WriteAsync(bytes, ct);
    }
}

/// <summary>
/// Resolves COM automation objects that are already registered in the running object table.
/// </summary>
internal static class ComActiveObject
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        nint reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object obj
    );

    /// <summary>
    /// Attempts to retrieve a running COM object by ProgID.
    /// </summary>
    /// <param name="progId">Programmatic identifier for the COM server.</param>
    /// <returns>The active COM object when present; otherwise <see langword="null"/>.</returns>
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
            // Treat lookup failures as a missing running instance because callers have a fallback path.
            return null;
        }
    }
}
