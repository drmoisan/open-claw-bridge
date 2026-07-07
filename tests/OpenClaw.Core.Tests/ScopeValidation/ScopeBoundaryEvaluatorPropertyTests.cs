using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// CsCheck property tests (>= 1 per pure function, T1) for
/// <see cref="ScopeBoundaryEvaluator"/>. Property (a): <c>IsAuthorizationDenial</c>
/// returns true iff all three conjuncts hold for any generated
/// <see cref="MailboxProbeOutcome"/>. Property (b): for any generated outcome pair,
/// <c>Evaluate</c> satisfies <c>Succeeded == InScopeAllowed &amp;&amp; OutOfScopeDenied</c>
/// and <c>(FailureReason is null) == Succeeded</c>. Failing seeds are reported by
/// CsCheck's default output.
/// </summary>
[TestClass]
public sealed class ScopeBoundaryEvaluatorPropertyTests
{
    // Generator vocabularies deliberately include the accepted constants, a case-variant
    // ("erroraccessdenied"), the 401 code, other D5 codes, and null, so the generated
    // space exercises every conjunct of the classifier.
    private static readonly string?[] ErrorCodes =
    [
        "UNAUTHORIZED",
        "unauthorized",
        "NOT_FOUND",
        "THROTTLED",
        "TRANSPORT_FAILURE",
        "CONFIGURATION_ERROR",
        "INTERNAL_ERROR",
        "INVALID_REQUEST",
        null,
    ];

    private static readonly string?[] BridgeCodes =
    [
        "ErrorAccessDenied",
        "erroraccessdenied",
        "InvalidAuthenticationToken",
        "ErrorItemNotFound",
        null,
    ];

    private static readonly Gen<MailboxProbeOutcome> OutcomeGen = Gen.Select(
        Gen.Bool,
        Gen.Int[0, ErrorCodes.Length - 1],
        Gen.Int[0, BridgeCodes.Length - 1],
        (ok, errorIndex, bridgeIndex) =>
            new MailboxProbeOutcome(ok, ErrorCodes[errorIndex], BridgeCodes[bridgeIndex], "message")
    );

    [TestMethod]
    public void IsAuthorizationDenial_TrueIffAllThreeConjunctsHold()
    {
        OutcomeGen.Sample(outcome =>
        {
            // Ordinal string equality via C# `==`, matching the implementation's
            // StringComparison.Ordinal comparisons.
            var expected =
                !outcome.Ok
                && outcome.ErrorCode == "UNAUTHORIZED"
                && outcome.BridgeErrorCode == "ErrorAccessDenied";

            ScopeBoundaryEvaluator.IsAuthorizationDenial(outcome).Should().Be(expected);
        });
    }

    [TestMethod]
    public void Evaluate_SucceededAndFailureReasonInvariantsHold()
    {
        Gen.Select(OutcomeGen, OutcomeGen)
            .Sample(pair =>
            {
                var (inScope, outOfScope) = pair;

                var result = ScopeBoundaryEvaluator.Evaluate(
                    "in-scope@contoso.com",
                    "out-of-scope@contoso.com",
                    inScope,
                    outOfScope
                );

                result
                    .Succeeded.Should()
                    .Be(
                        result.InScopeAllowed && result.OutOfScopeDenied,
                        "Succeeded is defined as InScopeAllowed && OutOfScopeDenied"
                    );
                (result.FailureReason is null)
                    .Should()
                    .Be(result.Succeeded, "FailureReason is null iff Succeeded");
            });
    }
}
