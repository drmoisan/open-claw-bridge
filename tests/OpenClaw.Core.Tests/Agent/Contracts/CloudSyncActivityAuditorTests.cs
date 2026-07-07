using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent.Contracts;

/// <summary>
/// Adapter-mapping tests for <see cref="CloudSyncActivityAuditor"/> (issue #124,
/// architecture-boundary revision): each <see cref="ICloudSyncActivityAuditor"/> port
/// method must call <see cref="IActionAuditLog.RecordAsync"/> exactly once with an
/// <see cref="ActionAuditRecord"/> whose <c>ActionType</c>/<c>ResultCode</c>/
/// <c>ActingFlags</c>/<c>MessageId</c>/<c>CorrelationId</c> match the mapping owned by the
/// adapter.
/// </summary>
[TestClass]
public sealed class CloudSyncActivityAuditorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 7, 3, 0, 0, TimeSpan.Zero);

    private const string Mailbox = "paula@contoso.com";

    private static (CloudSyncActivityAuditor Auditor, Mock<IActionAuditLog> AuditLog) NewAuditor()
    {
        var auditLog = new Mock<IActionAuditLog>();
        var auditor = new CloudSyncActivityAuditor(auditLog.Object, new FakeTimeProvider(Now));
        return (auditor, auditLog);
    }

    [TestMethod]
    public async Task RecordSubscriptionCreatedAsync_success_records_the_mapped_audit_record()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordSubscriptionCreatedAsync(
            Mailbox,
            "sub-1",
            "req-1",
            success: true,
            errorDetail: null,
            CancellationToken.None
        );

        // Assert
        auditLog.Verify(
            a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        captured!.ActionType.Should().Be(CloudSyncActivityType.SubscriptionCreated);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        captured.ActingFlags.Should().Be(CloudSyncActingFlags.NotApplicable);
        captured.MessageId.Should().Be("sub-1");
        captured.CorrelationId.Should().Be("req-1");
        captured.Mailbox.Should().Be(Mailbox);
        captured.ErrorDetail.Should().BeNull();
    }

    [TestMethod]
    public async Task RecordSubscriptionCreatedAsync_failure_with_no_subscriptionId_falls_back_to_mailbox()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordSubscriptionCreatedAsync(
            Mailbox,
            subscriptionId: null,
            "req-2",
            success: false,
            errorDetail: "boom",
            CancellationToken.None
        );

        // Assert
        captured!.ActionType.Should().Be(CloudSyncActivityType.SubscriptionCreated);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Failure);
        captured.MessageId.Should().Be(Mailbox, "no subscription id exists yet on a failed create");
        captured.ErrorDetail.Should().Be("boom");
    }

    [TestMethod]
    public async Task RecordSubscriptionRenewedAsync_records_the_mapped_audit_record()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordSubscriptionRenewedAsync(
            Mailbox,
            "sub-renew-1",
            "req-3",
            success: true,
            errorDetail: null,
            CancellationToken.None
        );

        // Assert
        auditLog.Verify(
            a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        captured!.ActionType.Should().Be(CloudSyncActivityType.SubscriptionRenewed);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        captured.MessageId.Should().Be("sub-renew-1");
        captured.CorrelationId.Should().Be("req-3");
    }

    [TestMethod]
    public async Task RecordSubscriptionExpiredAsync_records_a_failure_audit_record()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordSubscriptionExpiredAsync(
            Mailbox,
            "sub-expired-1",
            "req-4",
            errorDetail: "Access token is empty.",
            CancellationToken.None
        );

        // Assert
        auditLog.Verify(
            a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        captured!.ActionType.Should().Be(CloudSyncActivityType.SubscriptionExpired);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Failure);
        captured.MessageId.Should().Be("sub-expired-1");
        captured.ErrorDetail.Should().Be("Access token is empty.");
    }

    [TestMethod]
    public async Task RecordSubscriptionRemovedAsync_records_a_success_audit_record()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordSubscriptionRemovedAsync(
            Mailbox,
            "sub-removed-1",
            "corr-5",
            CancellationToken.None
        );

        // Assert
        auditLog.Verify(
            a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        captured!.ActionType.Should().Be(CloudSyncActivityType.SubscriptionRemoved);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        captured.MessageId.Should().Be("sub-removed-1");
        captured.CorrelationId.Should().Be("corr-5");
        captured.ErrorDetail.Should().BeNull();
    }

    [TestMethod]
    public async Task RecordWebhookReceivedAsync_records_a_success_audit_record()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordWebhookReceivedAsync(
            Mailbox,
            "AAMkAGUw",
            "corr-6",
            CancellationToken.None
        );

        // Assert
        auditLog.Verify(
            a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        captured!.ActionType.Should().Be(CloudSyncActivityType.WebhookReceived);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        captured.MessageId.Should().Be("AAMkAGUw");
        captured.CorrelationId.Should().Be("corr-6");
    }

    [TestMethod]
    public async Task RecordWebhookRejectedAsync_records_the_rejection_reason_as_the_result_code()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordWebhookRejectedAsync(
            Mailbox,
            "sub-unknown",
            CloudSyncActivityResultCode.UnknownSubscription,
            "corr-7",
            CancellationToken.None
        );

        // Assert
        auditLog.Verify(
            a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        captured!.ActionType.Should().Be(CloudSyncActivityType.WebhookRejected);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.UnknownSubscription);
        captured.MessageId.Should().Be("sub-unknown");
        captured.CorrelationId.Should().Be("corr-7");
    }

    [TestMethod]
    public async Task RecordDeltaReconciliationRunAsync_success_records_the_mapped_audit_record()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordDeltaReconciliationRunAsync(
            Mailbox,
            "req-delta-1",
            success: true,
            errorDetail: null,
            CancellationToken.None
        );

        // Assert
        auditLog.Verify(
            a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        captured!.ActionType.Should().Be(CloudSyncActivityType.DeltaReconciliationRun);
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
    }

    /// <summary>
    /// The CorrelationId==MessageId invariant for delta-reconciliation records, now
    /// validated at the adapter (Phase 9) rather than at the CloudSync call site.
    /// </summary>
    [TestMethod]
    public async Task RecordDeltaReconciliationRunAsync_correlationId_equals_messageId()
    {
        // Arrange
        var (auditor, auditLog) = NewAuditor();
        ActionAuditRecord? captured = null;
        auditLog
            .Setup(a => a.RecordAsync(It.IsAny<ActionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ActionAuditRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        // Act
        await auditor.RecordDeltaReconciliationRunAsync(
            Mailbox,
            "req-delta-2",
            success: false,
            errorDetail: "Bad delta.",
            CancellationToken.None
        );

        // Assert
        captured!.CorrelationId.Should().Be(captured.MessageId);
        captured.MessageId.Should().Be("req-delta-2");
        captured.ResultCode.Should().Be(CloudSyncActivityResultCode.Failure);
        captured.ErrorDetail.Should().Be("Bad delta.");
    }
}
