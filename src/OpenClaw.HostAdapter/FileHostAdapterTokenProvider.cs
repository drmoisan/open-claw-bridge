using Microsoft.Extensions.Options;

namespace OpenClaw.HostAdapter;

internal sealed class FileHostAdapterTokenProvider(IOptions<HostAdapterOptions> optionsAccessor)
    : IHostAdapterTokenProvider
{
    private readonly HostAdapterOptions options = optionsAccessor.Value;

    public string? ReadExpectedToken()
    {
        if (string.IsNullOrWhiteSpace(options.TokenFilePath) || !File.Exists(options.TokenFilePath))
        {
            return null;
        }

        return File.ReadAllText(options.TokenFilePath).Trim();
    }
}
