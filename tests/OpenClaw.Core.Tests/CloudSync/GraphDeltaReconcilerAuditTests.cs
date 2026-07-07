using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Audit-emission tests for <see cref="GraphDeltaReconciler"/> (issue #124, AC2/AC4): a
/// successful <c>RunAsync</c> emits exactly one <c>DeltaReconciliationRun</c> audit record with
/// <see cref="CloudSyncActivityResultCode.Success"/> and <c>CorrelationId == MessageId ==
/// requestId</c>, and a failed <c>RunAsync</c> (Graph error on page 1) emits exactly one such
/// record with <see cref="CloudSyncActivityResultCode.Failure"/> and a populated
/// <c>ErrorDetail</c>.
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
        var auditLog = new FakeActionAuditLog();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now),
            actionAuditLog: auditLog
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.DeltaReconciliationRun);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        record.CorrelationId.Should().Be(record.MessageId);
        record.ErrorDetail.Should().BeNull();
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
        var auditLog = new FakeActionAuditLog();
        var reconciler = GraphDeltaReconcilerTests.Reconciler(
            handler,
            repository,
            linkStore,
            new FakeTimeProvider(GraphDeltaReconcilerTests.Now),
            actionAuditLog: auditLog
        );

        // Act
        await reconciler.ReconcileAsync(Mailbox, CancellationToken.None);

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.DeltaReconciliationRun);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Failure);
        record.ErrorDetail.Should().NotBeNullOrWhiteSpace();
        record.CorrelationId.Should().Be(record.MessageId);
    }
}
