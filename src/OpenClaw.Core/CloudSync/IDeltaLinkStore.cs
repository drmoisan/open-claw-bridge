namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Persistence seam for per-mailbox <c>@odata.deltaLink</c> values (table
/// <c>graph_delta_links</c>), implemented by the <c>CoreCacheRepository.DeltaLinks</c>
/// partial. The delta link is opaque: it is stored and replayed verbatim (master
/// §6.2). Timestamps are caller-supplied (clock-free contract).
/// </summary>
internal interface IDeltaLinkStore
{
    /// <summary>Returns the stored delta link for <paramref name="mailbox"/>, or null when absent.</summary>
    /// <param name="mailbox">The mailbox UPN keying the link.</param>
    /// <param name="ct">Cancels the operation.</param>
    Task<string?> GetDeltaLinkAsync(string mailbox, CancellationToken ct);

    /// <summary>Stores <paramref name="deltaLink"/> verbatim for <paramref name="mailbox"/> (upsert).</summary>
    /// <param name="mailbox">The mailbox UPN keying the link.</param>
    /// <param name="deltaLink">The opaque terminal delta link, stored verbatim.</param>
    /// <param name="updatedAtUtc">The caller-supplied write instant.</param>
    /// <param name="ct">Cancels the operation.</param>
    Task SetDeltaLinkAsync(
        string mailbox,
        string deltaLink,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct
    );
}
