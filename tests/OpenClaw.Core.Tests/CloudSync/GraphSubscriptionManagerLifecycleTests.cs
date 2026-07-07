using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Lifecycle routing tests for <see cref="GraphSubscriptionManager"/> (AC-2):
/// <c>reauthorizationRequired</c> PATCHes and, on a 401-mapped envelope, marks the
/// record <c>reauthorize_failed</c>; <c>removed</c> deletes then POSTs a fresh create;
/// <c>missed</c> invokes the reconcile trigger with the subscription's mailbox and
/// issues no Graph call from the manager; unknown lifecycle values log Warning without
/// throwing.
/// </summary>
[TestClass]
public sealed class GraphSubscriptionManagerLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    private const string ClientState = "deterministic-client-state";

    private const string SubscriptionResponse = """
        {
          "id": "sub-recreated-1",
          "resource": "users/paula@contoso.com/mailFolders('Inbox')/messages",
          "changeType": "created,updated",
          "expirationDateTime": "2026-07-10T08:00:00Z"
        }
        """;

    /// <summary>Recorded Graph 401 error body (auth-mapped terminal failure).</summary>
    private const string UnauthorizedBody = """
        { "error": { "code": "InvalidAuthenticationToken", "message": "Access token is empty." } }
        """;

    private static FakeSubscriptionStore StoreWithSub1()
    {
        var store = new FakeSubscriptionStore();
        store.Records["sub-1"] = new GraphSubscriptionRecord(
            "sub-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            ClientState,
            Now.AddDays(2),
            SubscriptionStatus.Active
        );
        return store;
    }

    [TestMethod]
    public async Task ReauthorizationRequired_renews_and_marks_reauthorize_failed_on_401()
    {
        // Arrange: the PATCH renewal comes back 401, mapping to UNAUTHORIZED.
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHttpHandler(request =>
        {
            requests.Add(request);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(UnauthorizedBody),
                }
            );
        });
        var store = StoreWithSub1();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now)
        );

        // Act
        await manager.HandleLifecycleAsync(
            new LifecycleWorkItem("sub-1", LifecycleEvents.ReauthorizationRequired),
            CancellationToken.None
        );

        // Assert
        requests
            .Should()
            .ContainSingle("reauthorizationRequired routes to a renew PATCH")
            .Which.Method.Should()
            .Be(HttpMethod.Patch);
        store
            .StatusUpdates.Should()
            .ContainSingle()
            .Which.Should()
            .Be(("sub-1", SubscriptionStatus.ReauthorizeFailed));
        store.Records["sub-1"].Status.Should().Be(SubscriptionStatus.ReauthorizeFailed);
    }

    [TestMethod]
    public async Task Removed_deletes_the_local_record_and_recreates_the_subscription()
    {
        // Arrange
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHttpHandler(request =>
        {
            requests.Add(request);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(SubscriptionResponse),
                }
            );
        });
        var store = StoreWithSub1();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now)
        );

        // Act
        await manager.HandleLifecycleAsync(
            new LifecycleWorkItem("sub-1", LifecycleEvents.Removed),
            CancellationToken.None
        );

        // Assert
        store.Deletes.Should().Equal(["sub-1"], "the removed subscription's record is deleted");
        requests
            .Should()
            .ContainSingle("removed routes to a fresh create POST")
            .Which.Method.Should()
            .Be(HttpMethod.Post);
        store
            .Records.Should()
            .ContainKey("sub-recreated-1", "the recreated subscription is persisted");
    }

    [TestMethod]
    public async Task Missed_triggers_the_reconcile_seam_and_issues_no_graph_call()
    {
        // Arrange
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHttpHandler(request =>
        {
            requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var store = StoreWithSub1();
        var trigger = new RecordingReconcileTrigger();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            trigger
        );

        // Act
        await manager.HandleLifecycleAsync(
            new LifecycleWorkItem("sub-1", LifecycleEvents.Missed),
            CancellationToken.None
        );

        // Assert
        trigger
            .TriggeredMailboxes.Should()
            .Equal(
                ["paula@contoso.com"],
                "missed triggers a re-sync for the subscription's mailbox"
            );
        requests.Should().BeEmpty("the manager itself issues no Graph call on missed");
    }

    [TestMethod]
    public async Task Unknown_lifecycle_event_logs_warning_and_does_not_throw()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
        );
        var store = StoreWithSub1();
        var logger = new CapturingLogger<GraphSubscriptionManager>();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            logger: logger
        );

        // Act
        var act = async () =>
            await manager.HandleLifecycleAsync(
                new LifecycleWorkItem("sub-1", "subscriptionRemoved-preview"),
                CancellationToken.None
            );

        // Assert
        await act.Should().NotThrowAsync("unknown lifecycle values are ignored, not fatal");
        logger
            .Entries.Should()
            .ContainSingle("the unknown value is logged exactly once")
            .Which.Level.Should()
            .Be(Microsoft.Extensions.Logging.LogLevel.Warning);
        store.StatusUpdates.Should().BeEmpty();
        store.Deletes.Should().BeEmpty();
    }
}
