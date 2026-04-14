using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

internal sealed class StatusCacheService(
    IMemoryCache memoryCache,
    HostAdapterCommandBuilder commandBuilder,
    IHostAdapterProcessRunner processRunner,
    IOptions<HostAdapterOptions> optionsAccessor
)
{
    private const string CacheKey = "OpenClaw.HostAdapter.StatusCache";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);
    private readonly HostAdapterOptions options = optionsAccessor.Value;
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public async Task<AdapterCommandResult<BridgeStatusDto>> GetStatusAsync(
        string requestId,
        CancellationToken cancellationToken
    )
    {
        if (
            memoryCache.TryGetValue<BridgeStatusDto>(CacheKey, out var cachedStatus)
            && cachedStatus is not null
        )
        {
            return HostAdapterResponses.Success(
                cachedStatus,
                requestId,
                options.AdapterVersion,
                cachedStatus
            );
        }

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (
                memoryCache.TryGetValue<BridgeStatusDto>(CacheKey, out cachedStatus)
                && cachedStatus is not null
            )
            {
                return HostAdapterResponses.Success(
                    cachedStatus,
                    requestId,
                    options.AdapterVersion,
                    cachedStatus
                );
            }

            var result = await processRunner.ExecuteAsync(
                commandBuilder.BuildStatus(),
                requestId,
                null,
                HostAdapterProcessRunner.DeserializePayload<BridgeStatusDto>,
                cancellationToken
            );

            if (result.StatusCode == StatusCodes.Status200OK && result.Envelope.Data is not null)
            {
                memoryCache.Set(CacheKey, result.Envelope.Data, CacheTtl);
            }

            return result;
        }
        finally
        {
            refreshLock.Release();
        }
    }
}
