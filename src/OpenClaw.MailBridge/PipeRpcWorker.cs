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

/// <summary>
/// Hosts the named-pipe RPC endpoint that exposes bridge status and cache-backed metadata to clients.
/// </summary>
/// <param name="settings">Bridge settings that define the named-pipe endpoint.</param>
/// <param name="state">Shared bridge state returned by status requests.</param>
/// <param name="repo">Repository used to read persisted scan-state timestamps.</param>
/// <param name="logger">Logger used to record request-processing failures.</param>
[ExcludeFromCodeCoverage]
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
