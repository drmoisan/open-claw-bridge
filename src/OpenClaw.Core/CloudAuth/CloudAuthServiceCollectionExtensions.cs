using Microsoft.Extensions.Options;

namespace OpenClaw.Core.CloudAuth;

/// <summary>
/// Opt-in DI registration for the CloudAuth module (D8). Nothing in the running
/// application calls <see cref="AddCloudAuth"/>; absence of registration is the gate.
/// F13 (the Graph-backed adapter) will opt in when it lands.
/// </summary>
public static class CloudAuthServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="CloudAuthOptions"/> from the <c>OpenClaw:CloudAuth</c> section
    /// (env form <c>OpenClaw__CloudAuth__*</c>), validates fail-closed at startup via
    /// <see cref="CloudAuthOptionsValidator"/> with <c>ValidateOnStart()</c>, and
    /// registers <see cref="IAppTokenProvider"/> as a singleton
    /// <see cref="ClientCredentialsTokenProvider"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The configuration root supplying <c>OpenClaw:CloudAuth</c>.</param>
    public static IServiceCollection AddCloudAuth(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CloudAuthOptions>()
            .Bind(configuration.GetSection("OpenClaw:CloudAuth"))
            .Validate(
                options => CloudAuthOptionsValidator.Validate(options).Count == 0,
                "CloudAuth options are invalid; see CloudAuthOptionsValidator for the rules "
                    + "(required TenantId/ClientId, exactly one of CertificatePath/ClientSecret, "
                    + "absolute '/.default' scope, https authority, skew 0-60 minutes)."
            )
            .ValidateOnStart();

        services.AddSingleton<IAppTokenProvider>(sp => new ClientCredentialsTokenProvider(
            sp.GetRequiredService<IOptions<CloudAuthOptions>>().Value,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<ClientCredentialsTokenProvider>>()
        ));

        return services;
    }
}
