using Microsoft.Extensions.Options;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Opt-in DI registration for the CloudSync eventing module (issue #117, D-6). The
/// composition root calls <see cref="AddCloudSync"/> only when
/// <c>OpenClaw:CloudSync:Enabled</c> is <c>true</c>; the flag-absent default path
/// registers nothing from this module. Mirrors
/// <see cref="GraphServiceCollectionExtensions.AddGraphHostAdapterClient"/>: options
/// bind + fail-closed <c>ValidateOnStart</c> (D-7), typed <c>HttpClient</c>
/// registrations whose <c>BaseAddress</c> comes from
/// <see cref="GraphAdapterOptions.BaseUrl"/>, and the three hosted workers.
/// </summary>
public static class CloudSyncServiceCollectionExtensions
{
    /// <summary>
    /// Wires CloudSync in one call: binds <see cref="CloudSyncOptions"/> from
    /// <c>OpenClaw:CloudSync</c> with fail-closed startup validation (including the
    /// D-7 <c>OpenClaw:GraphAdapter:Enabled</c> cross-check), registers the queue,
    /// clientState generator, repository-backed stores, notification processor, typed
    /// Graph HTTP clients, and the renewal/reconciliation/dispatch workers.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The configuration root supplying <c>OpenClaw:CloudSync</c>.</param>
    public static IServiceCollection AddCloudSync(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CloudSyncOptions>()
            .Bind(configuration.GetSection(CloudSyncOptions.SectionName))
            .Validate(
                options =>
                    CloudSyncOptionsValidator
                        .Validate(
                            options,
                            configuration.GetValue<bool>("OpenClaw:GraphAdapter:Enabled")
                        )
                        .Count == 0,
                "CloudSync options are invalid; see CloudSyncOptionsValidator for the rules "
                    + "(Graph backend required, absolute https NotificationUrl, and the "
                    + "documented numeric bounds)."
            )
            .ValidateOnStart();

        services.AddSingleton<INotificationQueue, ChannelNotificationQueue>();
        services.AddSingleton<IClientStateGenerator, CryptoClientStateGenerator>();
        services.AddSingleton<ISubscriptionStore>(sp =>
            sp.GetRequiredService<CoreCacheRepository>()
        );
        services.AddSingleton<IDeltaLinkStore>(sp => sp.GetRequiredService<CoreCacheRepository>());
        services.AddSingleton<NotificationRequestProcessor>();
        services.AddSingleton<IDeltaReconcileTrigger>(sp =>
            sp.GetRequiredService<GraphDeltaReconciler>()
        );

        services.AddHttpClient<GraphSubscriptionManager>(ConfigureGraphBaseAddress);
        services.AddHttpClient<GraphDeltaReconciler>(ConfigureGraphBaseAddress);

        services.AddHostedService<SubscriptionRenewalWorker>();
        services.AddHostedService<DeltaReconciliationWorker>();
        services.AddHostedService<NotificationDispatchWorker>();

        return services;
    }

    private static void ConfigureGraphBaseAddress(
        IServiceProvider serviceProvider,
        HttpClient client
    )
    {
        var options = serviceProvider.GetRequiredService<IOptions<GraphAdapterOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    }
}
