namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// Pure freshness predicate for cached tokens (D6). Kept as a standalone pure function
/// so it is property-testable (CsCheck, T1 obligation) and trivially mutation-testable.
/// </summary>
internal static class TokenFreshness
{
    /// <summary>
    /// A token is fresh iff it exists and <paramref name="nowUtc"/> is strictly before
    /// <c>token.ExpiresOn - skew</c>. Boundary semantics: at exactly
    /// <c>ExpiresOn - skew</c> the token is stale (the comparison is strict), so a
    /// returned token always satisfies <c>now &lt; ExpiresOn - skew</c> at return time.
    /// </summary>
    /// <param name="token">The cached token, or <see langword="null"/> when none is cached.</param>
    /// <param name="nowUtc">The current UTC instant per the injected <see cref="TimeProvider"/>.</param>
    /// <param name="skew">The refresh skew subtracted from the expiry.</param>
    internal static bool IsFresh(AppAccessToken? token, DateTimeOffset nowUtc, TimeSpan skew) =>
        token is not null && nowUtc < token.ExpiresOn - skew;
}
