using System.Net;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Audit-emission tests for <see cref="GraphDeltaReconciler"/> (issue #124, AC2/AC4; revised
/// in the Phase 9 architecture-boundary seam): a successful <c>RunAsync</c> calls
/// <see cref="ICloudSyncActivityAuditor.RecordDeltaReconciliationRunAsync"/> exactly once with
/// <c>success: true</c> and a non-empty <c>requestId</c> (used as both message id and
/// correlation id at the adapter), and a failed <c>RunAsync</c> (Graph error on page 1) calls
/// it exactly once with <c>success: false</c> and a populated error detail.
/// </summary>
[TestClass]
public sealed class GraphDeltaReconcilerAuditTests
{
    private const string Mailbox = GraphDeltaReconcilerTests.Mailbox;

    [TestMethod]
    public async Task Successful_run_emits_exactly_one_delta_reconciliation_run_audit_record()
    {
        // Arrange
        var requestUris = new List<string>();
        var handler = GraphDeltaReconcilerTests.PagedHandler(
            requestUris,
            GraphDeltaReconcilerTests.TerminalPage
        );
        using var repository = new OpenClaw.Core.CoreCacheRepository(
            GraphDeltaReconcilerTests.NewConnectionString("audit-success")
        );
        await repository.InitializeAsync();
        var linkStore = new FakeDeltaLinkStore();
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now),
            activityAuditor: auditor.Object
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        auditor.Verify(
            a =>
                a.RecordDeltaReconciliationRunAsync(
                    Mailbox,
                    It.Is<string>(id => !string.IsNullOrWhiteSpace(id)),
                    true,
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }

    [TestMethod]
    public async Task Failed_run_on_page_one_emits_exactly_one_delta_reconciliation_run_audit_record()
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
        using var repository = new OpenClaw.Core.CoreCacheRepository(
            GraphDeltaReconcilerTests.NewConnectionString("audit-failure")
        );
        await repository.InitializeAsync();
        var linkStore = new FakeDeltaLinkStore();
        var auditor = new Mock<ICloudSyncActivityAuditor>();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now),
            activityAuditor: auditor.Object
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        auditor.Verify(
            a =>
                a.RecordDeltaReconciliationRunAsync(
                    Mailbox,
                    It.Is<string>(id => !string.IsNullOrWhiteSpace(id)),
                    false,
                    It.Is<string?>(detail => !string.IsNullOrWhiteSpace(detail)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }
}
