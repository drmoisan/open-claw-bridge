using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// CsCheck property tests for the pure renewal-due function
/// <see cref="GraphSubscriptionManager.IsRenewalDue"/> (T1 property-test rule):
/// monotonicity (once due, remains due as now advances), equivalence with the
/// arithmetic definition for arbitrary expiration/lead/now triples, and never due
/// while <c>now &lt; expiration - lead</c>. Failing seeds are reported by CsCheck's
/// default output.
/// </summary>
[TestClass]
public sealed class RenewalDuePropertyTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Arbitrary (now, expiration, lead) triples within sane subscription bounds.</summary>
    private static Gen<(DateTimeOffset Now, DateTimeOffset Expiration, int Lead)> Triples =>
        Gen.Select(
            Gen.Long[0, TimeSpan.TicksPerDay * 30],
            Gen.Long[0, TimeSpan.TicksPerDay * 30],
            Gen.Int[1, 10079],
            (nowTicks, expirationTicks, lead) =>
                (Epoch.AddTicks(nowTicks), Epoch.AddTicks(expirationTicks), lead)
        );

    [TestMethod]
    public void Due_matches_the_arithmetic_definition_for_arbitrary_triples()
    {
        Triples.Sample(triple =>
        {
            var (now, expiration, lead) = triple;

            var due = GraphSubscriptionManager.IsRenewalDue(now, expiration, lead);

            due.Should()
                .Be(
                    now >= expiration - TimeSpan.FromMinutes(lead),
                    "the function must equal its arithmetic definition"
                );
        });
    }

    [TestMethod]
    public void Once_due_remains_due_as_now_advances()
    {
        Gen.Select(Triples, Gen.Long[0, TimeSpan.TicksPerDay * 7], (t, advance) => (t, advance))
            .Sample(pair =>
            {
                var ((now, expiration, lead), advanceTicks) = pair;

                var dueNow = GraphSubscriptionManager.IsRenewalDue(now, expiration, lead);
                var dueLater = GraphSubscriptionManager.IsRenewalDue(
                    now.AddTicks(advanceTicks),
                    expiration,
                    lead
                );

                if (dueNow)
                {
                    dueLater
                        .Should()
                        .BeTrue("renewal-due is monotone: once due, it stays due as time advances");
                }
            });
    }

    [TestMethod]
    public void Never_due_while_now_is_before_expiration_minus_lead()
    {
        Triples
            .Where(t => t.Now < t.Expiration - TimeSpan.FromMinutes(t.Lead))
            .Sample(triple =>
            {
                var (now, expiration, lead) = triple;

                GraphSubscriptionManager
                    .IsRenewalDue(now, expiration, lead)
                    .Should()
                    .BeFalse("inside the safe window the subscription is never due");
            });
    }
}
