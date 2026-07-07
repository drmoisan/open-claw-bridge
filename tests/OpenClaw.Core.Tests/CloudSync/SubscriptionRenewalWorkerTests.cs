using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Deterministic <see cref="SubscriptionRenewalWorker"/> tests with
/// <see cref="FakeTimeProvider"/> (AC-2): renewal-due boundary exactly at
/// <c>expiration - lead</c> (one tick before is not due, at/after is due), the startup
/// sweep recreates an already-expired subscription and creates one when none exists,
/// and advancing fake time to the schedule tick triggers the periodic check. Time
/// advances only via <see cref="FakeTimeProvider"/>.
/// </summary>
[TestClass]
public sealed class SubscriptionRenewalWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    private const string SubscriptionResponse = """
        {
          "id": "sub-fresh-1",
          "resource": "users/paula@contoso.com/mailFolders('Inbox')/messages",
          "expirationDateTime": "2026-07-10T08:00:00Z"
        }
        """;

    private static readonly CloudSyncOptions WorkerOptions = new()
    {
        Enabled = true,
        NotificationUrl = "https://webhook.contoso.com/graph/notifications",
        RenewalLeadMinutes = 30,
    };

    private static GraphSubscriptionRecord Record(DateTimeOffset expiration) =>
        new(
            "sub-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            "client-state",
            expiration,
            SubscriptionStatus.Active
        );

    private static (
        SubscriptionRenewalWorker Worker,
        FakeSubscriptionStore Store,
        List<HttpRequestMessage> Requests
    ) Build(FakeTimeProvider timeProvider, FakeSubscriptionStore? store = null)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHttpHandler(request =>
        {
            requests.Add(request);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SubscriptionResponse),
                }
            );
        });
        store ??= new FakeSubscriptionStore();
        var manager = GraphSubscriptionManagerTests.Manager(handler, store, timeProvider);
        var worker = new SubscriptionRenewalWorker(
            manager,
            store,
            Options.Create(WorkerOptions),
            timeProvider,
            NullLogger<SubscriptionRenewalWorker>.Instance
        );
        return (worker, store, requests);
    }

    [TestMethod]
    public void Renewal_due_boundary_is_exact_at_expiration_minus_lead()
    {
        // Arrange
        var expiration = Now.AddHours(10);
        var boundary = expiration - TimeSpan.FromMinutes(30);

        // Act / Assert: exactly at the boundary is due; one tick earlier is not; one
        // tick later is due.
        GraphSubscriptionManager
            .IsRenewalDue(boundary, expiration, 30)
            .Should()
            .BeTrue("now == expiration - lead is due");
        GraphSubscriptionManager
            .IsRenewalDue(boundary.AddTicks(-1), expiration, 30)
            .Should()
            .BeFalse("one tick before the boundary is not due");
        GraphSubscriptionManager
            .IsRenewalDue(boundary.AddTicks(1), expiration, 30)
            .Should()
            .BeTrue("one tick after the boundary is due");
    }

    [TestMethod]
    public async Task Sweep_renews_a_due_subscription_and_leaves_an_undue_one_alone()
    {
        // Arrange: expiration exactly lead-distance away -> due (PATCH expected).
        var timeProvider = new FakeTimeProvider(Now);
        var store = new FakeSubscriptionStore();
        store.Records["sub-1"] = Record(Now.AddMinutes(30)) with { SubscriptionId = "sub-1" };
        var (worker, _, requests) = Build(timeProvider, store);

        // Act
        await worker.RunSweepOnceAsync(CancellationToken.None);

        // Assert
        requests
            .Should()
            .ContainSingle("a due subscription is renewed")
            .Which.Method.Should()
            .Be(HttpMethod.Patch);

        // Arrange: one tick inside the safe window -> not due, no request.
        requests.Clear();
        store.Records["sub-1"] = Record(timeProvider.GetUtcNow().AddMinutes(30).AddTicks(1));

        // Act
        await worker.RunSweepOnceAsync(CancellationToken.None);

        // Assert
        requests.Should().BeEmpty("a subscription outside the renewal window is untouched");
    }

    [TestMethod]
    public async Task Startup_sweep_recreates_an_already_expired_subscription()
    {
        // Arrange: the stored subscription expired while the host was down.
        var timeProvider = new FakeTimeProvider(Now);
        var store = new FakeSubscriptionStore();
        store.Records["sub-1"] = Record(Now.AddMinutes(-5));
        var (worker, _, requests) = Build(timeProvider, store);

        // Act: start the worker; the startup sweep runs immediately.
        await worker.StartAsync(CancellationToken.None);
        await WaitForAsync(() => requests.Count >= 1);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        requests
            .Should()
            .ContainSingle("the expired subscription is recreated promptly on startup")
            .Which.Method.Should()
            .Be(HttpMethod.Post);
        store.Deletes.Should().Equal(["sub-1"], "the expired record is deleted before recreate");
        store.Records.Should().ContainKey("sub-fresh-1");
    }

    [TestMethod]
    public async Task Startup_sweep_creates_a_subscription_when_none_exists()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Now);
        var (worker, store, requests) = Build(timeProvider);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await WaitForAsync(() => requests.Count >= 1);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        requests
            .Should()
            .ContainSingle("no stored subscription means the sweep creates one")
            .Which.Method.Should()
            .Be(HttpMethod.Post);
        store.Records.Should().ContainKey("sub-fresh-1");
    }

    [TestMethod]
    public async Task Advancing_fake_time_to_the_schedule_tick_triggers_the_periodic_check()
    {
        // Arrange: a healthy far-future subscription so sweeps make no Graph calls; the
        // sweep count is observed through the store's list calls... instead use a
        // subscription that becomes due only after the first tick.
        var timeProvider = new FakeTimeProvider(Now);
        var interval = SubscriptionRenewalWorker.ComputeCheckInterval(
            WorkerOptions.RenewalLeadMinutes
        );
        var store = new FakeSubscriptionStore();
        // Due at Now + 10 minutes; the startup sweep (Now) sees it as not due, the
        // sweep after one 15-minute tick sees it as due and renews.
        store.Records["sub-1"] = Record(Now.AddMinutes(40));
        var (worker, _, requests) = Build(timeProvider, store);

        // Act: step fake time forward (repo AwaitWithTimeAdvance pattern) until the
        // schedule tick fires and the now-due subscription is renewed.
        await worker.StartAsync(CancellationToken.None);
        requests.Should().BeEmpty("the startup sweep sees the subscription as not yet due");
        await WaitForAsync(
            () => requests.Count >= 1,
            () => timeProvider.Advance(TimeSpan.FromMinutes(1))
        );
        await worker.StopAsync(CancellationToken.None);

        // Assert
        interval.Should().Be(TimeSpan.FromMinutes(15), "the schedule is half the renewal lead");
        requests
            .Should()
            .ContainSingle(
                "the periodic check after one schedule tick renews the now-due subscription"
            )
            .Which.Method.Should()
            .Be(HttpMethod.Patch);
    }

    /// <summary>
    /// A <see cref="TaskCanceledException"/> from the sweep (for example an
    /// <c>HttpClient.Timeout</c>) while the stop token is NOT cancelled must not
    /// terminate the hosted service: the broadened catch filter
    /// (<c>when (!stoppingToken.IsCancellationRequested)</c>) logs Warning and the
    /// next scheduled sweep still runs (CR-117-03, fix item 5).
    /// </summary>
    [TestMethod]
    public async Task Loop_continues_with_warning_when_the_sweep_throws_TaskCanceledException_without_stop_requested()
    {
        // Arrange: the first sweep's store list throws TaskCanceledException; later
        // sweeps see a far-future (never-due) subscription, so loop survival is proven
        // by the second sweep's store read without any scheduling-sensitive Graph
        // call. (Moq cannot proxy the internal ISubscriptionStore, so a hand-rolled
        // throw-once decorator is used.)
        var timeProvider = new FakeTimeProvider(Now);
        var innerStore = new FakeSubscriptionStore();
        innerStore.Records["sub-1"] = Record(Now.AddYears(1));
        var throwingStore = new ThrowOnFirstListStore(innerStore);

        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHttpHandler(request =>
        {
            requests.Add(request);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SubscriptionResponse),
                }
            );
        });
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            new FakeSubscriptionStore(),
            timeProvider
        );
        var logger = new CapturingLogger<SubscriptionRenewalWorker>();
        var worker = new SubscriptionRenewalWorker(
            manager,
            throwingStore,
            Options.Create(WorkerOptions),
            timeProvider,
            logger
        );

        // Act: the startup sweep throws; advance fake time so the next sweep runs.
        // (Logger assertions happen only after StopAsync — the capturing logger's
        // list is not synchronized for concurrent polling; the store's call counter
        // is an atomic int read.)
        await worker.StartAsync(CancellationToken.None);
        await WaitForAsync(
            () => throwingStore.ListCalls >= 2,
            () => timeProvider.Advance(TimeSpan.FromMinutes(1))
        );
        await worker.StopAsync(CancellationToken.None);

        // Assert
        logger
            .Levels.Should()
            .Contain(LogLevel.Warning, "the non-stop-token cancellation is caught and logged");
        throwingStore
            .ListCalls.Should()
            .BeGreaterThanOrEqualTo(
                2,
                "the loop survives the TaskCanceledException and runs the next sweep"
            );
        requests.Should().BeEmpty("the never-due subscription requires no Graph call");
    }

    /// <summary>
    /// Cooperatively yields until <paramref name="condition"/> holds (bounded, no
    /// wall-clock waits) so async worker continuations scheduled by fake-time
    /// advancement can run.
    /// </summary>
    private static async Task WaitForAsync(Func<bool> condition, Action? advance = null)
    {
        // Cooperative yields (no wall-clock waits) with an optional fake-time step per
        // iteration — the repository's AwaitWithTimeAdvance pattern.
        var safety = 0;
        while (!condition())
        {
            if (++safety > 100_000)
            {
                throw new AssertFailedException(
                    "The worker did not reach the expected state under fake-time advancement."
                );
            }

            advance?.Invoke();
            await Task.Yield();
        }
    }

    /// <summary>
    /// Decorator over <see cref="FakeSubscriptionStore"/> whose FIRST
    /// <see cref="ListSubscriptionsAsync"/> call throws
    /// <see cref="TaskCanceledException"/> (simulating an <c>HttpClient.Timeout</c>
    /// surfacing from a sweep); every other member delegates. Hand-rolled because Moq
    /// cannot proxy the internal <see cref="ISubscriptionStore"/>.
    /// </summary>
    private sealed class ThrowOnFirstListStore(FakeSubscriptionStore inner) : ISubscriptionStore
    {
        private int listCalls;

        /// <summary>How many times <see cref="ListSubscriptionsAsync"/> was invoked.</summary>
        public int ListCalls => Volatile.Read(ref listCalls);

        public Task<GraphSubscriptionRecord?> GetSubscriptionAsync(
            string subscriptionId,
            CancellationToken ct
        ) => inner.GetSubscriptionAsync(subscriptionId, ct);

        public Task<IReadOnlyList<GraphSubscriptionRecord>> ListSubscriptionsAsync(
            CancellationToken ct
        )
        {
            if (Interlocked.Increment(ref listCalls) == 1)
            {
                throw new TaskCanceledException("Simulated HttpClient timeout.");
            }

            return inner.ListSubscriptionsAsync(ct);
        }

        public Task UpsertSubscriptionAsync(
            GraphSubscriptionRecord record,
            DateTimeOffset nowUtc,
            CancellationToken ct
        ) => inner.UpsertSubscriptionAsync(record, nowUtc, ct);

        public Task UpdateSubscriptionStatusAsync(
            string subscriptionId,
            string status,
            DateTimeOffset updatedAtUtc,
            CancellationToken ct
        ) => inner.UpdateSubscriptionStatusAsync(subscriptionId, status, updatedAtUtc, ct);

        public Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken ct) =>
            inner.DeleteSubscriptionAsync(subscriptionId, ct);
    }
}
