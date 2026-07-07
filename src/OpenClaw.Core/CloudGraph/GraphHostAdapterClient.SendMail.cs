using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Interface member 9: <c>SendMailAsync</c> posting the D7 body to
/// <c>POST /users/{a}/sendMail</c> through the assistant mailbox.
/// </summary>
internal sealed partial class GraphHostAdapterClient
{
    /// <summary>
    /// The configuration key whose value governs on-behalf authorization; named in the
    /// deny envelope so operators can locate the fix without exposing any mailbox UPN.
    /// </summary>
    private const string AllowlistKey = "OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns";

    /// <summary>
    /// Sends an outbound message: the <see cref="SendMailRequest"/> wire shape passes
    /// through in camelCase, with <c>message.from.emailAddress.address = {p}</c>
    /// injected only when the send is authorized on behalf of a differing principal (D7,
    /// F15). A non-allowlisted differing principal is denied fail-closed before any token
    /// acquisition or HTTP request. Graph's <c>202 Accepted</c> (empty body) yields
    /// <c>ok: true, data: null</c>; failures map through the shared D5 pipeline.
    /// </summary>
    public Task<ApiEnvelope<object?>> SendMailAsync(
        SendMailRequest request,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var decision = SendOnBehalfAuthorizer.Authorize(
            options.PrincipalMailboxUpn,
            options.AssistantMailboxUpn,
            options.AllowedPrincipalMailboxUpns
        );

        if (decision == SendAuthorizationDecision.DeniedNotAllowlisted)
        {
            var deniedRequestId = ResolveRequestId(requestId);
            logger.LogWarning(
                "Send-on-behalf denied for request {RequestId}: the configured principal is "
                    + "not present in the allowlist.",
                deniedRequestId
            );
            return Task.FromResult(
                new ApiEnvelope<object?>(
                    false,
                    null,
                    new ApiMeta(deniedRequestId, GraphRequestExecutor.AdapterVersion, null),
                    new ApiError(
                        "UNAUTHORIZED",
                        "The send was denied because the configured principal is not present in "
                            + $"{AllowlistKey}. Add the principal to the allowlist to permit this "
                            + "on-behalf send.",
                        "SendOnBehalfDenied",
                        Retryable: false
                    )
                )
            );
        }

        var url = $"users/{Assistant}/sendMail";
        var bodyJson = ComposeSendMailBody(request, decision);

        return executor.ExecuteAsync<object?>(
            () =>
                new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
                },
            _ => null,
            requestId,
            cancellationToken
        );
    }

    /// <summary>
    /// Serializes the wire request with camelCase names and injects the on-behalf
    /// <c>from</c> address if and only if the shared authorization
    /// <paramref name="decision"/> is <see cref="SendAuthorizationDecision.AllowedOnBehalf"/>
    /// (the wire contract carries no <c>from</c> field of its own). The from-injection
    /// predicate and the authorization decision therefore share one source.
    /// </summary>
    private string ComposeSendMailBody(SendMailRequest request, SendAuthorizationDecision decision)
    {
        var root = JsonSerializer
            .SerializeToNode(request, GraphRequestExecutor.JsonOptions)!
            .AsObject();

        if (decision == SendAuthorizationDecision.AllowedOnBehalf)
        {
            root["message"]!.AsObject()["from"] = new JsonObject
            {
                ["emailAddress"] = new JsonObject { ["address"] = options.PrincipalMailboxUpn },
            };
        }

        return root.ToJsonString(GraphRequestExecutor.JsonOptions);
    }
}
