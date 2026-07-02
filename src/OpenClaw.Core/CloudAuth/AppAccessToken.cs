namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// An acquired app-only access token and its expiry (D3).
/// </summary>
/// <param name="Token">The raw bearer token value. Never logged or printed.</param>
/// <param name="ExpiresOn">The UTC instant at which the token expires.</param>
/// <remarks>
/// The record-generated <c>ToString()</c> would print every positional property,
/// including <paramref name="Token"/>, so any log statement or string interpolation of
/// the record would leak the secret. The override below is fail-closed against that
/// accidental disclosure: it emits only the expiry timestamp.
/// </remarks>
public sealed record AppAccessToken(string Token, DateTimeOffset ExpiresOn)
{
    /// <summary>Redacting representation: expiry only, never the token value.</summary>
    public override string ToString() => $"AppAccessToken(ExpiresOn: {ExpiresOn:O})";
}
