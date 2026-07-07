namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Seam for generating the per-subscription <c>clientState</c> secret (D-5). The
/// production implementation is <see cref="CryptoClientStateGenerator"/>; tests
/// substitute a deterministic generator so subscription request shapes are pinnable.
/// </summary>
internal interface IClientStateGenerator
{
    /// <summary>
    /// Generates a fresh opaque <c>clientState</c> value, under Microsoft Graph's
    /// 128-character limit.
    /// </summary>
    string Generate();
}
