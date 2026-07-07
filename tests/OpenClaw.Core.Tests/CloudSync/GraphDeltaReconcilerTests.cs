using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
/// Multi-page delta-walk tests for <see cref="GraphDeltaReconciler"/> (AC-3) with
/// recorded payload constants: a three-page walk (two <c>@odata.nextLink</c> pages,
/// then the <c>@odata.deltaLink</c> page) issues requests in order with the initial
/// URL shape pinned, follows nextLinks verbatim, upserts every page's messages through
/// the real <c>CoreCacheRepository</c> sink, and persists the terminal delta link; a
/// subsequent reconcile resumes from the stored link verbatim.
/// </summary>
[TestClass]
public sealed class GraphDeltaReconcilerTests
{
    internal const string Mailbox = "paula@contoso.com";

    internal static readonly DateTimeOffset Now = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    internal const string NextLink2 =
        "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$skiptoken=page2";

    internal const string NextLink3 =
        "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$skiptoken=page3";

    internal const string FinalDeltaLink =
        "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$deltatoken=final-round-1";

    internal const string Page1 = """
        {
          "value": [
            { "id": "delta-msg-1", "subject": "Page one first", "isRead": false },
            { "id": "delta-msg-2", "subject": "Page one second", "isRead": true }
          ],
          "@odata.nextLink": "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$skiptoken=page2"
        }
        """;

    internal const string Page2 = """
        {
          "value": [
            { "id": "delta-msg-3", "subject": "Page two only", "isRead": false }
          ],
          "@odata.nextLink": "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$skiptoken=page3"
        }
        """;

    internal const string TerminalPage = """
        {
          "value": [],
          "@odata.deltaLink": "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$deltatoken=final-round-1"
        }
        """;

    internal static string NewConnectionString(string label) =>
        $"Data Source=core-delta-{label}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    internal static GraphDeltaReconciler Reconciler(
        FakeHttpHandler handler,
        OpenClaw.Core.CoreCacheRepository repository,
        FakeDeltaLinkStore linkStore,
        FakeTimeProvider timeProvider,
        GraphAdapterOptions? options = null,
        ILogger<GraphDeltaReconciler>? logger = null,
        OpenClaw.Core.Agent.IActionAuditLog? actionAuditLog = null
    )
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-delta", Now.AddHours(1)));

        return new GraphDeltaReconciler(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(
                options
                    ?? new GraphAdapterOptions
                    {
                        Enabled = true,
                        PrincipalMailboxUpn = Mailbox,
                        AssistantMailboxUpn = "amy@contoso.com",
                    }
            ),
            tokenProvider.Object,
            linkStore,
            repository,
            timeProvider,
            logger ?? NullLogger<GraphDeltaReconciler>.Instance,
            actionAuditLog ?? new FakeActionAuditLog()
        );
    }

    internal static FakeHttpHandler PagedHandler(List<string> requestUris, params string[] pages)
    {
        var page = 0;
        return new FakeHttpHandler(request =>
        {
            requestUris.Add(request.RequestUri!.AbsoluteUri);
            var body = pages[Math.Min(page, pages.Length - 1)];
            page++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            );
        });
    }

    [TestMethod]
    public async Task Three_page_walk_pins_the_initial_url_follows_next_links_and_persists_the_delta_link()
    {
        // Arrange
        var requestUris = new List<string>();
        var handler = PagedHandler(requestUris, Page1, Page2, TerminalPage);
        using var repository = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("walk"));
        await repository.InitializeAsync();
        var linkStore = new FakeDeltaLinkStore();
        var reconciler = Reconciler(handler, repository, linkStore, new FakeTimeProvider(Now));

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert: initial URL shape (path + $select) pinned, nextLinks followed verbatim.
        requestUris.Should().HaveCount(3, "two nextLink pages plus the terminal page");
        var first = new Uri(requestUris[0]);
        first
            .AbsolutePath.Should()
            .Be("/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta");
        Uri.UnescapeDataString(first.Query)
            .Should()
            .Be(
                $"?$select={GraphHostAdapterClient.MessageSelect}",
                "the initial delta request pins the spec $select list"
            );
        requestUris[1].Should().Be(NextLink2, "the second request follows the nextLink verbatim");
        requestUris[2].Should().Be(NextLink3, "the third request follows the nextLink verbatim");

        // Assert: every page's messages hit the repository sink.
        (await repository.GetMessageAsync("delta-msg-1"))!
            .Subject.Should()
            .Be("Page one first");
        (await repository.GetMessageAsync("delta-msg-2"))!.Unread.Should().BeFalse();
        (await repository.GetMessageAsync("delta-msg-3"))!.Subject.Should().Be("Page two only");

        // Assert: the terminal deltaLink is persisted per mailbox.
        linkStore
            .Links.Should()
            .ContainSingle()
            .Which.Should()
            .Be(new KeyValuePair<string, string>(Mailbox, FinalDeltaLink));
    }

    [TestMethod]
    public async Task Subsequent_reconcile_resumes_from_the_stored_link_verbatim()
    {
        // Arrange: a stored delta link from a previous round.
        var requestUris = new List<string>();
        var handler = PagedHandler(requestUris, TerminalPage);
        using var repository = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("resume"));
        await repository.InitializeAsync();
        var linkStore = new FakeDeltaLinkStore();
        linkStore.Links[Mailbox] = FinalDeltaLink;
        var reconciler = Reconciler(handler, repository, linkStore, new FakeTimeProvider(Now));

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        requestUris
            .Should()
            .ContainSingle("the stored link is the whole round when it returns a deltaLink")
            .Which.Should()
            .Be(FinalDeltaLink, "the stored delta link is replayed verbatim");
    }

    /// <summary>
    /// A page body with no <c>value</c> property exercises the false arm of the
    /// value-present-and-array gate in <c>ParseDeltaPage</c> (CR-117-02): nothing is
    /// upserted and the walk still follows the nextLink to the terminal deltaLink page.
    /// </summary>
    [TestMethod]
    public async Task Reconcile_page_without_a_value_property_upserts_nothing_and_completes_the_walk()
    {
        // Arrange: the first page carries only a nextLink (no "value" property).
        const string noValuePage = """
            {
              "@odata.nextLink": "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$skiptoken=page2"
            }
            """;
        var requestUris = new List<string>();
        var handler = PagedHandler(requestUris, noValuePage, TerminalPage);
        using var repository = new OpenClaw.Core.CoreCacheRepository(
            NewConnectionString("novalue")
        );
        await repository.InitializeAsync();
        var linkStore = new FakeDeltaLinkStore();
        var reconciler = Reconciler(handler, repository, linkStore, new FakeTimeProvider(Now));

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        requestUris.Should().HaveCount(2, "the no-value page still walks to the terminal page");
        (await repository.GetCountsAsync())
            .Messages.Should()
            .Be(0, "a page without a value property upserts nothing");
        linkStore
            .Links.Should()
            .ContainSingle("the walk succeeds and persists the terminal deltaLink")
            .Which.Should()
            .Be(new KeyValuePair<string, string>(Mailbox, FinalDeltaLink));
    }
}
