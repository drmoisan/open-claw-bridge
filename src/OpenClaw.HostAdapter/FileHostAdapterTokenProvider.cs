using Microsoft.Extensions.Options;

namespace OpenClaw.HostAdapter;

internal sealed class FileHostAdapterTokenProvider(IOptions<HostAdapterOptions> optionsAccessor)
    : IHostAdapterTokenProvider
{
    private readonly HostAdapterOptions options = optionsAccessor.Value;

    /// <summary>
    /// Reads raw file content from the given path, or returns null when the file does not exist.
    /// Replaced in unit tests to avoid filesystem access.
    /// </summary>
    internal Func<string, string?> FileReader { get; init; } =
        static path => File.Exists(path) ? File.ReadAllText(path) : null;

    public string? ReadExpectedToken()
    {
        if (string.IsNullOrWhiteSpace(options.TokenFilePath))
        {
            return null;
        }

        return FileReader(options.TokenFilePath)?.Trim();
    }
}
