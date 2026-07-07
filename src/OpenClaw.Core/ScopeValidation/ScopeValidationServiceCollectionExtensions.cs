using Microsoft.Extensions.Options;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Opt-in DI registration for the scope-boundary startup validation (spec D6). Called
/// unconditionally from the composition root immediately after the backend-selection
/// block; the three branches keep existing configurations unaffected and fail fast on
/// misconfiguration:
/// <list type="bullet">
/// <item><c>OpenClaw:ScopeValidation:Enabled == false</c> (or the section absent) registers nothing.</item>
/// <item>Enabled with <c>OpenClaw:GraphAdapter:Enabled == false</c> throws at composition time (the probe is Graph-only).</item>
/// <item>Enabled with the Graph adapter enabled binds and validates options, registers the
/// typed probe client mapped to <see cref="IMailboxScopeProbe"/>, the
/// <see cref="ScopeBoundaryValidator"/>, and the <see cref="ScopeBoundaryStartupValidator"/>
/// hosted service.</item>
/// </list>
/// </summary>
public static class ScopeValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers scope-boundary validation per the D6 branches. <c>IAppTokenProvider</c>,
    /// <c>TimeProvider</c>, and the <see cref="GraphAdapterOptions"/> binding are already
    /// registered by <c>AddGraphHostAdapterClient</c>/<c>Program.cs</c> in the enabled
    /// path, so the probe's typed client reuses them.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The configuration root supplying <c>OpenClaw:ScopeValidation</c> and <c>OpenClaw:GraphAdapter</c>.</param>
    public static IServiceCollection AddScopeBoundaryValidation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetValue<bool>($"{ScopeValidationOptions.SectionName}:Enabled"))
        {
            return services;
        }

        if (!configuration.GetValue<bool>("OpenClaw:GraphAdapter:Enabled"))
        {
            throw new InvalidOperationException(
                "OpenClaw:ScopeValidation:Enabled is true but OpenClaw:GraphAdapter:Enabled is "
                    + "false. Scope-boundary validation exercises the Graph read path and is "
                    + "Graph-only; enable the Graph adapter or disable scope validation."
            );
        }

        services
            .AddOptions<ScopeValidationOptions>()
            .Bind(configuration.GetSection(ScopeValidationOptions.SectionName))
            .Validate(
                options => ScopeValidationOptionsValidator.Validate(options).Count == 0,
                "ScopeValidation options are invalid; see ScopeValidationOptionsValidator for the "
                    + "rules (both test-mailbox UPNs required, non-whitespace, and distinct "
                    + "OrdinalIgnoreCase)."
            )
            .ValidateOnStart();

        services.AddHttpClient<IMailboxScopeProbe, GraphMailboxScopeProbe>(
            (serviceProvider, client) =>
            {
                var graphOptions = serviceProvider
                    .GetRequiredService<IOptions<GraphAdapterOptions>>()
                    .Value;
                client.BaseAddress = new Uri(graphOptions.BaseUrl);
            }
        );

        services.AddTransient<ScopeBoundaryValidator>();
        services.AddHostedService<ScopeBoundaryStartupValidator>();

        return services;
    }
}
