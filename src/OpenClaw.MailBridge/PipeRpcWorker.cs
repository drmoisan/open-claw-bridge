using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

internal sealed class PipeRpcWorker(
    BridgeSettings settings,
    BridgeStateStore state,
    IBridgeRepository repo,
    ILogger<PipeRpcWorker> logger
) : BackgroundService
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    internal static Func<string, SecurityIdentifier> AccountSidResolver { get; set; } =
        ResolveAccountSid;

    [ExcludeFromCodeCoverage]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var server = CreateServer();

            try
            {
                await server.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await server.DisposeAsync();
                break;
            }

            _ = RunClientSessionAsync(server, stoppingToken);
        }
    }

    [ExcludeFromCodeCoverage]
    private async Task RunClientSessionAsync(
        NamedPipeServerStream server,
        CancellationToken stoppingToken
    )
    {
        await using (server)
        {
            await HandleClientAsync(server, stoppingToken);
        }
    }

    [ExcludeFromCodeCoverage]
    internal NamedPipeServerStream CreateServer()
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

    [ExcludeFromCodeCoverage]
    internal PipeSecurity BuildPipeSecurity()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Named-pipe ACL configuration requires Windows."
            );
        }

        var security = new PipeSecurity();
        AddAllowRule(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        AddAllowRule(
            security,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
        );

        var currentUser =
            WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException(
                "Unable to resolve the primary interactive user SID for pipe ACL setup."
            );
        AddAllowRule(security, currentUser);
        AddAllowRule(security, AccountSidResolver("openclaw-svc"));

        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Deny
            )
        );
        return security;
    }

    [ExcludeFromCodeCoverage]
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

                if (read == 0)
                {
                    return;
                }
            } while (!server.IsMessageComplete);

            var resp = await BuildResponseAsync(Encoding.UTF8.GetString(ms.ToArray()));
            await WriteResponse(server, resp, ct);
        }
        catch (Exception ex)
        {
            logger.LogError("Pipe request failed: {Message}", ex.Message);
            try
            {
                if (server.IsConnected)
                {
                    await WriteResponse(
                        server,
                        RpcResponse.Failure(
                            "unknown",
                            BridgeErrorCodes.InternalError,
                            "Internal error"
                        ),
                        ct
                    );
                }
            }
            catch
            {
                // Best-effort error response; the client guards against empty/malformed payloads.
            }
        }
    }

    internal async Task<RpcResponse> BuildResponseAsync(string payload)
    {
        if (Encoding.UTF8.GetByteCount(payload) > 65536)
        {
            return RpcResponse.Failure(
                "unknown",
                BridgeErrorCodes.PayloadTooLarge,
                "Request exceeds 64KB"
            );
        }

        RpcRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<RpcRequest>(payload, _json);
        }
        catch (JsonException)
        {
            req = null;
        }

        if (req is null || !BridgeMethods.All.Contains(req.Method))
        {
            return RpcResponse.Failure(
                req?.Id ?? "unknown",
                BridgeErrorCodes.InvalidRequest,
                req is null ? "Malformed JSON request." : "Unsupported method."
            );
        }

        return await Handle(req);
    }

    internal async Task<RpcResponse> Handle(RpcRequest req)
    {
        try
        {
            return req.Method switch
            {
                BridgeMethods.GetStatus => await HandleStatusAsync(req),
                BridgeMethods.ListRecentMessages => await HandleListRecentMessagesAsync(req),
                BridgeMethods.ListRecentMeetingRequests =>
                    await HandleListRecentMeetingRequestsAsync(req),
                BridgeMethods.GetMessage => await HandleGetMessageAsync(req),
                BridgeMethods.ListCalendarWindow => await HandleListCalendarWindowAsync(req),
                BridgeMethods.GetEvent => await HandleGetEventAsync(req),
                _ => RpcResponse.Failure(
                    req.Id,
                    BridgeErrorCodes.InvalidRequest,
                    "Unsupported method."
                ),
            };
        }
        catch (InvalidRequestException ex)
        {
            logger.LogWarning("Invalid request for {Method}: {Message}", req.Method, ex.Message);
            return RpcResponse.Failure(req.Id, BridgeErrorCodes.InvalidRequest, ex.Message);
        }
    }

    internal async Task WriteResponse(Stream stream, RpcResponse response, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, _json);
        if (payload.Length > 1024 * 1024)
            response = RpcResponse.Failure(
                response.Id,
                BridgeErrorCodes.InternalError,
                "Response too large"
            );

        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, _json);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);

        // Drain ensures the client has read all buffered data before the pipe
        // is disposed.  Without this, DisconnectNamedPipe / CloseHandle can
        // discard unread bytes, causing the client to receive an empty response.
        if (stream is NamedPipeServerStream pipe && pipe.IsConnected)
        {
            pipe.WaitForPipeDrain();
        }
    }

    private async Task<RpcResponse> HandleStatusAsync(RpcRequest req)
    {
        var snapshot = await repo.GetScanStateSnapshotAsync();
        state.LastInboxScanUtc = snapshot.LastInboxScanUtc;
        state.LastCalendarScanUtc = snapshot.LastCalendarScanUtc;

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

    private async Task<RpcResponse> HandleListRecentMessagesAsync(RpcRequest req)
    {
        var sinceUtc = RequireIso8601(req, "since");
        var limit = RequireLimit(req, settings.MaxItemsPerScan);
        var items = await repo.ListRecentMessagesAsync(sinceUtc, limit);
        return RpcResponse.Success(
            req.Id,
            new
            {
                items = items
                    .Select(message => ResponseShaper.ShapeMessage(message, settings))
                    .ToArray(),
            }
        );
    }

    private async Task<RpcResponse> HandleListRecentMeetingRequestsAsync(RpcRequest req)
    {
        var sinceUtc = RequireIso8601(req, "since");
        var limit = RequireLimit(req, settings.MaxItemsPerScan);
        var items = await repo.ListRecentMeetingRequestsAsync(sinceUtc, limit);
        return RpcResponse.Success(
            req.Id,
            new
            {
                items = items
                    .Select(message => ResponseShaper.ShapeMessage(message, settings))
                    .ToArray(),
            }
        );
    }

    private async Task<RpcResponse> HandleGetMessageAsync(RpcRequest req)
    {
        var bridgeId = RequireParameter(req, "id");
        if (!BridgeIdCodec.TryDecodeMessageId(bridgeId, out _, out _))
        {
            throw new InvalidRequestException("The supplied message bridge ID is invalid.");
        }

        var message = await repo.GetMessageAsync(bridgeId);
        return message is null
            ? RpcResponse.Failure(req.Id, BridgeErrorCodes.NotFound, "Message not found.")
            : RpcResponse.Success(req.Id, ResponseShaper.ShapeMessage(message, settings));
    }

    private async Task<RpcResponse> HandleListCalendarWindowAsync(RpcRequest req)
    {
        var startUtc = RequireIso8601(req, "start");
        var endUtc = RequireIso8601(req, "end");
        if (startUtc >= endUtc)
        {
            throw new InvalidRequestException("Calendar window start must be earlier than end.");
        }

        var limit = RequireLimit(req, settings.MaxItemsPerScan);
        var items = await repo.ListCalendarWindowAsync(startUtc, endUtc, limit);
        return RpcResponse.Success(
            req.Id,
            new { items = items.Select(evt => ResponseShaper.ShapeEvent(evt, settings)).ToArray() }
        );
    }

    private async Task<RpcResponse> HandleGetEventAsync(RpcRequest req)
    {
        var bridgeId = RequireParameter(req, "id");
        if (!BridgeIdCodec.TryDecodeEventId(bridgeId, out _, out _))
        {
            throw new InvalidRequestException("The supplied event bridge ID is invalid.");
        }

        var evt = await repo.GetEventAsync(bridgeId);
        return evt is null
            ? RpcResponse.Failure(req.Id, BridgeErrorCodes.NotFound, "Event not found.")
            : RpcResponse.Success(req.Id, ResponseShaper.ShapeEvent(evt, settings));
    }

    private static void AddAllowRule(PipeSecurity security, IdentityReference identity) =>
        security.AddAccessRule(
            new PipeAccessRule(
                identity,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow
            )
        );

    private static SecurityIdentifier ResolveAccountSid(string accountName)
    {
        var account = new NTAccount(accountName);
        return (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
    }

    private static string RequireParameter(RpcRequest req, string key)
    {
        if (
            req.Params is null
            || !req.Params.TryGetValue(key, out var value)
            || string.IsNullOrWhiteSpace(value)
        )
        {
            throw new InvalidRequestException($"Missing required parameter '{key}'.");
        }

        return value;
    }

    private static DateTimeOffset RequireIso8601(RpcRequest req, string key)
    {
        var value = RequireParameter(req, key);
        if (!DateTimeOffset.TryParse(value, out var parsed))
        {
            throw new InvalidRequestException(
                $"Parameter '{key}' must be a valid ISO-8601 timestamp."
            );
        }

        return parsed;
    }

    private static int RequireLimit(RpcRequest req, int maxItemsPerScan)
    {
        var value = RequireParameter(req, "limit");
        if (!int.TryParse(value, out var limit) || limit < 1 || limit > maxItemsPerScan)
        {
            throw new InvalidRequestException(
                $"Parameter 'limit' must be between 1 and {maxItemsPerScan}."
            );
        }

        return limit;
    }

    private sealed class InvalidRequestException(string message) : Exception(message);
}
