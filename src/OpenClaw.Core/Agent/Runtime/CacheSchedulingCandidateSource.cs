using Microsoft.Extensions.Options;

namespace OpenClaw.Core.Agent.Runtime;

/// <summary>
/// Repository-backed <see cref="ISchedulingCandidateSource"/> that supplies recent
/// meeting-message identifiers from the local cache populated by the existing polling
/// workers (OR-3). Part of the runtime seam.
/// </summary>
internal sealed class CacheSchedulingCandidateSource(
    CoreCacheRepository repository,
    IOptions<OpenClawOptions> optionsAccessor
) : ISchedulingCandidateSource
{
    private readonly OpenClawOptions options = optionsAccessor.Value;

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCandidateMessageIdsAsync(
        CancellationToken cancellationToken
    )
    {
        var sinceUtc = DateTimeOffset.UtcNow.AddHours(-options.Polling.MessageLookbackHours);
        var messages = await repository
            .ListMessagesAsync("meeting", sinceUtc, options.Defaults.Limit)
            .ConfigureAwait(false);
        return messages.Select(message => message.BridgeId).ToList();
    }
}
