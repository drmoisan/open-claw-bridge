using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// Verifies the composition behavior of <see cref="ScopeBoundaryValidator"/> (spec D5):
/// both probes are invoked with the configured mailboxes, the in-scope probe runs before
/// the out-of-scope probe, the out-of-scope probe still runs when the in-scope probe
/// fails (no short-circuit), both outcomes are composed verbatim into the result, and the
/// cancellation token flows to both probe calls.
/// </summary>
[TestClass]
public sealed class ScopeBoundaryValidatorTests
{
    private const string InScope = "in-scope@contoso.com";
    private const string OutOfScope = "out-of-scope@contoso.com";

    private static readonly MailboxProbeOutcome Allowed = new(true, null, null, null);
    private static readonly MailboxProbeOutcome Denial = new(
        false,
        "UNAUTHORIZED",
        "ErrorAccessDenied",
        "HTTP 403."
    );

    private static IOptions<ScopeValidationOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(
            new ScopeValidationOptions
            {
                Enabled = true,
                InScopeTestMailboxUpn = InScope,
                OutOfScopeTestMailboxUpn = OutOfScope,
            }
        );

    private static Mock<IMailboxScopeProbe> ProbeReturning(
        MailboxProbeOutcome inScopeOutcome,
        MailboxProbeOutcome outOfScopeOutcome,
        List<string>? callOrder = null
    )
    {
        var probe = new Mock<IMailboxScopeProbe>();
        probe
            .Setup(p =>
                p.ProbeMailboxReadAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string?, CancellationToken>(
                (upn, _, _) =>
                {
                    callOrder?.Add(upn);
                    return Task.FromResult(upn == InScope ? inScopeOutcome : outOfScopeOutcome);
                }
            );
        return probe;
    }

    [TestMethod]
    public async Task ValidateAsync_InvokesBothProbesWithConfiguredMailboxes()
    {
        var probe = ProbeReturning(Allowed, Denial);
        var validator = new ScopeBoundaryValidator(probe.Object, Options());

        await validator.ValidateAsync(CancellationToken.None);

        probe.Verify(
            p =>
                p.ProbeMailboxReadAsync(
                    InScope,
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        probe.Verify(
            p =>
                p.ProbeMailboxReadAsync(
                    OutOfScope,
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task ValidateAsync_ProbesInScopeBeforeOutOfScope()
    {
        var callOrder = new List<string>();
        var probe = ProbeReturning(Allowed, Denial, callOrder);
        var validator = new ScopeBoundaryValidator(probe.Object, Options());

        await validator.ValidateAsync(CancellationToken.None);

        callOrder.Should().Equal(InScope, OutOfScope);
    }

    [TestMethod]
    public async Task ValidateAsync_WhenInScopeFails_StillProbesOutOfScope()
    {
        var inScopeFailure = new MailboxProbeOutcome(
            false,
            "TRANSPORT_FAILURE",
            null,
            "network error"
        );
        var callOrder = new List<string>();
        var probe = ProbeReturning(inScopeFailure, Denial, callOrder);
        var validator = new ScopeBoundaryValidator(probe.Object, Options());

        var result = await validator.ValidateAsync(CancellationToken.None);

        // No short-circuit on an in-scope failure: both mailboxes are probed, in order.
        callOrder.Should().Equal(InScope, OutOfScope);
        result.InScopeAllowed.Should().BeFalse();
        result.Succeeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task ValidateAsync_ComposesBothOutcomesVerbatim()
    {
        var probe = ProbeReturning(Allowed, Denial);
        var validator = new ScopeBoundaryValidator(probe.Object, Options());

        var result = await validator.ValidateAsync(CancellationToken.None);

        result.InScopeMailbox.Should().Be(InScope);
        result.OutOfScopeMailbox.Should().Be(OutOfScope);
        result.InScopeOutcome.Should().BeSameAs(Allowed);
        result.OutOfScopeOutcome.Should().BeSameAs(Denial);
        result.Succeeded.Should().BeTrue();
    }

    [TestMethod]
    public async Task ValidateAsync_PassesCancellationTokenToBothProbes()
    {
        var probe = ProbeReturning(Allowed, Denial);
        var validator = new ScopeBoundaryValidator(probe.Object, Options());
        using var cts = new CancellationTokenSource();

        await validator.ValidateAsync(cts.Token);

        probe.Verify(
            p => p.ProbeMailboxReadAsync(InScope, It.IsAny<string?>(), cts.Token),
            Times.Once
        );
        probe.Verify(
            p => p.ProbeMailboxReadAsync(OutOfScope, It.IsAny<string?>(), cts.Token),
            Times.Once
        );
    }
}
