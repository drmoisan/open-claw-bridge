using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Deterministic <see cref="DeltaReconciliationWorker"/> tests with
/// <see cref="FakeTimeProvider"/>: no reconcile runs before the first interval
/// elapses, advancing fake time by the interval triggers exactly one reconcile, and a
/// failing reconcile logs a Warning while the next tick still runs. Fake time is
/// advanced in small steps per yield (the repository's <c>AwaitWithTimeAdvance</c>
/// pattern) so timer registration races cannot stall the test.
/// </summary>
[TestClass]
public sealed class DeltaReconciliationWorkerTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);

    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private static readonly CloudSyncOptions WorkerOptions = new()
    {
        Enabled = true,
        NotificationUrl = "https://webhook.contoso.com/graph/notifications",
        ReconcileIntervalMinutes = 60,
    };

    private static async Task<(
        DeltaReconciliationWorker Worker,
        OpenClaw.Core.CoreCacheRepository Repository,
        List<string> RequestUris,
        CapturingLogger<GraphDeltaReconciler> ReconcilerLogger
    )> BuildAsync(FakeTimeProvider timeProvider, Func<int, HttpResponseMessage> responseForCall)
    {
        var requestUris = new List<string>();
        var handler = new FakeHttpHandler(request =>
        {
            requestUris.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(responseForCall(requestUris.Count));
        });
        var repository = new OpenClaw.Core.CoreCacheRepository(
            GraphDeltaReconcilerTests.NewConnectionString("worker")
        );
        await repository.InitializeAsync();
        var reconcilerLogger = new CapturingLogger<GraphDeltaReconciler>();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            new FakeDeltaLinkStore(),
            timeProvider,
            logger: reconcilerLogger
        );
        var worker = new DeltaReconciliationWorker(
            reconciler,
            Options.Create(
                new GraphAdapterOptions
                {
                    Enabled = true,
                    PrincipalMailboxUpn = GraphDeltaReconcilerTests.Mailbox,
                    AssistantMailboxUpn = "amy@contoso.com",
                }
            ),
            Options.Create(WorkerOptions),
            timeProvider,
            NullLogger<DeltaReconciliationWorker>.Instance
        );
        return (worker, repository, requestUris, reconcilerLogger);
    }

    private static HttpResponseMessage TerminalPage() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(GraphDeltaReconcilerTests.TerminalPage),
        };

    private static HttpResponseMessage BadRequest() =>
        new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{ "error": { "code": "BadRequest", "message": "Bad delta." } }"""
            ),
        };

    [TestMethod]
    public async Task No_reconcile_runs_before_the_first_interval_elapses()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(GraphDeltaReconcilerTests.Now);
        var (worker, repository, requestUris, _) = await BuildAsync(
            timeProvider,
            _ => TerminalPage()
        );
        using var _repo = repository;

        // Act: advance to one tick before the first interval boundary, in steps, with
        // yields so any worker continuation could run if one were (wrongly) scheduled.
        await worker.StartAsync(CancellationToken.None);
        for (var minute = 0; minute < 59; minute++)
        {
            timeProvider.Advance(Step);
            await Task.Yield();
        }

        timeProvider.Advance(Step - TimeSpan.FromTicks(1));
        for (var i = 0; i < 100; i++)
        {
            await Task.Yield();
        }

        await worker.StopAsync(CancellationToken.None);

        // Assert
        requestUris
            .Should()
            .BeEmpty("the worker waits a full interval before the first reconcile");
    }

    [TestMethod]
    public async Task Advancing_by_the_interval_triggers_exactly_one_reconcile()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(GraphDeltaReconcilerTests.Now);
        var (worker, repository, requestUris, _) = await BuildAsync(
            timeProvider,
            _ => TerminalPage()
        );
        using var _repo = repository;

        // Act: step fake time forward until the first interval elapses and the
        // reconcile lands; total advancement stays far below a second interval.
        await worker.StartAsync(CancellationToken.None);
        await WaitForAsync(() => requestUris.Count >= 1, () => timeProvider.Advance(Step));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        requestUris
            .Should()
            .ContainSingle("one interval elapse runs exactly one reconcile")
            .Which.Should()
            .Contain("/messages/delta");
    }

    [TestMethod]
    public async Task A_failing_reconcile_logs_warning_and_the_next_tick_still_runs()
    {
        // Arrange: the first reconcile fails terminally (400); the second succeeds.
        var timeProvider = new FakeTimeProvider(GraphDeltaReconcilerTests.Now);
        var (worker, repository, requestUris, reconcilerLogger) = await BuildAsync(
            timeProvider,
            call => call == 1 ? BadRequest() : TerminalPage()
        );
        using var _repo = repository;

        // Act
        await worker.StartAsync(CancellationToken.None);
        await WaitForAsync(() => requestUris.Count >= 1, () => timeProvider.Advance(Step));
        await WaitForAsync(() => requestUris.Count >= 2, () => timeProvider.Advance(Step));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        requestUris
            .Count.Should()
            .BeGreaterThanOrEqualTo(2, "the failed tick does not stop the loop");
        reconcilerLogger
            .Levels.Should()
            .Contain(LogLevel.Warning, "the failed reconcile is logged");
    }

    /// <summary>
    /// Cooperatively advances fake time and yields until <paramref name="condition"/>
    /// holds (bounded; no wall-clock waits) — the repository's
    /// <c>AwaitWithTimeAdvance</c> pattern.
    /// </summary>
    private static async Task WaitForAsync(Func<bool> condition, Action advance)
    {
        var safety = 0;
        while (!condition())
        {
            if (++safety > 10_000)
            {
                throw new AssertFailedException(
                    "The worker did not reach the expected state under fake-time advancement."
                );
            }

            advance();
            await Task.Yield();
        }
    }
}
