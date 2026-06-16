using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

/// <summary>
/// Registration for the Graph-shaped outbound mail action
/// <c>POST /users/{assistantMailbox}/sendMail</c> (issue #75). Extracted from <c>Program.cs</c> to
/// keep that file under the 500-line cap, mirroring <see cref="SchedulingRoutes"/>.
/// <para>
/// Caveats: the request travels over the named pipe to the bridge; a body that exceeds the 64KB
/// pipe message cap (R-1) returns 502 with the bridge <c>PAYLOAD_TOO_LARGE</c> code. A 202 indicates
/// the message was accepted for send, not guaranteed delivery; when Outlook is offline the item may
/// queue to the Outbox (R-5). The <c>{assistantMailbox}</c> segment is a Graph placeholder and is not
/// validated against the local profile (single-profile MVP, D-D).
/// </para>
/// </summary>
internal static class MailRoutes
{
    public static void MapMailRoutes(this WebApplication app)
    {
        app.MapPost(
            "/users/{assistantMailbox}/sendMail",
            async (
                string assistantMailbox,
                [FromBody] SendMailRequest request,
                HttpContext context,
                StatusCacheService statusCache,
                HostAdapterCommandBuilder commandBuilder,
                IHostAdapterProcessRunner processRunner,
                IOptions<HostAdapterOptions> optionsAccessor,
                CancellationToken cancellationToken
            ) =>
                await HandleSendMailAsync(
                    request,
                    context,
                    statusCache,
                    commandBuilder,
                    processRunner,
                    optionsAccessor.Value,
                    cancellationToken
                )
        );
    }

    private static async Task<IResult> HandleSendMailAsync(
        SendMailRequest request,
        HttpContext context,
        StatusCacheService statusCache,
        HostAdapterCommandBuilder commandBuilder,
        IHostAdapterProcessRunner processRunner,
        HostAdapterOptions options,
        CancellationToken cancellationToken
    )
    {
        var requestId = context.GetRequestId();

        // Order is preserved by the pipeline: BearerTokenMiddleware (auth) -> ready check -> validate
        // -> dispatch.
        var (bridgeStatus, failure) = await Program.RequireReadyBridgeAsync<object?>(
            requestId,
            options,
            statusCache,
            cancellationToken
        );
        if (failure is not null)
        {
            return Program.ToHttpResult(context, failure);
        }

        var validationError = ValidateRequest(request, requestId, options, bridgeStatus);
        if (validationError is not null)
        {
            return Program.ToHttpResult(context, validationError);
        }

        var result = await processRunner.ExecuteAsync<object?>(
            commandBuilder.BuildSendMail(request),
            requestId,
            bridgeStatus,
            static _ => null,
            cancellationToken
        );

        if (!result.Envelope.Ok)
        {
            // Propagate the mapped downstream failure (400 INVALID_REQUEST, 502 on COM/transport, D-H).
            return Program.ToHttpResult(context, result);
        }

        // Success path: translate the 200 runner success into 202 Accepted (D-A).
        return Program.ToHttpResult(
            context,
            HostAdapterResponses.AcceptedNoContent(
                requestId,
                options.AdapterVersion,
                result.Envelope.Meta.Bridge ?? bridgeStatus,
                result.CliExitCode
            )
        );
    }

    /// <summary>
    /// Validates the request per the locked decisions: at least one recipient across To/CC/BCC
    /// (D-G); <c>contentType</c> in {Text, HTML} case-insensitive; empty subject is permitted (D-F);
    /// <c>{assistantMailbox}</c> is not validated (D-D). Returns a 400 INVALID_REQUEST result on
    /// failure, otherwise <see langword="null"/>.
    /// </summary>
    private static AdapterCommandResult<object?>? ValidateRequest(
        SendMailRequest request,
        string requestId,
        HostAdapterOptions options,
        BridgeStatusDto? bridgeStatus
    )
    {
        var message = request.Message;
        var recipientCount =
            (message.ToRecipients?.Count ?? 0)
            + (message.CcRecipients?.Count ?? 0)
            + (message.BccRecipients?.Count ?? 0);
        if (recipientCount == 0)
        {
            return HostAdapterResponses.InvalidRequest<object?>(
                requestId,
                options.AdapterVersion,
                "At least one recipient across toRecipients/ccRecipients/bccRecipients is required.",
                bridgeStatus
            );
        }

        var contentType = message.Body?.ContentType;
        if (
            !string.Equals(contentType, "Text", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(contentType, "HTML", StringComparison.OrdinalIgnoreCase)
        )
        {
            return HostAdapterResponses.InvalidRequest<object?>(
                requestId,
                options.AdapterVersion,
                "body.contentType must be 'Text' or 'HTML'.",
                bridgeStatus
            );
        }

        return null;
    }
}
