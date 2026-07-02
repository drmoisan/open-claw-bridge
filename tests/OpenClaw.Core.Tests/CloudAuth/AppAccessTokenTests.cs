using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudAuth;

namespace OpenClaw.Core.Tests.CloudAuth;

/// <summary>
/// Verifies the D3 redaction contract of <see cref="AppAccessToken"/>: the token value
/// never appears in <c>ToString()</c> or interpolation output, while record value
/// semantics are preserved.
/// </summary>
[TestClass]
public sealed class AppAccessTokenTests
{
    private const string FakeToken = "fake-token-value";

    private static readonly DateTimeOffset Expiry = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [TestMethod]
    public void ToString_ContainsIso8601ExpiryOnly_NotTheTokenValue()
    {
        // Arrange
        var token = new AppAccessToken(FakeToken, Expiry);

        // Act
        var text = token.ToString();

        // Assert
        text.Should()
            .Contain(
                Expiry.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                "the redacting ToString() must expose the ISO-8601 expiry for diagnostics"
            );
        text.Should()
            .NotContain(FakeToken, "the token value must never appear in ToString() output");
    }

    [TestMethod]
    public void StringInterpolation_OfTheRecord_DoesNotLeakTheTokenValue()
    {
        // Arrange
        var token = new AppAccessToken(FakeToken, Expiry);

        // Act
        var interpolated = $"acquired: {token}";

        // Assert
        interpolated
            .Should()
            .NotContain(FakeToken, "interpolating the record routes through ToString()");
        interpolated.Should().Contain("AppAccessToken(ExpiresOn:");
    }

    [TestMethod]
    public void Equality_SameTokenAndExpiry_AreEqual()
    {
        // Arrange
        var first = new AppAccessToken(FakeToken, Expiry);
        var second = new AppAccessToken(FakeToken, Expiry);

        // Act + Assert
        first
            .Should()
            .Be(second, "records with identical Token and ExpiresOn must be value-equal");
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [TestMethod]
    public void Equality_DifferentTokenValue_AreNotEqual()
    {
        // Arrange
        var first = new AppAccessToken(FakeToken, Expiry);
        var second = new AppAccessToken("other-fake-token-value", Expiry);

        // Act + Assert
        first.Should().NotBe(second, "a different Token value must break value equality");
    }

    [TestMethod]
    public void Equality_DifferentExpiry_AreNotEqual()
    {
        // Arrange
        var first = new AppAccessToken(FakeToken, Expiry);
        var second = new AppAccessToken(FakeToken, Expiry.AddMinutes(1));

        // Act + Assert
        first.Should().NotBe(second, "a different ExpiresOn must break value equality");
    }
}
