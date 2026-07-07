using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// Pins the pure classifier (spec D3) and pair-evaluator (spec D4) of
/// <see cref="ScopeBoundaryEvaluator"/>. Part (a) is the full D3 classification matrix:
/// the real 403 RBAC denial is the only true case; every other error code, the null and
/// non-<c>ErrorAccessDenied</c> 401 shapes, the <c>Ok == true</c> case, and case-variant
/// constants (pinning Ordinal comparison) classify false. Part (b) is the D4
/// pair-evaluation matrix: (allowed, denied) succeeds; (denied, denied), (allowed,
/// allowed), and (denied, allowed) fail with the precise reason string(s), including the
/// <c>"; "</c>-joined both-sides case and the wrong-error quoting.
/// </summary>
[TestClass]
public sealed class ScopeBoundaryEvaluatorTests
{
    private const string InScope = "in-scope-user@contoso.com";
    private const string OutOfScope = "out-of-scope-user@contoso.com";

    private static MailboxProbeOutcome Allowed() => new(true, null, null, null);

    private static MailboxProbeOutcome RealDenial() =>
        new(false, "UNAUTHORIZED", "ErrorAccessDenied", "Microsoft Graph returned HTTP 403.");

    // ─── (a) D3 classification matrix ────────────────────────────────────────────

    [TestMethod]
    public void IsAuthorizationDenial_Real403RbacDenial_IsTrue()
    {
        ScopeBoundaryEvaluator
            .IsAuthorizationDenial(RealDenial())
            .Should()
            .BeTrue(
                "UNAUTHORIZED + ErrorAccessDenied on a failed read is the only accepted denial shape"
            );
    }

    [TestMethod]
    public void IsAuthorizationDenial_401ShapedUnauthorizedWithNullBridgeCode_IsFalse()
    {
        var outcome = new MailboxProbeOutcome(false, "UNAUTHORIZED", null, "HTTP 401.");

        ScopeBoundaryEvaluator
            .IsAuthorizationDenial(outcome)
            .Should()
            .BeFalse("a null BridgeErrorCode fails the third conjunct");
    }

    [TestMethod]
    public void IsAuthorizationDenial_401ShapedUnauthorizedWithInvalidTokenBridgeCode_IsFalse()
    {
        var outcome = new MailboxProbeOutcome(
            false,
            "UNAUTHORIZED",
            "InvalidAuthenticationToken",
            "HTTP 401."
        );

        ScopeBoundaryEvaluator
            .IsAuthorizationDenial(outcome)
            .Should()
            .BeFalse("InvalidAuthenticationToken is a 401 auth fault, not the 403 RBAC denial");
    }

    [DataTestMethod]
    [DataRow("CONFIGURATION_ERROR", null, DisplayName = "CONFIGURATION_ERROR / null")]
    [DataRow("NOT_FOUND", "ErrorItemNotFound", DisplayName = "NOT_FOUND / ErrorItemNotFound")]
    [DataRow("THROTTLED", "ApplicationThrottled", DisplayName = "THROTTLED")]
    [DataRow("TRANSPORT_FAILURE", null, DisplayName = "TRANSPORT_FAILURE / null")]
    [DataRow("INVALID_REQUEST", "ErrorInvalidRequest", DisplayName = "INVALID_REQUEST")]
    [DataRow("INTERNAL_ERROR", null, DisplayName = "INTERNAL_ERROR / null")]
    public void IsAuthorizationDenial_OtherErrorCodes_AreFalse(
        string errorCode,
        string? bridgeErrorCode
    )
    {
        var outcome = new MailboxProbeOutcome(false, errorCode, bridgeErrorCode, "some failure");

        ScopeBoundaryEvaluator
            .IsAuthorizationDenial(outcome)
            .Should()
            .BeFalse($"{errorCode} is not the UNAUTHORIZED/ErrorAccessDenied denial shape");
    }

    [TestMethod]
    public void IsAuthorizationDenial_SuccessfulRead_IsFalse()
    {
        ScopeBoundaryEvaluator
            .IsAuthorizationDenial(Allowed())
            .Should()
            .BeFalse("a successful read (Ok == true) is never a denial");
    }

    [TestMethod]
    public void IsAuthorizationDenial_CaseVariantBridgeCode_IsFalse()
    {
        var outcome = new MailboxProbeOutcome(
            false,
            "UNAUTHORIZED",
            "erroraccessdenied",
            "HTTP 403."
        );

        ScopeBoundaryEvaluator
            .IsAuthorizationDenial(outcome)
            .Should()
            .BeFalse(
                "BridgeErrorCode is compared Ordinal; 'erroraccessdenied' must not match 'ErrorAccessDenied'"
            );
    }

    [TestMethod]
    public void IsAuthorizationDenial_CaseVariantErrorCode_IsFalse()
    {
        var outcome = new MailboxProbeOutcome(
            false,
            "unauthorized",
            "ErrorAccessDenied",
            "HTTP 403."
        );

        ScopeBoundaryEvaluator
            .IsAuthorizationDenial(outcome)
            .Should()
            .BeFalse("ErrorCode is compared Ordinal; 'unauthorized' must not match 'UNAUTHORIZED'");
    }

    // ─── (b) D4 pair-evaluation matrix ───────────────────────────────────────────

    [TestMethod]
    public void Evaluate_AllowedAndDenied_SucceedsWithNullFailureReason()
    {
        var result = ScopeBoundaryEvaluator.Evaluate(InScope, OutOfScope, Allowed(), RealDenial());

        result.InScopeMailbox.Should().Be(InScope);
        result.OutOfScopeMailbox.Should().Be(OutOfScope);
        result.InScopeAllowed.Should().BeTrue();
        result.OutOfScopeDenied.Should().BeTrue();
        result
            .Succeeded.Should()
            .BeTrue("in-scope allowed and out-of-scope denied is the only pass condition");
        result.FailureReason.Should().BeNull("FailureReason is null iff Succeeded");
    }

    [TestMethod]
    public void Evaluate_DeniedAndDenied_FailsWithInScopeReasonOnly()
    {
        var inScopeDenial = new MailboxProbeOutcome(
            false,
            "UNAUTHORIZED",
            "ErrorAccessDenied",
            "in-scope was denied"
        );

        var result = ScopeBoundaryEvaluator.Evaluate(
            InScope,
            OutOfScope,
            inScopeDenial,
            RealDenial()
        );

        result.Succeeded.Should().BeFalse();
        result.InScopeAllowed.Should().BeFalse();
        result
            .OutOfScopeDenied.Should()
            .BeTrue("the out-of-scope side is the correct denial shape");
        result
            .FailureReason.Should()
            .Be(
                "in-scope mailbox read failed: UNAUTHORIZED/ErrorAccessDenied: in-scope was denied"
            );
        result.FailureReason.Should().NotContain("; ", "only the in-scope side failed");
    }

    [TestMethod]
    public void Evaluate_AllowedAndAllowed_FailsWithScopeLeakReason()
    {
        var result = ScopeBoundaryEvaluator.Evaluate(InScope, OutOfScope, Allowed(), Allowed());

        result.Succeeded.Should().BeFalse();
        result.OutOfScopeDenied.Should().BeFalse();
        result
            .FailureReason.Should()
            .Be("out-of-scope mailbox read unexpectedly succeeded; the RBAC scope does not hold");
    }

    [TestMethod]
    public void Evaluate_DeniedAndAllowed_FailsWithBothReasonsJoined()
    {
        var inScopeDenial = new MailboxProbeOutcome(
            false,
            "UNAUTHORIZED",
            "ErrorAccessDenied",
            "in-scope was denied"
        );

        var result = ScopeBoundaryEvaluator.Evaluate(InScope, OutOfScope, inScopeDenial, Allowed());

        result.Succeeded.Should().BeFalse();
        result
            .FailureReason.Should()
            .Be(
                "in-scope mailbox read failed: UNAUTHORIZED/ErrorAccessDenied: in-scope was denied; "
                    + "out-of-scope mailbox read unexpectedly succeeded; the RBAC scope does not hold"
            );
        result
            .FailureReason.Should()
            .Contain("; ", "both sides failed, so the reasons are '; '-joined");
    }

    [TestMethod]
    public void Evaluate_AllowedAndWrongError_QuotesObservedClassification()
    {
        var wrongError = new MailboxProbeOutcome(
            false,
            "NOT_FOUND",
            "ErrorItemNotFound",
            "HTTP 404."
        );

        var result = ScopeBoundaryEvaluator.Evaluate(InScope, OutOfScope, Allowed(), wrongError);

        result.Succeeded.Should().BeFalse();
        result.OutOfScopeDenied.Should().BeFalse();
        result
            .FailureReason.Should()
            .Be(
                "out-of-scope mailbox read failed but not with the expected authorization denial "
                    + "(expected UNAUTHORIZED/ErrorAccessDenied; observed NOT_FOUND/ErrorItemNotFound)"
            );
    }

    [TestMethod]
    public void Evaluate_AllowedAndWrongErrorWithNullBridgeCode_SubstitutesDashForNull()
    {
        var wrongError = new MailboxProbeOutcome(false, "TRANSPORT_FAILURE", null, "network error");

        var result = ScopeBoundaryEvaluator.Evaluate(InScope, OutOfScope, Allowed(), wrongError);

        result.Succeeded.Should().BeFalse();
        result
            .FailureReason.Should()
            .Be(
                "out-of-scope mailbox read failed but not with the expected authorization denial "
                    + "(expected UNAUTHORIZED/ErrorAccessDenied; observed TRANSPORT_FAILURE/-)"
            );
    }

    [TestMethod]
    public void Evaluate_ComposesBothOutcomesVerbatim()
    {
        var inScopeOutcome = Allowed();
        var outOfScopeOutcome = RealDenial();

        var result = ScopeBoundaryEvaluator.Evaluate(
            InScope,
            OutOfScope,
            inScopeOutcome,
            outOfScopeOutcome
        );

        result.InScopeOutcome.Should().BeSameAs(inScopeOutcome);
        result.OutOfScopeOutcome.Should().BeSameAs(outOfScopeOutcome);
    }
}
