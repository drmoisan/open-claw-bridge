using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace OpenClaw.Core.Tests;

internal sealed class CoreTestWebApplicationFactory(
    Action<OpenClaw.Core.CoreHealthState>? configureHealthState
) : WebApplicationFactory<Program>
{
    private readonly string dbPath = $"core-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    public async Task InitializeRepositoryAsync(
        Func<OpenClaw.Core.CoreCacheRepository, Task> seedAsync
    )
    {
        using var scope = Services.CreateScope();
        var repository =
            scope.ServiceProvider.GetRequiredService<OpenClaw.Core.CoreCacheRepository>();
        await repository.InitializeAsync();
        await seedAsync(repository);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["OpenClaw:HostAdapter:BaseUrl"] = "http://127.0.0.1:4319/",
                        ["OpenClaw:HostAdapter:TokenFile"] = "/run/openclaw/hostadapter.token",
                        ["OpenClaw:Storage:DbPath"] = dbPath,
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            foreach (
                var descriptor in services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService)
                        && (
                            descriptor.ImplementationType
                                == typeof(OpenClaw.Core.MessagePollingWorker)
                            || descriptor.ImplementationType
                                == typeof(OpenClaw.Core.CalendarPollingWorker)
                        )
                    )
                    .ToArray()
            )
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<OpenClaw.Core.CoreHealthState>();
            var healthState = new OpenClaw.Core.CoreHealthState();
            configureHealthState?.Invoke(healthState);
            services.AddSingleton(healthState);
        });
    }
}
