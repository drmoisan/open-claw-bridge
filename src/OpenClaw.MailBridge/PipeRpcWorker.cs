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
    IScanStateRepository repo,
    ILogger<PipeRpcWorker> logger
) : BackgroundService
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    [ExcludeFromCodeCoverage]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var server = CreateServer();
            await server.WaitForConnectionAsync(stoppingToken);
            _ = HandleClientAsync(server, stoppingToken);
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

        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            security.AddAccessRule(
                new PipeAccessRule(currentUser, PipeAccessRights.ReadWrite, AccessControlType.Allow)
            );
        }

        try
        {
            security.AddAccessRule(
                new PipeAccessRule(new NTAccount("openclaw-svc"), PipeAccessRights.ReadWrite, AccessControlType.Allow)
            );
        }
        catch (IdentityNotMappedException)
        {
            // Allow local/dev environments that do not provision the service identity yet.
        }
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
            } while (!server.IsMessageComplete);

            var req = JsonSerializer.Deserialize<RpcRequest>(
                Encoding.UTF8.GetString(ms.ToArray()),
                _json
            );

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

    internal async Task<RpcResponse> Handle(RpcRequest req)
    {
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
    }
}
