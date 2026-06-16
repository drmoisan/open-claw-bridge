using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Covers the non-COM guard surface of <see cref="OutlookComMailSender"/> that runs before any STA
/// COM work. The live COM send path is marked <c>[ExcludeFromCodeCoverage]</c> and is exercised by
/// the Phase 9 integration tests.
/// </summary>
[TestClass]
public class OutlookComMailSenderGuardTests
{
    private static SendMailComRequest Request() =>
        new("Subject", "Text", "body", To: ["a@b.c"], Cc: [], Bcc: [], SaveToSentItems: true);

    /// <summary>
    /// STA executor that fails the test if invoked: the guard paths must short-circuit before any
    /// STA COM work is queued.
    /// </summary>
    private sealed class ThrowingStaExecutor : IOutlookStaExecutor
    {
        public Task<T> InvokeAsync<T>(Func<T> operation) =>
            throw new InvalidOperationException("STA executor must not be invoked in guard tests.");

        public void Dispose() { }
    }

    [TestMethod]
    public async Task SendMailAsync_should_throw_when_outlook_application_is_null()
    {
        // Arrange
        var provider = new OutlookApplicationProvider(); // Application defaults to null
        var sender = new OutlookComMailSender(
            new ThrowingStaExecutor(),
            provider,
            NullLogger<OutlookComMailSender>.Instance
        );

        // Act
        var act = async () => await sender.SendMailAsync(Request(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not connected*");
    }

    [TestMethod]
    public async Task SendMailAsync_should_observe_cancellation_before_sta_work()
    {
        // Arrange
        var provider = new OutlookApplicationProvider();
        provider.Set(new object());
        var sender = new OutlookComMailSender(
            new ThrowingStaExecutor(),
            provider,
            NullLogger<OutlookComMailSender>.Instance
        );
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await sender.SendMailAsync(Request(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
