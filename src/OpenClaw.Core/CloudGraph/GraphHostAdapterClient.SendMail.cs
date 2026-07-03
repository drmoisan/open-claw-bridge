using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Interface member 9: <c>SendMailAsync</c> posting the D7 body to
/// <c>POST /users/{a}/sendMail</c> through the assistant mailbox.
/// </summary>
internal sealed partial class GraphHostAdapterClient
{
    /// <summary>
    /// Sends an outbound message: the <see cref="SendMailRequest"/> wire shape passes
    /// through in camelCase, with <c>message.from.emailAddress.address = {p}</c>
    /// injected only when the principal and assistant mailboxes differ (D7). Graph's
    /// <c>202 Accepted</c> (empty body) yields <c>ok: true, data: null</c>; failures
    /// map through the shared D5 pipeline.
    /// </summary>
    public Task<ApiEnvelope<object?>> SendMailAsync(
        SendMailRequest request,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = $"users/{Assistant}/sendMail";
        var bodyJson = ComposeSendMailBody(request);

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
    /// <c>from</c> address when <c>{p} != {a}</c> (the wire contract carries no
    /// <c>from</c> field of its own).
    /// </summary>
    private string ComposeSendMailBody(SendMailRequest request)
    {
        var root = JsonSerializer
            .SerializeToNode(request, GraphRequestExecutor.JsonOptions)!
            .AsObject();

        var principalIsAssistant = string.Equals(
            options.PrincipalMailboxUpn,
            options.AssistantMailboxUpn,
            StringComparison.OrdinalIgnoreCase
        );
        if (!principalIsAssistant)
        {
            root["message"]!.AsObject()["from"] = new JsonObject
            {
                ["emailAddress"] = new JsonObject { ["address"] = options.PrincipalMailboxUpn },
            };
        }

        return root.ToJsonString(GraphRequestExecutor.JsonOptions);
    }
}
