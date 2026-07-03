using Microsoft.Extensions.Options;
using OpenClaw.Core.CloudAuth;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Opt-in DI registration for the Graph-backed adapter (D8). The composition root
/// calls <see cref="AddGraphHostAdapterClient"/> only when
/// <c>OpenClaw:GraphAdapter:Enabled</c> is <c>true</c>; the default path keeps the
/// local <c>HostAdapterHttpClient</c> registration untouched.
/// </summary>
public static class GraphServiceCollectionExtensions
{
    /// <summary>
    /// Wires the Graph backend in one call: registers CloudAuth token acquisition
    /// (<c>AddCloudAuth</c>, whose XML doc names F13 as its consumer), binds
    /// <see cref="GraphAdapterOptions"/> from <c>OpenClaw:GraphAdapter</c> with
    /// fail-closed startup validation, and registers the typed
    /// <c>IHostAdapterClient</c> HTTP client whose <c>BaseAddress</c> comes from
    /// <see cref="GraphAdapterOptions.BaseUrl"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The configuration root supplying <c>OpenClaw:GraphAdapter</c> and <c>OpenClaw:CloudAuth</c>.</param>
    public static IServiceCollection AddGraphHostAdapterClient(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddCloudAuth(configuration);

        services
            .AddOptions<GraphAdapterOptions>()
            .Bind(configuration.GetSection("OpenClaw:GraphAdapter"))
            .Validate(
                options => GraphAdapterOptionsValidator.Validate(options).Count == 0,
                "GraphAdapter options are invalid; see GraphAdapterOptionsValidator for the "
                    + "rules (required principal/assistant UPNs, absolute https BaseUrl, and "
                    + "the documented numeric bounds)."
            )
            .ValidateOnStart();

        services.AddHttpClient<IHostAdapterClient, GraphHostAdapterClient>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<GraphAdapterOptions>>()
                    .Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            }
        );

        return services;
    }
}
