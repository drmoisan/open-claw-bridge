using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudAuth;

namespace OpenClaw.Core.Tests.CloudAuth;

/// <summary>
/// Boundary and property tests for the pure D6 predicate
/// <see cref="TokenFreshness.IsFresh"/>: null token is stale, freshness flips exactly
/// at <c>ExpiresOn - skew</c> (stale at the boundary instant), zero-skew behavior, and
/// CsCheck properties (T1 obligation). No wall-clock reads anywhere — all instants are
/// explicit constants.
/// </summary>
[TestClass]
public sealed class TokenFreshnessTests
{
    private const string FakeToken = "fake-token-value";

    private static readonly DateTimeOffset ExpiresOn = new(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    private static readonly long MinInstantTicks = new DateTimeOffset(
        2000,
        1,
        1,
        0,
        0,
        0,
        TimeSpan.Zero
    ).Ticks;

    private static readonly long MaxInstantTicks = new DateTimeOffset(
        2100,
        1,
        1,
        0,
        0,
        0,
        TimeSpan.Zero
    ).Ticks;

    private static AppAccessToken Token() => new(FakeToken, ExpiresOn);

    [TestMethod]
    public void IsFresh_NullToken_IsStale()
    {
        // Arrange + Act
        var fresh = TokenFreshness.IsFresh(token: null, nowUtc: ExpiresOn - Skew - Skew, Skew);

        // Assert
        fresh.Should().BeFalse("no cached token means an acquisition is always required");
    }

    [TestMethod]
    public void IsFresh_OneTickBeforeExpiryMinusSkew_IsFresh()
    {
        // Arrange
        var oneTickBeforeBoundary = ExpiresOn - Skew - TimeSpan.FromTicks(1);

        // Act
        var fresh = TokenFreshness.IsFresh(Token(), oneTickBeforeBoundary, Skew);

        // Assert
        fresh.Should().BeTrue("now is strictly before ExpiresOn - skew");
    }

    [TestMethod]
    public void IsFresh_AtExactlyExpiryMinusSkew_IsStale()
    {
        // Arrange
        var boundary = ExpiresOn - Skew;

        // Act
        var fresh = TokenFreshness.IsFresh(Token(), boundary, Skew);

        // Assert
        fresh
            .Should()
            .BeFalse("the comparison is strict: at exactly ExpiresOn - skew the token is stale");
    }

    [TestMethod]
    public void IsFresh_AfterExpiry_IsStale()
    {
        // Arrange
        var afterExpiry = ExpiresOn + TimeSpan.FromMinutes(1);

        // Act
        var fresh = TokenFreshness.IsFresh(Token(), afterExpiry, Skew);

        // Assert
        fresh.Should().BeFalse("a token past its expiry is stale under any skew");
    }

    [TestMethod]
    public void IsFresh_ZeroSkew_FreshUntilExactlyExpiresOn()
    {
        // Arrange + Act + Assert: with zero skew the boundary instant is ExpiresOn itself.
        TokenFreshness
            .IsFresh(Token(), ExpiresOn - TimeSpan.FromTicks(1), TimeSpan.Zero)
            .Should()
            .BeTrue("one tick before ExpiresOn with zero skew is fresh");
        TokenFreshness
            .IsFresh(Token(), ExpiresOn, TimeSpan.Zero)
            .Should()
            .BeFalse("at exactly ExpiresOn with zero skew the token is stale");
    }

    /// <summary>
    /// CsCheck property (T1): freshness is monotone in time — if a token is fresh at
    /// instant <c>t</c>, it is fresh at every earlier instant.
    /// </summary>
    [TestMethod]
    public void IsFresh_Property_FreshAtTImpliesFreshAtEveryEarlierInstant()
    {
        // Bounded instants (years 2000-2100 as UTC ticks) keep the arithmetic inside
        // the valid DateTimeOffset range while still exercising both sides of the
        // freshness boundary.
        var genCase = Gen.Select(
            Gen.Long[MinInstantTicks, MaxInstantTicks],
            Gen.Long[0, TimeSpan.TicksPerHour],
            Gen.Long[0, TimeSpan.TicksPerDay],
            (nowTicks, skewTicks, earlierByTicks) => (nowTicks, skewTicks, earlierByTicks)
        );

        genCase.Sample(
            c =>
            {
                var token = Token();
                var now = new DateTimeOffset(c.nowTicks, TimeSpan.Zero);
                var skew = TimeSpan.FromTicks(c.skewTicks);

                if (TokenFreshness.IsFresh(token, now, skew))
                {
                    var earlier = now - TimeSpan.FromTicks(c.earlierByTicks);
                    TokenFreshness
                        .IsFresh(token, earlier, skew)
                        .Should()
                        .BeTrue("freshness at t implies freshness at every earlier instant");
                }
            },
            iter: 1000
        );
    }

    /// <summary>
    /// CsCheck property (T1): <see cref="TokenFreshness.IsFresh"/> agrees with the
    /// definitional inequality <c>token != null &amp;&amp; now &lt; ExpiresOn - skew</c>
    /// for arbitrary token/now/skew combinations (including null tokens).
    /// </summary>
    [TestMethod]
    public void IsFresh_Property_AgreesWithDefinitionalInequality()
    {
        var genCase = Gen.Select(
            Gen.Bool,
            Gen.Long[MinInstantTicks, MaxInstantTicks],
            Gen.Long[MinInstantTicks, MaxInstantTicks],
            Gen.Long[0, TimeSpan.TicksPerHour],
            (hasToken, expiresOnTicks, nowTicks, skewTicks) =>
                (hasToken, expiresOnTicks, nowTicks, skewTicks)
        );

        genCase.Sample(
            c =>
            {
                var expiresOn = new DateTimeOffset(c.expiresOnTicks, TimeSpan.Zero);
                var now = new DateTimeOffset(c.nowTicks, TimeSpan.Zero);
                var token = c.hasToken ? new AppAccessToken(FakeToken, expiresOn) : null;
                var skew = TimeSpan.FromTicks(c.skewTicks);

                var expected = token is not null && now < token.ExpiresOn - skew;

                TokenFreshness
                    .IsFresh(token, now, skew)
                    .Should()
                    .Be(expected, "IsFresh must equal the D6 definitional inequality");
            },
            iter: 1000
        );
    }
}
