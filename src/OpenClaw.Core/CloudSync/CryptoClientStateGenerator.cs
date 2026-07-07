using System.Buffers.Text;
using System.Security.Cryptography;

namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Production <see cref="IClientStateGenerator"/> (D-5): 32 cryptographically random
/// bytes from <see cref="RandomNumberGenerator"/> encoded base64url (43 characters,
/// well under Graph's 128-character <c>clientState</c> limit). No <c>Random.Shared</c>
/// or other banned randomness APIs.
/// </summary>
internal sealed class CryptoClientStateGenerator : IClientStateGenerator
{
    private const int RandomByteCount = 32;

    /// <inheritdoc />
    public string Generate()
    {
        Span<byte> bytes = stackalloc byte[RandomByteCount];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }
}
