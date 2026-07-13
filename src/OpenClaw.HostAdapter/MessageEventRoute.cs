using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

/// <summary>
/// Registration for the message-to-event linkage route (issue #146):
/// <c>GET /users/{id}/messages/{messageId}/event</c>. It mirrors the existing
/// <c>GET /users/{id}/messages/{messageId}</c> route pattern (request id, ready-bridge gate, bridge-id
/// validation, CLI command build, process-runner execution, then <c>ToHttpResult</c>) but uses the
/// null-tolerant <see cref="HostAdapterEventProjector.ProjectNullableEvent"/> so an unlinked message
/// resolves to an <c>ok:true</c> / <c>data:null</c> / HTTP 200 envelope rather than a 502. Extracted
/// into its own file to keep <c>Program.cs</c> under the 500-line cap.
/// </summary>
internal static class MessageEventRoute
{
    public static void MapMessageEventRoute(this WebApplication app)
    {
        app.MapGet(
            "/users/{id}/messages/{messageId}/event",
            async (
                string id,
                string messageId,
                HttpContext context,
                StatusCacheService statusCache,
                HostAdapterCommandBuilder commandBuilder,
                IHostAdapterProcessRunner processRunner,
                IOptions<HostAdapterOptions> optionsAccessor,
                CancellationToken cancellationToken
            ) =>
            {
                var requestId = context.GetRequestId();
                var options = optionsAccessor.Value;
                var (bridgeStatus, failure) = await Program.RequireReadyBridgeAsync<EventDto?>(
                    requestId,
                    options,
                    statusCache,
                    cancellationToken
                );
                if (failure is not null)
                {
                    return Program.ToHttpResult(context, failure);
                }

                if (
                    !HostAdapterRequestValidation.TryGetBridgeId<EventDto?>(
                        messageId,
                        requestId,
                        options,
                        bridgeStatus,
                        out var normalizedBridgeId,
                        out var bridgeIdFailure
                    )
                )
                {
                    return Program.ToHttpResult(context, bridgeIdFailure!);
                }

                var result = await processRunner.ExecuteAsync<EventDto?>(
                    commandBuilder.BuildGetEventForMessage(normalizedBridgeId),
                    requestId,
                    bridgeStatus,
                    HostAdapterEventProjector.ProjectNullableEvent,
                    cancellationToken
                );
                return Program.ToHttpResult(context, result);
            }
        );
    }
}
