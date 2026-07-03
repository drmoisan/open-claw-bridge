using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// CsCheck property tests for the pure constant-time clientState comparison helper
/// <see cref="NotificationRequestProcessor.ClientStateMatches"/> (T1 property-test
/// rule): the comparison returns true iff the candidate equals the stored value,
/// arbitrary unequal strings (including equal-length ones) return false, and
/// null/empty candidates return false without throwing. Failing seeds are reported by
/// CsCheck's default output.
/// </summary>
[TestClass]
public sealed class ClientStatePropertyTests
{
    [TestMethod]
    public void Comparison_is_true_iff_the_candidate_equals_the_stored_value()
    {
        Gen.String[1, 64]
            .Select(Gen.String[0, 64])
            .Sample(pair =>
            {
                var (stored, candidate) = pair;

                var matches = NotificationRequestProcessor.ClientStateMatches(candidate, stored);

                matches
                    .Should()
                    .Be(
                        candidate.Length > 0
                            && string.Equals(candidate, stored, StringComparison.Ordinal),
                        "the comparison must agree with ordinal string equality for non-empty candidates"
                    );
            });
    }

    [TestMethod]
    public void Identical_candidate_and_stored_value_always_match()
    {
        Gen.String[1, 64]
            .Sample(secret =>
            {
                NotificationRequestProcessor
                    .ClientStateMatches(secret, secret)
                    .Should()
                    .BeTrue("a candidate equal to the stored value must match");
            });
    }

    [TestMethod]
    public void Unequal_strings_of_equal_length_never_match()
    {
        // Equal-length inputs exercise the byte-by-byte fixed-time path rather than
        // the trivial length short-circuit.
        Gen.String[8, 8]
            .Select(Gen.String[8, 8])
            .Where(pair => !string.Equals(pair.Item1, pair.Item2, StringComparison.Ordinal))
            .Sample(pair =>
            {
                var (stored, candidate) = pair;

                NotificationRequestProcessor
                    .ClientStateMatches(candidate, stored)
                    .Should()
                    .BeFalse("distinct equal-length values must not match");
            });
    }

    [TestMethod]
    public void Null_and_empty_candidates_return_false_without_throwing()
    {
        Gen.String[0, 64]
            .Sample(stored =>
            {
                var nullCandidate = () =>
                    NotificationRequestProcessor.ClientStateMatches(null, stored);
                var emptyCandidate = () =>
                    NotificationRequestProcessor.ClientStateMatches(string.Empty, stored);

                nullCandidate.Should().NotThrow().Which.Should().BeFalse();
                emptyCandidate.Should().NotThrow().Which.Should().BeFalse();
            });
    }
}
