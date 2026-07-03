using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Unit tests for <see cref="CryptoClientStateGenerator"/> (D-5): the generated
/// <c>clientState</c> is non-empty, uses only the base64url alphabet, stays under
/// Graph's 128-character limit, and consecutive generations differ.
/// </summary>
[TestClass]
public sealed class CryptoClientStateGeneratorTests
{
    private const string Base64UrlAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    [TestMethod]
    public void Generate_returns_a_non_empty_value()
    {
        // Arrange
        var generator = new CryptoClientStateGenerator();

        // Act
        var clientState = generator.Generate();

        // Assert
        clientState.Should().NotBeNullOrWhiteSpace("the clientState is a required secret");
    }

    [TestMethod]
    public void Generate_uses_only_the_base64url_alphabet()
    {
        // Arrange
        var generator = new CryptoClientStateGenerator();

        // Act
        var clientState = generator.Generate();

        // Assert
        clientState
            .Should()
            .MatchRegex(
                "^[A-Za-z0-9_-]+$",
                "base64url uses A-Z, a-z, 0-9, '-' and '_' with no padding"
            );
        clientState.ToCharArray().Should().OnlyContain(c => Base64UrlAlphabet.Contains(c));
    }

    [TestMethod]
    public void Generate_stays_under_the_graph_128_character_limit()
    {
        // Arrange
        var generator = new CryptoClientStateGenerator();

        // Act
        var clientState = generator.Generate();

        // Assert
        clientState
            .Length.Should()
            .BeLessThan(128, "Graph rejects clientState values of 128 characters or more");
    }

    [TestMethod]
    public void Generate_twice_returns_different_values()
    {
        // Arrange
        var generator = new CryptoClientStateGenerator();

        // Act
        var first = generator.Generate();
        var second = generator.Generate();

        // Assert
        second
            .Should()
            .NotBe(first, "32 random bytes make a collision across two calls implausible");
    }
}
