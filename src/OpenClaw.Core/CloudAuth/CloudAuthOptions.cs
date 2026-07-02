namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// App-only (client-credentials) auth configuration (D5). Bound from the
/// <c>OpenClaw:CloudAuth</c> configuration section. This type is a plain options bag;
/// all validation lives in <see cref="CloudAuthOptionsValidator"/>.
/// </summary>
public sealed class CloudAuthOptions
{
    /// <summary>
    /// The Entra tenant id. Required, non-whitespace. Env binding:
    /// <c>OpenClaw__CloudAuth__TenantId</c>.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The app registration (client) id. Required, non-whitespace. Env binding:
    /// <c>OpenClaw__CloudAuth__ClientId</c>.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Path to the client certificate file (certificate-first, the preferred credential
    /// source). Exactly one of <see cref="CertificatePath"/> and
    /// <see cref="ClientSecret"/> must be configured. Env binding:
    /// <c>OpenClaw__CloudAuth__CertificatePath</c>.
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// The client secret value (documented fallback to certificate auth). Exactly one
    /// of <see cref="CertificatePath"/> and <see cref="ClientSecret"/> must be
    /// configured. Env binding: <c>OpenClaw__CloudAuth__ClientSecret</c>.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The token scope; must be an absolute URI ending with <c>/.default</c> (the
    /// client-credentials flow cannot serve delegated-style scopes). Env binding:
    /// <c>OpenClaw__CloudAuth__Scope</c>.
    /// </summary>
    public string Scope { get; set; } = "https://graph.microsoft.com/.default";

    /// <summary>
    /// The Entra authority host; must be an absolute <c>https</c> URI. Override exists
    /// only for national-cloud endpoints. Env binding:
    /// <c>OpenClaw__CloudAuth__AuthorityHost</c>.
    /// </summary>
    public string AuthorityHost { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// Minutes before <c>ExpiresOn</c> at which a cached token counts as stale
    /// (<c>0 &lt;= value &lt;= 60</c>). Env binding:
    /// <c>OpenClaw__CloudAuth__RefreshSkewMinutes</c>.
    /// </summary>
    public int RefreshSkewMinutes { get; set; } = 5;
}
