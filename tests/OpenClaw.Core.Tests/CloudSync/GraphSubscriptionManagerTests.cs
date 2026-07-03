using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Handler-level request-shape tests for <see cref="GraphSubscriptionManager"/>
/// (AC-2): create pins <c>POST {BaseUrl}subscriptions</c> with the exact JSON body
/// (changeType, principal-UPN resource, notification/lifecycle URLs, deterministic
/// clientState, FakeTimeProvider-now + lifetime expiration in ISO-8601 UTC); renew
/// pins <c>PATCH {BaseUrl}subscriptions/{id}</c> with <c>expirationDateTime</c> only;
/// successful calls persist/update the store record. Graph responses are recorded
/// in-code constants.
/// </summary>
[TestClass]
public sealed class GraphSubscriptionManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    private const string ClientState = "deterministic-client-state";

    /// <summary>Recorded Graph subscription resource returned by create/renew.</summary>
    private const string SubscriptionResponse = """
        {
          "@odata.context": "https://graph.example.test/v1.0/$metadata#subscriptions/$entity",
          "id": "sub-created-1",
          "resource": "users/paula@contoso.com/mailFolders('Inbox')/messages",
          "applicationId": "00000000-0000-0000-0000-000000000002",
          "changeType": "created,updated",
          "clientState": "deterministic-client-state",
          "notificationUrl": "https://webhook.contoso.com/graph/notifications",
          "lifecycleNotificationUrl": "https://webhook.contoso.com/graph/notifications",
          "expirationDateTime": "2026-07-10T08:00:00Z",
          "creatorId": "creator-1"
        }
        """;

    internal static GraphSubscriptionManager Manager(
        FakeHttpHandler handler,
        FakeSubscriptionStore store,
        FakeTimeProvider timeProvider,
        RecordingReconcileTrigger? trigger = null,
        FixedClientStateGenerator? generator = null,
        Microsoft.Extensions.Logging.ILogger<GraphSubscriptionManager>? logger = null
    )
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-subs", Now.AddHours(1)));

        return new GraphSubscriptionManager(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(
                new GraphAdapterOptions
                {
                    Enabled = true,
                    PrincipalMailboxUpn = "paula@contoso.com",
                    AssistantMailboxUpn = "amy@contoso.com",
                }
            ),
            Options.Create(
                new CloudSyncOptions
                {
                    Enabled = true,
                    NotificationUrl = "https://webhook.contoso.com/graph/notifications",
                }
            ),
            tokenProvider.Object,
            generator ?? new FixedClientStateGenerator(ClientState),
            store,
            trigger ?? new RecordingReconcileTrigger(),
            timeProvider,
            logger ?? NullLogger<GraphSubscriptionManager>.Instance
        );
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [TestMethod]
    public async Task CreateAsync_pins_the_exact_post_request_shape()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Json(SubscriptionResponse);
        });
        var store = new FakeSubscriptionStore();
        var manager = Manager(handler, store, new FakeTimeProvider(Now));

        // Act
        var result = await manager.CreateAsync("req-create", CancellationToken.None);

        // Assert
        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured
            .RequestUri!.AbsoluteUri.Should()
            .Be("https://graph.example.test/v1.0/subscriptions");
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-subs");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-create");

        using var body = JsonDocument.Parse(capturedBody!);
        var root = body.RootElement;
        root.GetProperty("changeType").GetString().Should().Be("created,updated");
        root.GetProperty("resource")
            .GetString()
            .Should()
            .Be("users/paula@contoso.com/mailFolders('Inbox')/messages");
        root.GetProperty("notificationUrl")
            .GetString()
            .Should()
            .Be("https://webhook.contoso.com/graph/notifications");
        root.GetProperty("lifecycleNotificationUrl")
            .GetString()
            .Should()
            .Be("https://webhook.contoso.com/graph/notifications");
        root.GetProperty("clientState").GetString().Should().Be(ClientState);
        root.GetProperty("expirationDateTime")
            .GetString()
            .Should()
            .Be(
                "2026-07-10T08:00:00.0000000Z",
                "expiration is FakeTimeProvider-now + the default 10080-minute lifetime in ISO-8601 UTC"
            );
    }

    [TestMethod]
    public async Task CreateAsync_persists_the_created_record_in_the_store()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => Task.FromResult(Json(SubscriptionResponse)));
        var store = new FakeSubscriptionStore();
        var manager = Manager(handler, store, new FakeTimeProvider(Now));

        // Act
        var result = await manager.CreateAsync("req-persist", CancellationToken.None);

        // Assert
        var expected = new GraphSubscriptionRecord(
            "sub-created-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            ClientState,
            new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero),
            SubscriptionStatus.Active
        );
        result.Data.Should().Be(expected);
        store
            .Records.Should()
            .ContainSingle("the created subscription is persisted")
            .Which.Value.Should()
            .Be(expected);
    }

    [TestMethod]
    public async Task RenewAsync_pins_the_exact_patch_request_shape_with_expiration_only()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Json(SubscriptionResponse.Replace("sub-created-1", "sub-renew-1"));
        });
        var store = new FakeSubscriptionStore();
        store.Records["sub-renew-1"] = new GraphSubscriptionRecord(
            "sub-renew-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            ClientState,
            Now.AddMinutes(20),
            SubscriptionStatus.Active
        );
        var manager = Manager(handler, store, new FakeTimeProvider(Now));

        // Act
        var result = await manager.RenewAsync("sub-renew-1", "req-renew", CancellationToken.None);

        // Assert
        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Patch);
        captured
            .RequestUri!.AbsoluteUri.Should()
            .Be("https://graph.example.test/v1.0/subscriptions/sub-renew-1");

        using var body = JsonDocument.Parse(capturedBody!);
        var properties = body.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        properties
            .Should()
            .Equal(["expirationDateTime"], "the renew body carries expirationDateTime only");
        body.RootElement.GetProperty("expirationDateTime")
            .GetString()
            .Should()
            .Be("2026-07-10T08:00:00.0000000Z");
    }

    [TestMethod]
    public async Task RenewAsync_updates_the_stored_record_expiration_and_status()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Json(SubscriptionResponse.Replace("sub-created-1", "sub-renew-1")))
        );
        var store = new FakeSubscriptionStore();
        store.Records["sub-renew-1"] = new GraphSubscriptionRecord(
            "sub-renew-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            ClientState,
            Now.AddMinutes(20),
            SubscriptionStatus.ReauthorizeFailed
        );
        var manager = Manager(handler, store, new FakeTimeProvider(Now));

        // Act
        var result = await manager.RenewAsync("sub-renew-1", null, CancellationToken.None);

        // Assert
        result.Ok.Should().BeTrue();
        var stored = store.Records["sub-renew-1"];
        stored
            .ExpirationUtc.Should()
            .Be(
                new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero),
                "the store reflects the response's expirationDateTime"
            );
        stored.Status.Should().Be(SubscriptionStatus.Active, "a successful renew reactivates");
        stored.ClientState.Should().Be(ClientState, "renewal does not rotate the clientState");
    }
}
