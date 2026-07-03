using System.Net;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Recovery-semantics tests for <see cref="GraphDeltaReconciler"/> (AC-3): an
/// absent/empty stored link starts a full re-sync from the initial delta request; the
/// <c>missed</c>-triggered entry point re-syncs even when a link is stored; the
/// <c>MaxPages</c> bound stops a runaway nextLink chain; <c>@removed</c> entries are
/// skipped at Debug and not upserted; re-running the same pages is idempotent through
/// the <c>ON CONFLICT(bridge_id)</c> upsert; upserted rows carry the D-3 synthesized
/// ready/graph status; and an <c>ingest_runs</c> row with operation
/// <c>delta_reconcile</c> is written for success and for a failed walk.
/// </summary>
[TestClass]
public sealed class GraphDeltaReconcilerRecoveryTests
{
    private const string Mailbox = GraphDeltaReconcilerTests.Mailbox;

    private const string RemovedEntryPage = """
        {
          "value": [
            { "id": "delta-live-1", "subject": "Still alive", "isRead": false },
            { "id": "delta-gone-1", "@removed": { "reason": "deleted" } }
          ],
          "@odata.deltaLink": "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$deltatoken=after-removed"
        }
        """;

    private const string RunawayPage = """
        {
          "value": [
            { "id": "delta-runaway-1", "subject": "Runs away", "isRead": false }
          ],
          "@odata.nextLink": "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$skiptoken=again"
        }
        """;

    private static async Task<(
        OpenClaw.Core.CoreCacheRepository Repository,
        string ConnectionString
    )> NewRepositoryAsync(string label)
    {
        var connectionString = GraphDeltaReconcilerTests.NewConnectionString(label);
        var repository = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await repository.InitializeAsync();
        return (repository, connectionString);
    }

    private static async Task<List<(string Outcome, string? Error)>> ReadIngestRunsAsync(
        string connectionString
    )
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT outcome, error_message FROM ingest_runs WHERE operation_name = 'delta_reconcile' ORDER BY id;";
        var rows = new List<(string, string?)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        }

        return rows;
    }

    [TestMethod]
    public async Task Empty_stored_link_starts_from_the_initial_delta_request()
    {
        // Arrange: an empty (not absent) stored link must also trigger a full re-sync.
        var requestUris = new List<string>();
        var handler = GraphDeltaReconcilerTests.PagedHandler(
            requestUris,
            GraphDeltaReconcilerTests.TerminalPage
        );
        var (repository, _) = await NewRepositoryAsync("emptylink");
        using var _repo = repository;
        var linkStore = new FakeDeltaLinkStore();
        linkStore.Links[Mailbox] = string.Empty;
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now)
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        new Uri(requestUris.Single())
            .AbsolutePath.Should()
            .Be(
                "/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta",
                "an empty stored link falls back to the initial delta request"
            );
    }

    [TestMethod]
    public async Task Missed_triggered_entry_point_resyncs_even_when_a_link_is_stored()
    {
        // Arrange
        var requestUris = new List<string>();
        var handler = GraphDeltaReconcilerTests.PagedHandler(
            requestUris,
            GraphDeltaReconcilerTests.TerminalPage
        );
        var (repository, _) = await NewRepositoryAsync("resync");
        using var _repo = repository;
        var linkStore = new FakeDeltaLinkStore();
        linkStore.Links[Mailbox] =
            "https://graph.example.test/v1.0/users/paula%40contoso.com/mailFolders/Inbox/messages/delta?$deltatoken=stale";
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now)
        );

        // Act: the missed lifecycle trigger uses the full re-sync entry point.
        await ((IDeltaReconcileTrigger)reconciler).TriggerResyncAsync(
            Mailbox,
            CancellationToken.None
        );

        // Assert
        requestUris
            .Single()
            .Should()
            .NotContain("deltatoken=stale", "a missed-triggered re-sync ignores the stored link");
        new Uri(requestUris.Single())
            .Query.Should()
            .Contain("select", "the re-sync starts from the initial delta request");
    }

    [TestMethod]
    public async Task MaxPages_bound_stops_a_runaway_next_link_chain()
    {
        // Arrange: every page returns another nextLink; MaxPages = 2 must stop the walk.
        var requestUris = new List<string>();
        var handler = GraphDeltaReconcilerTests.PagedHandler(requestUris, RunawayPage);
        var (repository, connectionString) = await NewRepositoryAsync("runaway");
        using var _repo = repository;
        var linkStore = new FakeDeltaLinkStore();
        var logger = new CapturingLogger<GraphDeltaReconciler>();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now),
            new GraphAdapterOptions
            {
                Enabled = true,
                PrincipalMailboxUpn = Mailbox,
                AssistantMailboxUpn = "amy@contoso.com",
                MaxPages = 2,
            },
            logger
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        requestUris.Should().HaveCount(2, "the walk stops at the MaxPages bound");
        linkStore.Sets.Should().BeEmpty("no deltaLink was reached, so none is persisted");
        logger.Levels.Should().Contain(LogLevel.Warning, "the truncated walk is fail-visible");
        var runs = await ReadIngestRunsAsync(connectionString);
        runs.Should()
            .ContainSingle()
            .Which.Outcome.Should()
            .Be("failed", "a walk that never reaches the deltaLink did not converge");
    }

    [TestMethod]
    public async Task Removed_entries_are_skipped_with_debug_and_not_upserted()
    {
        // Arrange
        var requestUris = new List<string>();
        var handler = GraphDeltaReconcilerTests.PagedHandler(requestUris, RemovedEntryPage);
        var (repository, _) = await NewRepositoryAsync("removed");
        using var _repo = repository;
        var linkStore = new FakeDeltaLinkStore();
        var logger = new CapturingLogger<GraphDeltaReconciler>();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now),
            logger: logger
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        (await repository.GetMessageAsync("delta-live-1"))
            .Should()
            .NotBeNull();
        (await repository.GetMessageAsync("delta-gone-1"))
            .Should()
            .BeNull("@removed entries are skipped, not upserted");
        logger
            .Entries.Should()
            .Contain(e => e.Level == LogLevel.Debug && e.Message.Contains("delta-gone-1"));
    }

    [TestMethod]
    public async Task Rerunning_the_same_pages_is_idempotent_and_rows_carry_ready_graph_status()
    {
        // Arrange
        var requestUris = new List<string>();
        var pages = new[]
        {
            GraphDeltaReconcilerTests.Page1,
            GraphDeltaReconcilerTests.Page2,
            GraphDeltaReconcilerTests.TerminalPage,
        };
        var page = 0;
        var handler = new FakeHttpHandler(request =>
        {
            requestUris.Add(request.RequestUri!.AbsoluteUri);
            var body = pages[page % pages.Length];
            page++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            );
        });
        var (repository, connectionString) = await NewRepositoryAsync("idempotent");
        using var _repo = repository;
        var linkStore = new FakeDeltaLinkStore();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now)
        );

        // Act: two full re-syncs over the identical pages.
        await ((IDeltaReconcileTrigger)reconciler).TriggerResyncAsync(
            Mailbox,
            CancellationToken.None
        );
        await ((IDeltaReconcileTrigger)reconciler).TriggerResyncAsync(
            Mailbox,
            CancellationToken.None
        );

        // Assert: the ON CONFLICT(bridge_id) upsert keeps one row per message.
        var counts = await repository.GetCountsAsync();
        counts.Messages.Should().Be(3, "re-running the same pages must not duplicate rows");

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT bridge_mode, cache_stale FROM messages WHERE bridge_id = 'delta-msg-1';";
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("graph", "the D-3 synthesized status mode is graph");
        reader.GetInt32(1).Should().Be(0, "the D-3 synthesized status is not stale");

        var runs = await ReadIngestRunsAsync(connectionString);
        runs.Should().HaveCount(2).And.OnlyContain(r => r.Outcome == "success");
    }

    [TestMethod]
    public async Task Failed_walk_records_a_failed_delta_reconcile_ingest_run()
    {
        // Arrange: a terminal 400 error envelope on the first page.
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """{ "error": { "code": "BadRequest", "message": "Bad delta." } }"""
                    ),
                }
            )
        );
        var (repository, connectionString) = await NewRepositoryAsync("failedrun");
        using var _repo = repository;
        var linkStore = new FakeDeltaLinkStore();
        var logger = new CapturingLogger<GraphDeltaReconciler>();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now),
            logger: logger
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        var runs = await ReadIngestRunsAsync(connectionString);
        runs.Should().ContainSingle().Which.Outcome.Should().Be("failed");
        linkStore.Sets.Should().BeEmpty("no link is persisted on a failed walk");
        logger.Levels.Should().Contain(LogLevel.Warning);
    }
}
