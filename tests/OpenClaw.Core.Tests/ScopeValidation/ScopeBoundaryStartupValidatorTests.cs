using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// Verifies the one-shot startup hosted service <see cref="ScopeBoundaryStartupValidator"/>
/// (spec D5): success logs a single <c>Information</c> entry carrying every result field
/// and completes without throwing; failure logs a single <c>Critical</c> entry including
/// the <c>FailureReason</c> and throws <see cref="InvalidOperationException"/> whose
/// message names the reason; the log never contains a bearer token or response-body text;
/// cancellation propagates cleanly; and <c>StopAsync</c> completes synchronously.
/// </summary>
[TestClass]
public sealed class ScopeBoundaryStartupValidatorTests
{
    private const string InScope = "in-scope@contoso.com";
    private const string OutOfScope = "out-of-scope@contoso.com";
    private const string BearerToken = "secret-bearer-token-value";

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

    private static ScopeBoundaryValidator ValidatorReturning(
        MailboxProbeOutcome inScopeOutcome,
        MailboxProbeOutcome outOfScopeOutcome
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
                (upn, _, _) => Task.FromResult(upn == InScope ? inScopeOutcome : outOfScopeOutcome)
            );
        return new ScopeBoundaryValidator(probe.Object, Options());
    }

    [TestMethod]
    public async Task StartAsync_Success_LogsSingleInformationEntryWithEveryField()
    {
        var validator = ValidatorReturning(Allowed, Denial);
        var logger = new CapturingLogger<ScopeBoundaryStartupValidator>();
        var sut = new ScopeBoundaryStartupValidator(validator, logger);

        await sut.StartAsync(CancellationToken.None);

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries[0];
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain(InScope);
        entry.Message.Should().Contain(OutOfScope);
        entry.Message.Should().Contain("InScopeAllowed");
        entry.Message.Should().Contain("OutOfScopeDenied");
        entry.Message.Should().Contain("Succeeded");
        entry.Message.Should().Contain("InScopeOutcome");
        entry.Message.Should().Contain("OutOfScopeOutcome");
    }

    [TestMethod]
    public async Task StartAsync_Failure_LogsSingleCriticalEntryAndThrowsNamingReason()
    {
        // (allowed, allowed) -> scope leak failure.
        var validator = ValidatorReturning(Allowed, Allowed);
        var logger = new CapturingLogger<ScopeBoundaryStartupValidator>();
        var sut = new ScopeBoundaryStartupValidator(validator, logger);

        var act = async () => await sut.StartAsync(CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        exception
            .Message.Should()
            .Contain(
                "out-of-scope mailbox read unexpectedly succeeded",
                "the thrown exception names the FailureReason"
            );
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Critical);
        logger.Entries[0].Message.Should().Contain("FailureReason");
        logger
            .Entries[0]
            .Message.Should()
            .Contain("out-of-scope mailbox read unexpectedly succeeded");
    }

    [TestMethod]
    public async Task StartAsync_LogOutput_ContainsNoTokenOrResponseBody()
    {
        // The probe outcomes carry only mapped fields, never a token or a raw body; the
        // startup validator's summaries are limited to Ok/ErrorCode/BridgeErrorCode.
        var validator = ValidatorReturning(Allowed, Denial);
        var logger = new CapturingLogger<ScopeBoundaryStartupValidator>();
        var sut = new ScopeBoundaryStartupValidator(validator, logger);

        await sut.StartAsync(CancellationToken.None);

        var allText = string.Join("\n", logger.Entries.Select(e => e.Message));
        allText.Should().NotContain(BearerToken, "tokens are never logged");
        allText.Should().NotContain("<html", "raw response bodies are never logged");
        allText.Should().NotContain("value\":", "raw response bodies are never logged");
    }

    [TestMethod]
    public async Task StartAsync_WhenCancellationRequested_PropagatesCancellation()
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
                (_, _, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(Allowed);
                }
            );
        var validator = new ScopeBoundaryValidator(probe.Object, Options());
        var logger = new CapturingLogger<ScopeBoundaryStartupValidator>();
        var sut = new ScopeBoundaryStartupValidator(validator, logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await sut.StartAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task StopAsync_CompletesSynchronously()
    {
        var validator = ValidatorReturning(Allowed, Denial);
        var logger = new CapturingLogger<ScopeBoundaryStartupValidator>();
        var sut = new ScopeBoundaryStartupValidator(validator, logger);

        var stopTask = sut.StopAsync(CancellationToken.None);

        stopTask.IsCompletedSuccessfully.Should().BeTrue("StopAsync is a synchronous no-op");
        await stopTask;
    }

    /// <summary>
    /// Minimal capturing <see cref="ILogger{T}"/> that records the level and rendered
    /// message of each entry, so tests can assert on logged content without a logging
    /// framework or any I/O.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
