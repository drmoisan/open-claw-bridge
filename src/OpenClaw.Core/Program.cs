using Microsoft.Extensions.Options;
using OpenClaw.Core;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.Core.CloudGraph;
using OpenClaw.Core.CloudSync;
using OpenClaw.HostAdapter.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder
    .Services.AddOptions<OpenClawOptions>()
    .Bind(builder.Configuration.GetSection("OpenClaw"))
    .PostConfigure(options =>
    {
        options.HostAdapter.BaseUrl = string.IsNullOrWhiteSpace(options.HostAdapter.BaseUrl)
            ? "http://host.docker.internal:4319/"
            : EnsureTrailingSlash(options.HostAdapter.BaseUrl);
        options.HostAdapter.TokenFile = string.IsNullOrWhiteSpace(options.HostAdapter.TokenFile)
            ? "/run/openclaw/hostadapter.token"
            : options.HostAdapter.TokenFile;
        options.Polling.MessagesIntervalSeconds =
            options.Polling.MessagesIntervalSeconds <= 0
                ? 60
                : options.Polling.MessagesIntervalSeconds;
        options.Polling.MeetingRequestsIntervalSeconds =
            options.Polling.MeetingRequestsIntervalSeconds <= 0
                ? 60
                : options.Polling.MeetingRequestsIntervalSeconds;
        options.Polling.CalendarIntervalSeconds =
            options.Polling.CalendarIntervalSeconds <= 0
                ? 300
                : options.Polling.CalendarIntervalSeconds;
        options.Polling.MessageLookbackHours =
            options.Polling.MessageLookbackHours <= 0 ? 48 : options.Polling.MessageLookbackHours;
        options.Polling.CalendarPastDays =
            options.Polling.CalendarPastDays <= 0 ? 14 : options.Polling.CalendarPastDays;
        options.Polling.CalendarFutureDays =
            options.Polling.CalendarFutureDays <= 0 ? 30 : options.Polling.CalendarFutureDays;
        options.Defaults.Limit = options.Defaults.Limit <= 0 ? 100 : options.Defaults.Limit;
        options.Defaults.MaxLimit =
            options.Defaults.MaxLimit <= 0 ? 250 : options.Defaults.MaxLimit;
        options.Storage.DbPath = string.IsNullOrWhiteSpace(options.Storage.DbPath)
            ? "/data/openclaw.db"
            : options.Storage.DbPath;
    });
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenClawOptions>>().Value);
builder.Services.AddSingleton<CoreHealthState>();
builder.Services.AddSingleton<CoreCacheRepository>();

// Backend selection (issue #115, D8): OpenClaw:GraphAdapter:Enabled=true opts into
// the Graph-backed adapter; the default path registers the local client unchanged.
if (builder.Configuration.GetValue<bool>("OpenClaw:GraphAdapter:Enabled"))
{
    builder.Services.AddGraphHostAdapterClient(builder.Configuration);
}
else
{
    builder.Services.AddHttpClient<IHostAdapterClient, HostAdapterHttpClient>(
        (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenClawOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.HostAdapter.BaseUrl));
        }
    );
}

// Scope-boundary startup validation (issue #120, D6): opt-in via OpenClaw:ScopeValidation:Enabled; registers nothing when disabled and throws if enabled without the Graph adapter.
OpenClaw.Core.ScopeValidation.ScopeValidationServiceCollectionExtensions.AddScopeBoundaryValidation(
    builder.Services,
    builder.Configuration
);

// CloudSync opt-in (issue #117, D-6): OpenClaw:CloudSync:Enabled=true registers the
// webhook processor, stores, and workers; the default path registers nothing new.
if (builder.Configuration.GetValue<bool>("OpenClaw:CloudSync:Enabled"))
{
    builder.Services.AddCloudSync(builder.Configuration);
}
builder.Services.AddHostedService<MessagePollingWorker>();
builder.Services.AddHostedService<CalendarPollingWorker>();

// Deterministic agent (D5/D6) registrations.
builder
    .Services.AddOptions<AgentPolicyOptions>()
    .Bind(builder.Configuration.GetSection("OpenClaw:AgentPolicy"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SchedulingDtoMapper>();
builder.Services.AddSingleton<ISchedulingService, HostAdapterSchedulingService>();
builder.Services.AddSingleton<ISentActionStore>(sp => sp.GetRequiredService<CoreCacheRepository>());
builder.Services.AddSingleton<ISeriesMoveHistory>(sp =>
    sp.GetRequiredService<CoreCacheRepository>()
);
builder.Services.AddSingleton<IActionAuditLog>(sp => sp.GetRequiredService<CoreCacheRepository>());
builder.Services.AddSingleton<
    ICloudSyncActivityAuditor,
    OpenClaw.Core.Agent.CloudSyncActivityAuditor
>();
builder.Services.AddSingleton<ISchedulingCandidateSource, CacheSchedulingCandidateSource>();
builder.Services.AddHostedService<SchedulingWorker>();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();

// CloudSync opt-in (issue #117, D-6): the webhook route exists only when enabled.
if (app.Configuration.GetValue<bool>("OpenClaw:CloudSync:Enabled"))
{
    app.MapGraphNotificationsEndpoint();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet(
    "/health/ready",
    (CoreHealthState healthState) =>
    {
        var snapshot = healthState.GetSnapshot();
        var payload = new
        {
            status = snapshot.DatabaseReady && snapshot.HostAdapterReachable ? "ready" : "degraded",
            sqliteReady = snapshot.DatabaseReady,
            hostAdapterReachable = snapshot.HostAdapterReachable,
            lastSuccessfulPollUtc = snapshot.LastSuccessfulPollUtc,
            cacheStale = snapshot.BridgeStatus?.CacheStale ?? true,
        };
        return snapshot.DatabaseReady && snapshot.HostAdapterReachable
            ? Results.Ok(payload)
            : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
);

app.MapGet(
    "/api/status",
    async (
        CoreCacheRepository repository,
        CoreHealthState healthState,
        IOptions<OpenClawOptions> optionsAccessor
    ) =>
    {
        var counts = await repository.GetCountsAsync();
        var latestBridgeSnapshot = await repository.GetLatestBridgeStatusSnapshotAsync();
        var lastSuccessfulPollUtc = await repository.GetLastSuccessfulPollUtcAsync();
        var healthSnapshot = healthState.GetSnapshot();
        return Results.Ok(
            new
            {
                sqliteReady = healthSnapshot.DatabaseReady,
                hostAdapterReachable = healthSnapshot.HostAdapterReachable,
                lastSuccessfulPollUtc,
                lastFailedPollUtc = healthSnapshot.LastFailedPollUtc,
                lastFailureReason = healthSnapshot.LastFailureReason,
                bridge = latestBridgeSnapshot?.BridgeStatus,
                bridgeObservedAtUtc = latestBridgeSnapshot?.ObservedAtUtc,
                bridgeFreshness = new
                {
                    cacheStale = latestBridgeSnapshot?.BridgeStatus.CacheStale ?? true,
                    staleReason = latestBridgeSnapshot?.BridgeStatus.StaleReason,
                },
                dbPath = optionsAccessor.Value.Storage.DbPath,
                cacheItemCounts = new
                {
                    messages = counts.Messages,
                    meetingRequests = counts.MeetingRequests,
                    events = counts.Events,
                },
            }
        );
    }
);

app.MapGet(
    "/api/messages/recent",
    async (
        HttpContext context,
        CoreCacheRepository repository,
        IOptions<OpenClawOptions> optionsAccessor
    ) =>
    {
        var options = optionsAccessor.Value;
        if (!TryGetKind(context.Request.Query["kind"], out var kind))
        {
            return Results.BadRequest(
                new { code = "INVALID_REQUEST", message = "kind must be all, mail, or meeting." }
            );
        }

        if (
            !TryGetLimit(
                context.Request.Query["limit"],
                options,
                out var limit,
                out var limitMessage
            )
        )
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", message = limitMessage });
        }

        if (
            !TryGetOptionalUtc(
                context.Request.Query["since"],
                out var sinceUtc,
                out var sinceMessage
            )
        )
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", message = sinceMessage });
        }

        var items = await repository.ListMessagesAsync(kind, sinceUtc, limit);
        return Results.Ok(new { items });
    }
);

app.MapGet(
    "/api/messages/{bridgeId}",
    async (string bridgeId, CoreCacheRepository repository) =>
    {
        var message = await repository.GetMessageAsync(bridgeId);
        return message is null ? Results.NotFound() : Results.Ok(message);
    }
);

app.MapGet(
    "/api/events/window",
    async (
        HttpContext context,
        CoreCacheRepository repository,
        IOptions<OpenClawOptions> optionsAccessor
    ) =>
    {
        var options = optionsAccessor.Value;
        if (
            !TryGetRequiredUtc(
                context.Request.Query["start"],
                "start",
                out var startUtc,
                out var startMessage
            )
        )
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", message = startMessage });
        }

        if (
            !TryGetRequiredUtc(
                context.Request.Query["end"],
                "end",
                out var endUtc,
                out var endMessage
            )
        )
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", message = endMessage });
        }

        if (endUtc <= startUtc)
        {
            return Results.BadRequest(
                new { code = "INVALID_REQUEST", message = "end must be later than start." }
            );
        }

        if (
            !TryGetLimit(
                context.Request.Query["limit"],
                options,
                out var limit,
                out var limitMessage
            )
        )
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", message = limitMessage });
        }

        var items = await repository.ListEventsAsync(startUtc, endUtc, limit);
        return Results.Ok(new { items });
    }
);

app.MapGet(
    "/api/events/{bridgeId}",
    async (string bridgeId, CoreCacheRepository repository) =>
    {
        var evt = await repository.GetEventAsync(bridgeId);
        return evt is null ? Results.NotFound() : Results.Ok(evt);
    }
);

app.MapRazorPages();
app.Run();

static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + "/";

static bool TryGetKind(string? rawValue, out string kind)
{
    kind = string.IsNullOrWhiteSpace(rawValue) ? "all" : rawValue;
    return kind is "all" or "mail" or "meeting";
}

static bool TryGetLimit(
    string? rawValue,
    OpenClawOptions options,
    out int limit,
    out string? message
)
{
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        limit = options.Defaults.Limit;
        message = null;
        return true;
    }

    if (!int.TryParse(rawValue, out limit) || limit <= 0)
    {
        message = "limit must be a positive integer.";
        return false;
    }

    if (limit > options.Defaults.MaxLimit)
    {
        message = $"limit must not exceed {options.Defaults.MaxLimit}.";
        return false;
    }

    message = null;
    return true;
}

static bool TryGetOptionalUtc(string? rawValue, out DateTimeOffset? value, out string? message)
{
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        value = null;
        message = null;
        return true;
    }

    if (DateTimeOffset.TryParse(rawValue, out var parsed) && parsed.Offset == TimeSpan.Zero)
    {
        value = parsed;
        message = null;
        return true;
    }

    value = null;
    message = "The timestamp must be an ISO-8601 UTC value.";
    return false;
}

static bool TryGetRequiredUtc(
    string? rawValue,
    string name,
    out DateTimeOffset value,
    out string message
)
{
    if (DateTimeOffset.TryParse(rawValue, out value) && value.Offset == TimeSpan.Zero)
    {
        message = string.Empty;
        return true;
    }

    value = default;
    message = $"{name} must be an ISO-8601 UTC value.";
    return false;
}
