using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Catch-path and edge-case tests for <see cref="ComMessageSource"/> SMTP resolution (issue #73
/// RF-1). These tests cover the <c>ResolveViaPropertyAccessor</c> and <c>ResolveViaExchangeUser</c>
/// catch branches by supplying reflection-readable fakes whose invoked methods throw on demand.
/// Split from <c>ComMessageSourceTests.cs</c> to keep each file under the 500-line cap.
/// No live COM; <see cref="FakeComActiveObject"/> release is a no-op on non-COM objects.
/// </summary>
[TestClass]
public sealed class ComMessageSourceResolutionTests
{
    /// <summary>
    /// Reflection-readable mail item exposing only the members used by the SMTP resolution path.
    /// </summary>
    private sealed class FakeMailItem
    {
        public string? SenderEmailAddress { get; init; }
        public object? Sender { get; init; }
    }

    /// <summary>
    /// Address-entry fake whose <c>PropertyAccessor</c> getter returns a
    /// <see cref="FakeThrowingPropertyAccessor"/> that throws on <c>GetProperty</c>.
    /// Drives the catch path inside <c>ResolveViaPropertyAccessor</c>.
    /// </summary>
    private sealed class FakeAddressEntryWithThrowingPropertyAccessor
    {
        public FakeThrowingPropertyAccessor PropertyAccessor { get; } =
            new FakeThrowingPropertyAccessor();

        public object? GetExchangeUser() => null;
    }

    /// <summary>
    /// Address-entry fake whose <c>GetExchangeUser</c> method throws on invocation, driving the
    /// catch path inside <c>ResolveViaExchangeUser</c>.
    /// </summary>
    private sealed class FakeAddressEntryWithThrowingExchangeUser
    {
        public object? GetExchangeUser() =>
            throw new InvalidOperationException("Simulated COM failure on GetExchangeUser.");
    }

    /// <summary>
    /// Sender fake that exposes the specified address entry as <c>AddressEntry</c>.
    /// </summary>
    private sealed class FakeSenderWithEntry<T>
        where T : class
    {
        public T? AddressEntry { get; init; }

        public string? Address { get; init; }
    }

    [TestMethod]
    public void ResolveViaPropertyAccessor_should_return_null_when_get_property_throws()
    {
        // Arrange: address entry whose PropertyAccessor.GetProperty throws → catch path fires,
        // returns null, and the adapter falls through to the next resolution step.
        var addressEntry = new FakeAddressEntryWithThrowingPropertyAccessor();
        var sender = new FakeSenderWithEntry<FakeAddressEntryWithThrowingPropertyAccessor>
        {
            AddressEntry = addressEntry,
            Address = null,
        };
        var item = new FakeMailItem { Sender = sender, SenderEmailAddress = "fallback@test.com" };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act: the PropertyAccessor catch path returns null; then ExchangeUser (null), senderAddress
        // (null), entryAddress (null → null ?? null = null) → falls through to raw SenderEmailAddress.
        source.SenderEmailResolved.Should().Be("fallback@test.com");
    }

    [TestMethod]
    public void ResolveViaExchangeUser_should_return_null_when_get_exchange_user_throws()
    {
        // Arrange: address entry whose GetExchangeUser throws → catch path fires, returns null.
        var addressEntry = new FakeAddressEntryWithThrowingExchangeUser();
        var sender = new FakeSenderWithEntry<FakeAddressEntryWithThrowingExchangeUser>
        {
            AddressEntry = addressEntry,
            Address = null,
        };
        var item = new FakeMailItem { Sender = sender, SenderEmailAddress = "fallback@test.com" };
        var source = new ComMessageSource(item, new FakeComActiveObject(), isMeeting: false);

        // Act: ExchangeUser catch returns null; senderAddress (null), entryAddress (null) → null.
        // Falls through to raw SenderEmailAddress.
        source.SenderEmailResolved.Should().Be("fallback@test.com");
    }
}
