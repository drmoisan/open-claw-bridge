using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Microsoft Graph-backed implementation of <see cref="IMailboxScopeProbe"/> (spec D2).
/// Constructor seams mirror <see cref="GraphHostAdapterClient"/> exactly and it builds its
/// own <see cref="GraphRequestExecutor"/>, so the probe inherits the production pipeline
/// unchanged: app-only bearer token, <c>client-request-id</c> header, retry/backoff, and
/// the D5 error mapping the scope-boundary classifier depends on. Tokens and response
/// bodies are never logged (an existing executor guarantee). The harmless read is
/// <c>GET users/{escaped-upn}/messages?$top=1&amp;$select=id</c>; a 200 with an empty
/// <c>value</c> array is success, and the success body is discarded (no DTO mapping, so no
/// <see cref="GraphMappingException"/> path exists in the probe).
/// </summary>
internal sealed class GraphMailboxScopeProbe : IMailboxScopeProbe
{
    private readonly GraphRequestExecutor executor;

    /// <summary>
    /// Creates the Graph-backed probe. All seams are injected: HTTP transport, options,
    /// app-only token acquisition, and the clock driving retry backoff.
    /// </summary>
    public GraphMailboxScopeProbe(
        HttpClient httpClient,
        IOptions<GraphAdapterOptions> optionsAccessor,
        IAppTokenProvider tokenProvider,
        TimeProvider timeProvider,
        ILogger<GraphMailboxScopeProbe> logger
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        executor = new GraphRequestExecutor(
            httpClient,
            tokenProvider,
            timeProvider,
            optionsAccessor.Value,
            logger
        );
    }

    /// <inheritdoc />
    public async Task<MailboxProbeOutcome> ProbeMailboxReadAsync(
        string mailboxUpn,
        string? requestId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxUpn);

        var url = $"users/{Uri.EscapeDataString(mailboxUpn)}/messages?$top=1&$select=id";

        // The success body is discarded: the probe asserts authorization, not content, so
        // a 200 (even with an empty value array) parses to a non-null sentinel and never
        // takes the executor's unparseable-body path.
        var envelope = await executor.ExecuteAsync(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            static _ => true,
            requestId,
            cancellationToken
        );

        return new MailboxProbeOutcome(
            envelope.Ok,
            envelope.Error?.Code,
            envelope.Error?.BridgeErrorCode,
            envelope.Error?.Message
        );
    }
}
