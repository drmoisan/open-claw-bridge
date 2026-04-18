using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenClaw.HostAdapter.Tests;

/// <summary>
/// Unit tests for <see cref="FileHostAdapterTokenProvider.ReadExpectedToken"/>.
/// All filesystem access is replaced by the <c>FileReader</c> seam so that no real files
/// are created or read during test execution.
/// </summary>
[TestClass]
public class FileHostAdapterTokenProviderTests
{
    // ─── Guard-condition tests ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an empty <c>TokenFilePath</c> causes the method to return null
    /// without invoking the file reader.
    /// </summary>
    [TestMethod]
    public void ReadExpectedToken_WhenTokenFilePathIsEmpty_ReturnsNull()
    {
        // Arrange
        var provider = BuildProvider(tokenFilePath: string.Empty);

        // Act + Assert
        provider.ReadExpectedToken().Should().BeNull();
    }

    /// <summary>
    /// Verifies that a whitespace-only <c>TokenFilePath</c> causes the method to return null
    /// without invoking the file reader.
    /// </summary>
    [TestMethod]
    public void ReadExpectedToken_WhenTokenFilePathIsWhitespace_ReturnsNull()
    {
        // Arrange
        var provider = BuildProvider(tokenFilePath: "   ");

        // Act + Assert
        provider.ReadExpectedToken().Should().BeNull();
    }

    // ─── FileReader seam tests ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a null result from the file reader (file does not exist) is propagated
    /// as null from <c>ReadExpectedToken</c>.
    /// </summary>
    [TestMethod]
    public void ReadExpectedToken_WhenFileReaderReturnsNull_ReturnsNull()
    {
        // Arrange: the reader signals that the file does not exist
        var provider = BuildProvider(tokenFilePath: "any-path.token", fileReader: _ => null);

        // Act + Assert
        provider.ReadExpectedToken().Should().BeNull();
    }

    /// <summary>
    /// Verifies that whitespace surrounding the token content is stripped before returning.
    /// Token files commonly include a trailing newline, so trimming is a contract requirement.
    /// </summary>
    [TestMethod]
    public void ReadExpectedToken_WhenFileReaderReturnsTokenWithSurroundingWhitespace_ReturnsTrimmedToken()
    {
        // Arrange
        var provider = BuildProvider(
            tokenFilePath: "any-path.token",
            fileReader: _ => "  my-secret-token\n"
        );

        // Act
        var result = provider.ReadExpectedToken();

        // Assert: surrounding whitespace (including newline) must be stripped
        result.Should().Be("my-secret-token");
    }

    /// <summary>
    /// Verifies that a token with no surrounding whitespace is returned unchanged.
    /// </summary>
    [TestMethod]
    public void ReadExpectedToken_WhenFileReaderReturnsCleanToken_ReturnsThatToken()
    {
        // Arrange
        var provider = BuildProvider(
            tokenFilePath: "any-path.token",
            fileReader: _ => "clean-token"
        );

        // Act + Assert
        provider.ReadExpectedToken().Should().Be("clean-token");
    }

    /// <summary>
    /// Verifies that the configured <c>TokenFilePath</c> value is forwarded as-is to the
    /// file reader, so the reader receives the exact path from configuration.
    /// </summary>
    [TestMethod]
    public void ReadExpectedToken_PassesConfiguredPathToFileReader()
    {
        // Arrange: capture the path the reader is called with
        string? capturedPath = null;
        var provider = BuildProvider(
            tokenFilePath: "/configured/path/adapter.token",
            fileReader: path =>
            {
                capturedPath = path;
                return "token";
            }
        );

        // Act
        provider.ReadExpectedToken();

        // Assert
        capturedPath.Should().Be("/configured/path/adapter.token");
    }

    // ─── Build helper ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Constructs a <see cref="FileHostAdapterTokenProvider"/> with the given path and
    /// optional test-double reader. When <paramref name="fileReader"/> is null the
    /// provider uses its production default (filesystem-based).
    /// </summary>
    private static FileHostAdapterTokenProvider BuildProvider(
        string tokenFilePath,
        Func<string, string?>? fileReader = null
    )
    {
        var options = Options.Create(new HostAdapterOptions { TokenFilePath = tokenFilePath });

        // Inject the test-double reader when provided to avoid any filesystem access.
        return fileReader is not null
            ? new FileHostAdapterTokenProvider(options) { FileReader = fileReader }
            : new FileHostAdapterTokenProvider(options);
    }
}
