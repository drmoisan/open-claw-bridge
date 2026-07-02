namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// Raised when app-only token acquisition fails for a reason other than cancellation
/// (D7). Carries tenant/client/scope context so consumers can diagnose the failure
/// without depending on <c>Azure.Identity</c> exception types. The message is built
/// exclusively from those three identifiers; token, secret, and certificate material
/// never appear in the message or the properties.
/// </summary>
public sealed class TokenAcquisitionException : Exception
{
    /// <summary>
    /// Creates the failure with tenant/client/scope context and the underlying cause.
    /// </summary>
    /// <param name="tenantId">The Entra tenant id the acquisition targeted.</param>
    /// <param name="clientId">The app registration (client) id used.</param>
    /// <param name="scope">The requested scope (for example the Graph <c>.default</c> scope).</param>
    /// <param name="innerException">The underlying credential failure, preserved.</param>
    public TokenAcquisitionException(
        string tenantId,
        string clientId,
        string scope,
        Exception innerException
    )
        : base(
            $"App-only token acquisition failed for tenant '{tenantId}', client '{clientId}', scope '{scope}'. See the inner exception for the underlying cause.",
            innerException
        )
    {
        TenantId = tenantId;
        ClientId = clientId;
        Scope = scope;
    }

    /// <summary>The Entra tenant id the acquisition targeted.</summary>
    public string TenantId { get; }

    /// <summary>The app registration (client) id used.</summary>
    public string ClientId { get; }

    /// <summary>The requested scope.</summary>
    public string Scope { get; }
}
