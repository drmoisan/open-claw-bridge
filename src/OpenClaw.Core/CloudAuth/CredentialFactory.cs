using Azure.Core;
using Azure.Identity;

namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// The single <c>Azure.Identity</c> instantiation site in the module (D4/D5). Builds
/// the concrete <see cref="TokenCredential"/> from already-validated
/// <see cref="CloudAuthOptions"/>: certificate-first when <c>CertificatePath</c> is
/// set, client secret as the documented fallback. No other file may reference
/// <see cref="ClientCertificateCredential"/> or <see cref="ClientSecretCredential"/>.
/// </summary>
internal static class CredentialFactory
{
    /// <summary>
    /// Builds the credential for the configured source. The caller guarantees the
    /// options passed <see cref="CloudAuthOptionsValidator.Validate"/>, so exactly one
    /// of <c>CertificatePath</c> / <c>ClientSecret</c> is set and
    /// <c>AuthorityHost</c> is an absolute https URI.
    /// </summary>
    /// <param name="options">Validated options selecting the credential source.</param>
    internal static TokenCredential Create(CloudAuthOptions options)
    {
        var credentialOptions = new ClientCertificateCredentialOptions
        {
            AuthorityHost = new Uri(options.AuthorityHost),
        };

        if (!string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            return new ClientCertificateCredential(
                options.TenantId,
                options.ClientId,
                options.CertificatePath,
                credentialOptions
            );
        }

        return new ClientSecretCredential(
            options.TenantId,
            options.ClientId,
            options.ClientSecret,
            new ClientSecretCredentialOptions { AuthorityHost = new Uri(options.AuthorityHost) }
        );
    }
}
