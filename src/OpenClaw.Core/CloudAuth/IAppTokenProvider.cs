namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// Host-neutral contract for acquiring an app-only (client-credentials) access token
/// (D3). Consumers depend on this interface only; no <c>Azure.*</c> or MSAL type
/// crosses the contract, so callers never need <c>Azure.Identity</c> to obtain or
/// handle tokens.
/// </summary>
public interface IAppTokenProvider
{
    /// <summary>
    /// Returns a currently fresh <see cref="AppAccessToken"/>, acquiring or refreshing
    /// one when the cached token is stale. The cache-hit path completes synchronously,
    /// hence <see cref="ValueTask{TResult}"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait and the acquisition call.</param>
    ValueTask<AppAccessToken> GetTokenAsync(CancellationToken cancellationToken);
}
