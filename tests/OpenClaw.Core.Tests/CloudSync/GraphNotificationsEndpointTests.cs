using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// HTTP round-trip tests for the mapped <c>POST /graph/notifications</c> route through
/// a minimal in-test host (TestServer + a minimal <c>WebApplication</c> mapping only
/// <c>MapGraphNotificationsEndpoint</c> with substituted stores/queue — no
/// <c>Program.cs</c> dependency): handshake 200 <c>text/plain</c> encoded token, valid
/// notification 202 with the item on the queue, malformed body 400.
/// </summary>
[TestClass]
public sealed class GraphNotificationsEndpointTests
{
    private const string StoredClientState = "client-state-secret-1";

    private const string ValidChangeNotification = """
        {
          "value": [
            {
              "subscriptionId": "sub-1",
              "clientState": "client-state-secret-1",
              "changeType": "created",
              "resource": "Users/paula@contoso.com/Messages/AAMkAGUw",
              "resourceData": { "id": "AAMkAGUw" }
            }
          ]
        }
        """;

    private static FakeSubscriptionStore StoreWithSub1()
    {
        var store = new FakeSubscriptionStore();
        store.Records["sub-1"] = new GraphSubscriptionRecord(
            "sub-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            StoredClientState,
            new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero),
            SubscriptionStatus.Active
        );
        return store;
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartHostAsync(
        FakeSubscriptionStore store,
        RecordingNotificationQueue queue
    )
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ISubscriptionStore>(store);
        builder.Services.AddSingleton<INotificationQueue>(queue);
        builder.Services.AddSingleton<ICloudSyncActivityAuditor>(
            new NoOpCloudSyncActivityAuditor()
        );
        builder.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero))
        );
        builder.Services.AddSingleton<NotificationRequestProcessor>();

        var app = builder.Build();
        app.MapGraphNotificationsEndpoint();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [TestMethod]
    public async Task Handshake_round_trip_returns_200_text_plain_with_the_encoded_token()
    {
        // Arrange
        var (app, client) = await StartHostAsync(StoreWithSub1(), new RecordingNotificationQueue());
        await using var _ = app;

        // Act: %3Cscript%3E in the query arrives at the endpoint as "<script>".
        var response = await client.PostAsync(
            "/graph/notifications?validationToken=abc%3Cscript%3Etoken",
            content: null
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("abc&lt;script&gt;token", "the decoded token is echoed HTML-encoded");
    }

    [TestMethod]
    public async Task Valid_notification_post_returns_202_and_the_item_appears_on_the_queue()
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var (app, client) = await StartHostAsync(StoreWithSub1(), queue);
        await using var _ = app;

        // Act
        var response = await client.PostAsync(
            "/graph/notifications",
            new StringContent(ValidChangeNotification, Encoding.UTF8, "application/json")
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        queue
            .Enqueued.Should()
            .ContainSingle()
            .Which.Should()
            .Be(new NotificationWorkItem("paula@contoso.com", "AAMkAGUw", "created"));
    }

    [TestMethod]
    public async Task Malformed_body_returns_400()
    {
        // Arrange
        var queue = new RecordingNotificationQueue();
        var (app, client) = await StartHostAsync(StoreWithSub1(), queue);
        await using var _ = app;

        // Act
        var response = await client.PostAsync(
            "/graph/notifications",
            new StringContent("{ not json !!!", Encoding.UTF8, "application/json")
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("INVALID_REQUEST");
        queue.Enqueued.Should().BeEmpty();
    }
}
