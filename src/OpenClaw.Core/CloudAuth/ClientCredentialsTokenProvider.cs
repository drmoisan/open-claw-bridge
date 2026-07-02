using Azure.Core;
using Microsoft.Extensions.Logging;

namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// The <c>Azure.Identity</c>-backed <see cref="IAppTokenProvider"/> (D4/D6/D7):
/// client-credentials acquisition with an in-process cached token, expiry skew on the
/// injected <see cref="TimeProvider"/>, and single-flight refresh. No Azure type
/// appears on the public surface; failures cross the boundary as
/// <see cref="TokenAcquisitionException"/>.
/// </summary>
public sealed class ClientCredentialsTokenProvider : IAppTokenProvider
{
    private readonly TokenCredential _credential;
    private readonly CloudAuthOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ClientCredentialsTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeSpan _skew;

    private AppAccessToken? _cachedToken;

    /// <summary>
    /// Production constructor: validates <paramref name="options"/> fail-closed (any
    /// violation throws before a credential is built) and builds the certificate or
    /// secret credential via <see cref="CredentialFactory"/>.
    /// </summary>
    /// <param name="options">The bound <c>OpenClaw:CloudAuth</c> options.</param>
    /// <param name="timeProvider">Clock seam for all expiry math.</param>
    /// <param name="logger">Logs refresh (Debug, expiry only) and failure (Error, no secrets).</param>
    /// <exception cref="ArgumentException">One or more option violations; the message carries all of them.</exception>
    public ClientCredentialsTokenProvider(
        CloudAuthOptions options,
        TimeProvider timeProvider,
        ILogger<ClientCredentialsTokenProvider> logger
    )
        : this(CreateValidatedCredential(options), options, timeProvider, logger) { }

    /// <summary>
    /// Test seam (D4): accepts the abstract <see cref="TokenCredential"/> directly so
    /// tests can mock acquisition. Reachable from tests via the existing
    /// <c>InternalsVisibleTo("OpenClaw.Core.Tests")</c>.
    /// </summary>
    internal ClientCredentialsTokenProvider(
        TokenCredential credential,
        CloudAuthOptions options,
        TimeProvider timeProvider,
        ILogger<ClientCredentialsTokenProvider> logger
    )
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _credential = credential;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
        _skew = TimeSpan.FromMinutes(options.RefreshSkewMinutes);
    }

    /// <inheritdoc />
    public ValueTask<AppAccessToken> GetTokenAsync(CancellationToken cancellationToken)
    {
        // Fast path: the cached token is fresh per the injected clock — complete
        // synchronously (the dominant case; hence ValueTask).
        var cached = Volatile.Read(ref _cachedToken);
        if (TokenFreshness.IsFresh(cached, _timeProvider.GetUtcNow(), _skew))
        {
            return ValueTask.FromResult(cached!);
        }

        return new ValueTask<AppAccessToken>(RefreshAsync(cancellationToken));
    }

    private async Task<AppAccessToken> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check under the lock: a concurrent caller may have refreshed
            // while this caller waited (single-flight — one credential call per
            // staleness window).
            var cached = Volatile.Read(ref _cachedToken);
            if (TokenFreshness.IsFresh(cached, _timeProvider.GetUtcNow(), _skew))
            {
                return cached!;
            }

            AppAccessToken refreshed;
            try
            {
                var accessToken = await _credential
                    .GetTokenAsync(new TokenRequestContext([_options.Scope]), cancellationToken)
                    .ConfigureAwait(false);
                refreshed = new AppAccessToken(accessToken.Token, accessToken.ExpiresOn);
            }
            catch (OperationCanceledException)
            {
                // D7: cancellation propagates unwrapped; the cache is untouched.
                throw;
            }
            catch (Exception ex)
            {
                // D7 fail-closed: never serve a stale cached token after a failed
                // refresh; surface the failure with non-secret context only.
                _logger.LogError(
                    ex,
                    "App-only token acquisition failed for tenant {TenantId}, client {ClientId}, scope {Scope}.",
                    _options.TenantId,
                    _options.ClientId,
                    _options.Scope
                );
                throw new TokenAcquisitionException(
                    _options.TenantId,
                    _options.ClientId,
                    _options.Scope,
                    ex
                );
            }

            Volatile.Write(ref _cachedToken, refreshed);
            _logger.LogDebug(
                "App-only token refreshed; expires {ExpiresOn:O}.",
                refreshed.ExpiresOn
            );
            return refreshed;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static TokenCredential CreateValidatedCredential(CloudAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var violations = CloudAuthOptionsValidator.Validate(options);
        if (violations.Count > 0)
        {
            throw new ArgumentException(
                "CloudAuth options are invalid: " + string.Join(" ", violations),
                nameof(options)
            );
        }

        return CredentialFactory.Create(options);
    }
}
